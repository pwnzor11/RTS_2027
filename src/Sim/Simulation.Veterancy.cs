// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Sim.Core;
using Rts.Sim.Rules;
using Rts.Sim.Veterancy;
using Rts.Sim.World;

namespace Rts.Sim;

// Generic veterancy (docs/design/14_veterancy.md, D-127). VET-1: the "veterancy" part and its accessor. VET-2: kill credit (the shooter
// ActorRef travels Fire -> ProjectileStore -> Detonate -> Hurt -> PendingHit; ApplyPendingDamage calls OnVeterancyKill). VET-3: rank and
// bonuses.
public sealed partial class Simulation
{
    /// <summary>The [veterancy] constants. A global block (no upgrade changes it), read from the base rules in this one place.</summary>
    internal VeterancyRules VetRules => Setup.Rules.Veterancy;

    /// <summary>The "veterancy" part (VET-1): kill value and last-combat tick per vehicle and infantry slot.</summary>
    public VeterancyState Veterancy => _veterancy;

    /// <summary>
    /// A hit took <paramref name="victim"/> from positive health to zero or below (called from ApplyPendingDamage). The shooter of the
    /// lethal hit earns the victim's cost x kill permille / 1000 as kill value when: veterancy is enabled, the shooter is a live vehicle or
    /// infantry soldier that is still the same actor (its slot and generation match: no credit once the slot was freed or reused), its
    /// type can rank, and the victim belongs to an Enemy of the hit's owner (the test OnLethalHit uses). Hits without a shooter (death
    /// weapons, superweapons, radiation, storms) never credit. The cost is the victim type's rules cost in its owner's table (not the discounted price paid). Garrisoned soldiers fire through Fire like any other, so they earn their own value.
    /// </summary>
    private void OnVeterancyKill(ActorRef shooter, ActorRef victim, byte source)
    {
        VeterancyRules vr = VetRules;
        if (!vr.Enabled || shooter.Id.IsNone || shooter.Kind > ActorKind.Infantry) return;
        if (!SlotsOf(shooter.Kind).IsAlive(shooter.Id)) return;
        byte victimOwner = PlayerAt(victim.Kind, victim.Id.Index);
        if (source >= _players.Count || victimOwner >= _players.Count || RelationOf(source, victimOwner) != Relation.Enemy) return;
        int s = shooter.Id.Index;
        if (!CanRankAt(shooter.Kind, s)) return;
        int vi = victim.Id.Index;
        SimRules vrules = RulesOf(victimOwner);   // the victim owner's table: a cost upgrade changes what the kill is worth
        long add = victim.Kind switch
        {
            ActorKind.Vehicle => (long)vrules.Vehicles[_units.TypeIndex[vi]].Cost * vr.KillPermilleUnit / 1000,
            ActorKind.Infantry => (long)vrules.Infantry[_infantry.TypeIndex[vi]].Cost * vr.KillPermilleUnit / 1000,
            ActorKind.Building => (long)vrules.Buildings[_buildings.TypeIndex[vi]].Cost * vr.KillPermilleBuilding / 1000,
            _ => 0,
        };
        if (add <= 0) return;
        int[] kills = shooter.Kind == ActorKind.Vehicle ? _veterancy.VehicleKillValue : _veterancy.InfantryKillValue;
        kills[s] = (int)Math.Min((long)int.MaxValue, kills[s] + add);
    }

    // ---- rank and bonuses (VET-3) -------------------------------------------------------------------------------

    /// <summary>
    /// Veterancy rank of a vehicle or infantry slot: 0 (recruit) to 3 (hero), derived from the stored kill value and the rules table
    /// (thresholds scaled by the owner's type <see cref="VehicleType.VetThresholdPermille"/>, so a lower threshold only ever raises the
    /// rank). 0 when veterancy is disabled, for other kinds, for a free slot, and for types that cannot rank.
    /// </summary>
    public int RankOf(ActorKind kind, int i)
    {
        VeterancyRules vr = VetRules;
        if (!vr.Enabled || kind > ActorKind.Infantry || !SlotsOf(kind).IsIndexAlive(i)) return 0;
        int kv = kind == ActorKind.Vehicle ? _veterancy.VehicleKillValue[i] : _veterancy.InfantryKillValue[i];
        if (kv <= 0) return 0;
        SimRules r = RulesOf(PlayerAt(kind, i));
        int scale;
        if (kind == ActorKind.Vehicle)
        {
            VehicleType t = r.Vehicles[_units.TypeIndex[i]];
            if (!t.CanRank) return 0;
            scale = t.VetThresholdPermille;
        }
        else
        {
            InfantryType t = r.Infantry[_infantry.TypeIndex[i]];
            if (!t.CanRank) return 0;
            scale = t.VetThresholdPermille;
        }
        int rank = 0;
        for (int k = 0; k < VeterancyRules.Ranks; k++)
            if (kv >= Math.Max(1L, (long)vr.Thresholds[k] * scale / 1000)) rank = k + 1;
        return rank;
    }

