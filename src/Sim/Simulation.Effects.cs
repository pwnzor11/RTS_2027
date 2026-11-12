// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Sim.Core;
using Rts.Sim.Numerics;
using Rts.Sim.Rules;
using Rts.Sim.World;

namespace Rts.Sim;

// Auras and death weapons (design 05 §4). OWNER: M3-06. State: Effects.*RofPermille / *SpeedPermille.
// Read sites (marked "// M3-06"): MoveMission.StatsOf (speed) and Simulation.Combat.Fire (reload).
//
// AURA RULES
//  * Every AuraIntervalTicks ticks (Tick % 10 == 0) every live vehicle/infantry slot is recomputed from scratch, so an actor that
//    leaves the range (or whose source died) loses the bonus at the next boundary.
//  * A source is a live, surface-layer vehicle or infantry with AuraRadius > 0. It affects friendly actors (same player or ally)
//    within AuraRadius cells (Euclidean, integer: dx^2 + dy^2 <= (radius * 256)^2 on lepton positions) whose type matches
//    AuraTargets (a type id, "class:vehicle" or "class:infantry"). A source does not affect itself.
//  * STACKING (deterministic, independent of slot order): auras do not multiply. Per stat, the best single source wins: for rate of
//    fire the LOWEST AuraRofPermille below 1000 (faster reload), for speed the HIGHEST AuraSpeedPermille above 1000. A value of
//    1000 from a source contributes nothing. A debuff (rof above 1000 / speed below 1000) applies only when no buff of that stat
//    reaches the actor, and then the worst debuff wins.
// M6-06 ADDITIONS (same recompute, same 10-tick boundary):
//  * REPAIR (AuraRepairPermille, vehicle or infantry source): heals friendly VEHICLES (packed rigs are vehicles) within the source's
//    repair radius by max(1, MaxHealth * permille / 1000), never above MaxHealth, never itself. Best single source wins (no stacking).
//    It ignores AuraTargets. The upgrade flag RepairsVehiclesAtRange (owner of the source) widens the radius by RepairRangeBonusCells;
//    an Engineer-flagged infantry source with no AuraRepairPermille of its own then also gains RangeRepairPermille over
//    RangeRepairRadiusCells (so a Tinker works even if its data has no aura).
//  * TOW (VehicleType.TowBonusPermille): a speed multiplier given ONLY to friendly packed rigs within AuraRadius, folded into the speed
//    stat with the ordinary rule (above 1000 = buff, below = debuff).
//  * STORM (Simulation.StormSpeedPermilleAt, M6-07): every vehicle gets the storm's slow as one more speed debuff candidate. Worst
//    debuff wins; ANY buff (aura or tow) beats the storm slow, i.e. a Tow Truck keeps a rig at its tow speed inside a storm.
// ASSUMED (M3-06): the stacking rule above, the exclusion of self, allies counting as friendly, and underground sources being inert.
public sealed partial class Simulation
{
    /// <summary>Ticks between aura recomputations.</summary>
    internal const int AuraIntervalTicks = 10;

    private struct AuraSource
    {
        public ActorKind Kind;
        public int Index;
        public byte Owner;
        public LeptonCoord Pos;
        public long RadiusSq;
        public int Rof, Speed;
        public int Tow, Repair;
        public long RepairRadiusSq;
        public IReadOnlyList<string> Targets;
    }

    /// <summary>Repair radius added by the RepairsVehiclesAtRange upgrade, in cells.</summary>
    internal const int RepairRangeBonusCells = 2;
    /// <summary>Repair permille an Engineer infantry gains from RepairsVehiclesAtRange when its type has no aura of its own.</summary>
    internal const int RangeRepairPermille = 15;
    /// <summary>Repair radius of that fallback aura, in cells.</summary>
    internal const int RangeRepairRadiusCells = 4;

    /// <summary>Folds a storm slow into the aura speed result. <paramref name="bestBuff"/> and <paramref name="worstDebuff"/> are 0 when
    /// absent. Any buff wins outright; otherwise the lower of the worst debuff and the storm slow (1000 = no storm) is used.</summary>
    internal static int FoldSpeed(int bestBuff, int worstDebuff, int stormPermille)
    {
        if (bestBuff != 0) return bestBuff;
        if (stormPermille > 0 && stormPermille < 1000 && (worstDebuff == 0 || stormPermille < worstDebuff)) return stormPermille;
        return worstDebuff;
    }

