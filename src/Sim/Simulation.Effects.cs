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
        public IReadOnlyList<string> Targets;
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
            int radius, rof, speed;
            IReadOnlyList<string> targets;
            if (kind == ActorKind.Vehicle)
            {
                VehicleType t = RulesOf(store.Player[i]).Vehicles[store.TypeIndex[i]];
                (radius, rof, speed, targets) = (t.AuraRadius, t.AuraRofPermille, t.AuraSpeedPermille, t.AuraTargets);
            }
            else
            {
                InfantryType t = RulesOf(store.Player[i]).Infantry[store.TypeIndex[i]];
                (radius, rof, speed, targets) = (t.AuraRadius, t.AuraRofPermille, t.AuraSpeedPermille, t.AuraTargets);
            }
            if (radius <= 0 || targets.Count == 0 || (rof == 1000 && speed == 1000)) continue;
            long r = (long)radius * SimConstants.LeptonsPerCell;
            sources.Add(new AuraSource
            {
                Kind = kind, Index = i, Owner = store.Player[i], Pos = store.Position[i], RadiusSq = r * r,
                Rof = rof, Speed = speed, Targets = targets,
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
            if (sources.Count == 0 || !store.Slots.IsIndexAlive(i) || store.Health[i] <= 0) continue;
            string typeId = kind == ActorKind.Vehicle ? RulesOf(store.Player[i]).Vehicles[store.TypeIndex[i]].Id
                                                       : RulesOf(store.Player[i]).Infantry[store.TypeIndex[i]].Id;
            int bestRofBuff = 0, worstRofDebuff = 0, bestSpeedBuff = 0, worstSpeedDebuff = 0;   // 0 = none
            LeptonCoord pos = store.Position[i];
            for (int s = 0; s < sources.Count; s++)
            {
                AuraSource a = sources[s];
                if (a.Kind == kind && a.Index == i) continue;
                if (!IsFriendly(a.Owner, store.Player[i]) || !AuraTargetMatches(a.Targets, kind, typeId)) continue;
                long dx = pos.X - a.Pos.X, dy = pos.Y - a.Pos.Y;
                if (dx * dx + dy * dy > a.RadiusSq) continue;
                if (a.Rof < 1000) { if (bestRofBuff == 0 || a.Rof < bestRofBuff) bestRofBuff = a.Rof; }
                else if (a.Rof > 1000) { if (a.Rof > worstRofDebuff) worstRofDebuff = a.Rof; }
                if (a.Speed > 1000) { if (a.Speed > bestSpeedBuff) bestSpeedBuff = a.Speed; }
                else if (a.Speed < 1000) { if (worstSpeedDebuff == 0 || a.Speed < worstSpeedDebuff) worstSpeedDebuff = a.Speed; }
            }
            rofArr[i] = bestRofBuff != 0 ? bestRofBuff : worstRofDebuff;
            speedArr[i] = bestSpeedBuff != 0 ? bestSpeedBuff : worstSpeedDebuff;
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
        Detonate(PlayerAt(kind, i), weapon.Damage, weapon.Warhead, PositionAt(kind, i), default, false);
        ApplyPendingDamage();
    }
}