    /// <summary>Applies a per-rank permille factor (<paramref name="table"/> entry rank - 1) to <paramref name="value"/>; unchanged at rank 0.</summary>
    private static int ScaleByRank(IReadOnlyList<int> table, int rank, int value) =>
        rank <= 0 ? value : (int)((long)value * table[rank - 1] / 1000);

    /// <summary>Records that the vehicle or soldier fired or was hit now (the self-heal quiet timer). A no-op while veterancy is off, so
    /// the part stays untouched (and hashes as before) in games that do not use it.</summary>
    private void MarkCombat(ActorKind kind, int i)
    {
        if (!VetRules.Enabled) return;
        if (kind == ActorKind.Vehicle) _veterancy.VehicleLastCombat[i] = Tick + 1;   // stored as tick + 1 so 0 can mean "never"
        else if (kind == ActorKind.Infantry) _veterancy.InfantryLastCombat[i] = Tick + 1;
    }

    /// <summary>Movement speed factor in permille: the aura speed composed with the rank's speed bonus (identical to
    /// <c>Effects.SpeedPermille</c> at rank 0 or with veterancy off). Used by the move and air missions.</summary>
    internal int MoveSpeedPermille(ActorKind kind, int i)
    {
        int aura = _effects.SpeedPermille(kind, i);
        int rank = RankOf(kind, i);
        return rank == 0 ? aura : ScaleByRank(VetRules.SpeedPermille, rank, aura);
    }

    /// <summary>
    /// Per tick (before RunCleanup): rank-3 self-heal. A ranked soldier or vehicle with a non-zero heal for its rank recovers
    /// <c>heal_permille</c> of its max health (at least 1) whenever <c>(Tick + slot) % heal_interval_ticks == 0</c> and it neither fired nor
    /// was hit for <c>heal_quiet_ticks</c>. No stored timer beyond the last-combat tick; slot order; integers only.
    /// </summary>
    private void RunVeterancy()
    {
        VeterancyRules vr = VetRules;
        if (!vr.Enabled) return;
        for (int k = 0; k <= (int)ActorKind.Infantry; k++)
        {
            var kind = (ActorKind)k;
            SlotArray slots = SlotsOf(kind);
            for (int i = 1; i < slots.Capacity; i++)
            {
                if ((Tick + i) % vr.HealIntervalTicks != 0 || !slots.IsIndexAlive(i)) continue;
                int hp = HealthAt(kind, i);
                if (hp <= 0) continue;
                int rank = RankOf(kind, i);
                int permille = rank == 0 ? 0 : vr.HealPermille[rank - 1];
                if (permille <= 0) continue;
                int last = _veterancy.LastCombat(kind, i);
                if (last != 0 && Tick - (last - 1) < vr.HealQuietTicks) continue;
                SimRules r = RulesOf(PlayerAt(kind, i));
                int max = kind == ActorKind.Vehicle ? r.Vehicles[_units.TypeIndex[i]].MaxHealth : r.Infantry[_infantry.TypeIndex[i]].MaxHealth;
                if (hp >= max) continue;
                int heal = Math.Max(1, (int)((long)max * permille / 1000));
                int next = Math.Min(max, hp + heal);
                if (kind == ActorKind.Vehicle) _units.Health[i] = next; else _infantry.Health[i] = next;
            }
        }
    }

    /// <summary>True if the vehicle or soldier in slot <paramref name="i"/> is of a type that earns ranks (the <c>veteran</c> flag; default: armed).</summary>
    private bool CanRankAt(ActorKind kind, int i) => kind switch
    {
        ActorKind.Vehicle => RulesOf(_units.Player[i]).Vehicles[_units.TypeIndex[i]].CanRank,
        ActorKind.Infantry => RulesOf(_infantry.Player[i]).Infantry[_infantry.TypeIndex[i]].CanRank,
        _ => false,
    };
}