    /// <summary>Per tick (first in RunEffects): keep passengers on their transport, then every 10 ticks recompute aura multipliers.</summary>
    private void RunAuras()
    {
        SyncPassengers();   // M3-06: Simulation.Transport.cs (hooked here because Simulation.M3.cs is not ours to edit)
        if (Tick % AuraIntervalTicks != 0) return;
        var sources = new List<AuraSource>();   // local on purpose: a field would need a hash-coverage allow-list entry
        CollectAuraSources(sources, ActorKind.Vehicle);
        CollectAuraSources(sources, ActorKind.Infantry);
        RecomputeAuras(sources, ActorKind.Vehicle);
        RecomputeAuras(sources, ActorKind.Infantry);
    }

    private void CollectAuraSources(List<AuraSource> sources, ActorKind kind)
    {
        MobileStore store = StoreOf(kind)!;
        for (int i = 1; i < store.Capacity; i++)
        {
            if (!store.Slots.IsIndexAlive(i) || store.Health[i] <= 0 || _tunnels.LayerOf(kind, i) != ActorLayer.Surface) continue;
            int radius, rof, speed, tow = 0, repair;
            bool engineer;
            IReadOnlyList<string> targets;
            if (kind == ActorKind.Vehicle)
            {
                VehicleType t = RulesOf(store.Player[i]).Vehicles[store.TypeIndex[i]];
                (radius, rof, speed, targets, tow, repair, engineer) = (t.AuraRadius, t.AuraRofPermille, t.AuraSpeedPermille, t.AuraTargets, t.TowBonusPermille, t.AuraRepairPermille, false);
            }
            else
            {
                InfantryType t = RulesOf(store.Player[i]).Infantry[store.TypeIndex[i]];
                (radius, rof, speed, targets, repair, engineer) = (t.AuraRadius, t.AuraRofPermille, t.AuraSpeedPermille, t.AuraTargets, t.AuraRepairPermille, t.Engineer);
            }
            if (targets.Count == 0 || (rof == 1000 && speed == 1000) || radius <= 0) { rof = 1000; speed = 1000; }   // no rof/speed aura
            if (tow == 1000 || radius <= 0) tow = 0;
            int repairRadius = radius;
            if (repair > 0 || engineer)
            {
                if (HasUpgradeFlag(store.Player[i], u => u.RepairsVehiclesAtRange))
                {
                    if (repair > 0) repairRadius = radius + RepairRangeBonusCells;
                    else if (engineer) { repair = RangeRepairPermille; repairRadius = Math.Max(radius, RangeRepairRadiusCells); }
                }
            }
            if (repair <= 0 || repairRadius <= 0) repair = 0;
            if ((rof == 1000 && speed == 1000) && tow == 0 && repair == 0) continue;
            long r = (long)radius * SimConstants.LeptonsPerCell;
            long rr = (long)repairRadius * SimConstants.LeptonsPerCell;
            sources.Add(new AuraSource
            {
                Kind = kind, Index = i, Owner = store.Player[i], Pos = store.Position[i], RadiusSq = r * r,
                Rof = rof, Speed = speed, Targets = targets, Tow = tow, Repair = repair, RepairRadiusSq = rr * rr,
            });
        }
    }

