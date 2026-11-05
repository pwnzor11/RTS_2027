// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Sim.Core;
using Rts.Sim.Rules;
using Rts.Sim.Veterancy;

namespace Rts.Sim;

// Generic veterancy (docs/design/14_veterancy.md, D-127). VET-1: the "veterancy" part and its accessor. VET-2: kill credit (the shooter
// ActorRef travels Fire -> ProjectileStore -> Detonate -> Hurt -> PendingHit; ApplyPendingDamage calls OnVeterancyKill). VET-3: rank and
// bonuses.
public sealed partial class Simulation
{
    /// <summary>The "veterancy" part (VET-1): kill value and last-combat tick per vehicle and infantry slot.</summary>
    public VeterancyState Veterancy => _veterancy;

    /// <summary>
    /// A hit took <paramref name="victim"/> from positive health to zero or below (called from ApplyPendingDamage). The shooter of the
    /// lethal hit earns the victim's cost x kill permille / 1000 as kill value when: veterancy is enabled, the shooter is a live vehicle or
    /// infantry soldier that is still the same actor (its slot and generation match: no credit once the slot was freed or reused), its
    /// type can rank, and the victim belongs to an Enemy of the hit's owner (the test OnLethalHit uses). Hits without a shooter (death
    /// weapons, superweapons, radiation, storms) never credit. The cost is the victim type's cost in the base rules (not an upgrade-changed
    /// or discounted price). Garrisoned soldiers fire through Fire like any other, so they earn their own value.
    /// </summary>
    private void OnVeterancyKill(ActorRef shooter, ActorRef victim, byte source)
    {
        VeterancyRules vr = Setup.Rules.Veterancy;
        if (!vr.Enabled || shooter.Id.IsNone || shooter.Kind > ActorKind.Infantry) return;
        if (!SlotsOf(shooter.Kind).IsAlive(shooter.Id)) return;
        byte victimOwner = PlayerAt(victim.Kind, victim.Id.Index);
        if (source >= _players.Count || victimOwner >= _players.Count || RelationOf(source, victimOwner) != Relation.Enemy) return;
        int s = shooter.Id.Index;
        if (!CanRankAt(shooter.Kind, s)) return;
        int vi = victim.Id.Index;
        long add = victim.Kind switch
        {
            ActorKind.Vehicle => (long)Setup.Rules.Vehicles[_units.TypeIndex[vi]].Cost * vr.KillPermilleUnit / 1000,
            ActorKind.Infantry => (long)Setup.Rules.Infantry[_infantry.TypeIndex[vi]].Cost * vr.KillPermilleUnit / 1000,
            ActorKind.Building => (long)Setup.Rules.Buildings[_buildings.TypeIndex[vi]].Cost * vr.KillPermilleBuilding / 1000,
            _ => 0,
        };
        if (add <= 0) return;
        int[] kills = shooter.Kind == ActorKind.Vehicle ? _veterancy.VehicleKillValue : _veterancy.InfantryKillValue;
        kills[s] = (int)Math.Min((long)int.MaxValue, kills[s] + add);
    }

    /// <summary>True if the vehicle or soldier in slot <paramref name="i"/> is of a type that earns ranks (the <c>veteran</c> flag; default: armed).</summary>
    private bool CanRankAt(ActorKind kind, int i) => kind switch
    {
        ActorKind.Vehicle => RulesOf(_units.Player[i]).Vehicles[_units.TypeIndex[i]].CanRank,
        ActorKind.Infantry => RulesOf(_infantry.Player[i]).Infantry[_infantry.TypeIndex[i]].CanRank,
        _ => false,
    };
}