    private void RecomputeAuras(List<AuraSource> sources, ActorKind kind)
    {
        MobileStore store = StoreOf(kind)!;
        int[] rofArr = kind == ActorKind.Vehicle ? _effects.VehicleRofPermille : _effects.InfantryRofPermille;
        int[] speedArr = kind == ActorKind.Vehicle ? _effects.VehicleSpeedPermille : _effects.InfantrySpeedPermille;
        for (int i = 1; i < store.Capacity; i++)
        {
            rofArr[i] = 0; speedArr[i] = 0;
            if (!store.Slots.IsIndexAlive(i) || store.Health[i] <= 0) continue;
            if (sources.Count == 0)
            {
                if (kind == ActorKind.Vehicle) speedArr[i] = FoldSpeed(0, 0, StormSpeedPermilleAt(store.Player[i], store.Position[i]));
                continue;
            }
            string typeId = kind == ActorKind.Vehicle ? RulesOf(store.Player[i]).Vehicles[store.TypeIndex[i]].Id
                                                       : RulesOf(store.Player[i]).Infantry[store.TypeIndex[i]].Id;
            int bestRofBuff = 0, worstRofDebuff = 0, bestSpeedBuff = 0, worstSpeedDebuff = 0;   // 0 = none
            LeptonCoord pos = store.Position[i];
            int bestRepair = 0;
            bool packedRig = kind == ActorKind.Vehicle && _rigs.IsPackedRig(i);
            for (int s = 0; s < sources.Count; s++)
            {
                AuraSource a = sources[s];
                if (a.Kind == kind && a.Index == i) continue;
                if (!IsFriendly(a.Owner, store.Player[i])) continue;
                long dx = pos.X - a.Pos.X, dy = pos.Y - a.Pos.Y;
                long d2 = dx * dx + dy * dy;
                if (kind == ActorKind.Vehicle && a.Repair > bestRepair && d2 <= a.RepairRadiusSq) bestRepair = a.Repair;
                if (packedRig && a.Tow != 0 && d2 <= a.RadiusSq)
                {
                    if (a.Tow > 1000) { if (a.Tow > bestSpeedBuff) bestSpeedBuff = a.Tow; }
                    else if (worstSpeedDebuff == 0 || a.Tow < worstSpeedDebuff) worstSpeedDebuff = a.Tow;
                }
                if ((a.Rof == 1000 && a.Speed == 1000) || !AuraTargetMatches(a.Targets, kind, typeId) || d2 > a.RadiusSq) continue;
                if (a.Rof < 1000) { if (bestRofBuff == 0 || a.Rof < bestRofBuff) bestRofBuff = a.Rof; }
                else if (a.Rof > 1000) { if (a.Rof > worstRofDebuff) worstRofDebuff = a.Rof; }
                if (a.Speed > 1000) { if (a.Speed > bestSpeedBuff) bestSpeedBuff = a.Speed; }
                else if (a.Speed < 1000) { if (worstSpeedDebuff == 0 || a.Speed < worstSpeedDebuff) worstSpeedDebuff = a.Speed; }
            }
            rofArr[i] = bestRofBuff != 0 ? bestRofBuff : worstRofDebuff;
            int storm = kind == ActorKind.Vehicle ? StormSpeedPermilleAt(store.Player[i], pos) : 1000;
            speedArr[i] = FoldSpeed(bestSpeedBuff, worstSpeedDebuff, storm);
            if (bestRepair > 0)
            {
                int max = RulesOf(store.Player[i]).Vehicles[store.TypeIndex[i]].MaxHealth;
                if (store.Health[i] < max) store.Health[i] = Math.Min(max, store.Health[i] + Math.Max(1, (int)((long)max * bestRepair / 1000)));
            }
        }
    }

    private static bool AuraTargetMatches(IReadOnlyList<string> targets, ActorKind kind, string typeId)
    {
        string cls = kind == ActorKind.Vehicle ? "class:vehicle" : "class:infantry";
        for (int k = 0; k < targets.Count; k++)
            if (string.Equals(targets[k], typeId, StringComparison.Ordinal) || string.Equals(targets[k], cls, StringComparison.Ordinal)) return true;
        return false;
    }

    /// <summary>A mobile actor is dying (RunCleanup, before removal): fire its DeathWeapon at its cell.</summary>
    private void OnDeathWeapon(ActorKind kind, int i)
    {
        if (kind != ActorKind.Vehicle && kind != ActorKind.Infantry) return;
        SimRules rules = RulesOf(PlayerAt(kind, i));
        int w = kind == ActorKind.Vehicle ? rules.Vehicles[_units.TypeIndex[i]].DeathWeapon : rules.Infantry[_infantry.TypeIndex[i]].DeathWeapon;
        if (w < 0 || rules.Weapons == null || w >= rules.Weapons.Count) return;
        WeaponType weapon = rules.Weapons[w];
        // Not a shot at an actor: detonate at the actor's own position as a splash (instant: false, so a splash-0 warhead hurts one
        // actor of the cell). The hits are applied at once because RunCleanup runs after RunCombat's ApplyPendingDamage; leaving them
        // queued would lose them if the game were saved between ticks. Victims that die from it are removed this tick or the next.
        Detonate(PlayerAt(kind, i), weapon.Damage, weapon.Warhead, PositionAt(kind, i), default, false, default, weapon.Targets);
        ApplyPendingDamage();
    }
}
