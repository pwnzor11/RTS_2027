// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Sim.Combat;
using Rts.Sim.Core;
using Rts.Sim.Numerics;
using Rts.Sim.Rules;
using Rts.Sim.World;

namespace Rts.Sim.Missions;

/// <summary>
/// Guard, Attack and AttackMove mission handlers. OWNER: M1-09 (combat).
/// Per-actor state (attack target, guard point / AttackMove destination, scan and chase timers) lives in
/// <see cref="CombatState"/>; a mission's first handler call sees <see cref="MissionStatus.Init"/>.
/// </summary>
internal static class CombatMissions
{
    /// <summary>Ticks between target scans of a moving AttackMove unit.</summary>
    private const int AttackMoveScanTicks = 5;
    /// <summary>Ticks between re-targeting the chase cell on a moving target.</summary>
    private const int ChaseRetargetTicks = 10;

    public static int HandleGuard(Simulation sim, MobileStore store, ActorKind kind, int i)
    {
        ref CombatEntry e = ref sim.CombatData.At(kind, i);
        LeptonCoord pos = store.Position[i];
        if (store.Status[i] == MissionStatus.Init)
        {
            store.Status[i] = MissionStatus.Waiting;
            e.AttackTarget = default; e.RetargetTick = 0; e.ScanTick = 0;
            if (e.GuardPending) e.GuardPending = false;
            else e.GuardPoint = pos.ToCell();
        }

        byte owner = store.Player[i];
        int weapon = sim.WeaponOf(kind, i);
        UnitStance stance = sim.CombatData.StanceOf(kind, i);   // S19 (M13-15)
        if (weapon >= 0 && stance == UnitStance.HoldFire) e.AttackTarget = default;
        else if (weapon >= 0)
        {
            WeaponType w = sim.RulesOf(store.Player[i]).Weapons[weapon];
            if (!e.AttackTarget.Id.IsNone)
            {
                bool keep = sim.IsValidAutoTarget(owner, e.AttackTarget) && sim.IsTargetVisible(owner, e.AttackTarget);
                if (keep && sim.TargetPoint(e.AttackTarget, pos, out LeptonCoord tp))
                {
                    // Leash: chasing is allowed up to Sight cells from the guard point (a Defensive unit: DefensiveChaseCells);
                    // a target already in range needs no chase.
                    int leash = (stance == UnitStance.Defensive ? Simulation.DefensiveChaseCells : sim.SightOf(kind, i)) * SimConstants.LeptonsPerCell;
                    if (LeptonMath.Distance(pos, tp) > w.Range && LeptonMath.Distance(pos, LeptonCoord.CenterOf(e.GuardPoint)) > leash)
                        keep = false;
                }
                if (!keep) e.AttackTarget = default;
            }
            if (e.AttackTarget.Id.IsNone && sim.Tick >= e.ScanTick)
            {
                e.AttackTarget = AcquireFor(sim, owner, pos, kind, i, w, stance);
                e.ScanTick = sim.Tick + Simulation.ScanIntervalTicks;
                e.RetargetTick = 0;
            }
            if (!e.AttackTarget.Id.IsNone)
            {
                Engage(sim, store, kind, i, weapon, e.AttackTarget);
                return 1;
            }
        }

        if (pos.ToCell() != e.GuardPoint) return MoveMission.StepToward(sim, store, kind, i, e.GuardPoint);
        return MissionHandlers.GuardSleepTicks;
    }

    public static int HandleAttack(Simulation sim, MobileStore store, ActorKind kind, int i)
    {
        ref CombatEntry e = ref sim.CombatData.At(kind, i);
        if (store.Status[i] == MissionStatus.Init) { store.Status[i] = MissionStatus.Waiting; e.RetargetTick = 0; }
        int weapon = sim.WeaponOf(kind, i);
        if (weapon < 0 || !sim.IsValidTarget(store.Player[i], e.AttackTarget))
        {
            e.AttackTarget = default;
            ToGuard(sim, store, kind, i);
            return 1;
        }
        Engage(sim, store, kind, i, weapon, e.AttackTarget);
        return 1;
    }

    /// <summary>S19 (FAD-05): fire at the ground cell the mission was given (<c>Destination</c>), closing in when out of range.</summary>
    public static int HandleAttackGround(Simulation sim, MobileStore store, ActorKind kind, int i)
    {
        ref CombatEntry e = ref sim.CombatData.At(kind, i);
        if (store.Status[i] == MissionStatus.Init) { store.Status[i] = MissionStatus.Waiting; e.RetargetTick = 0; e.AttackTarget = default; }
        int weapon = sim.WeaponOf(kind, i);
        if (weapon < 0) { ToGuard(sim, store, kind, i); return 1; }
        WeaponType w = sim.RulesOf(store.Player[i]).Weapons[weapon];
        LeptonCoord pos = store.Position[i];
        LeptonCoord tp = store.Destination[i];
        if (sim.GroundEffectDone(store.Player[i], w, tp.ToCell())) { ToGuard(sim, store, kind, i); return 1; }
        int dist = LeptonMath.Distance(pos, tp);
        if (dist > w.Range)
        {
            if (sim.Tick >= e.RetargetTick) { e.ChaseCell = tp.ToCell(); e.RetargetTick = sim.Tick + ChaseRetargetTicks; }
            MoveMission.StepToward(sim, store, kind, i, e.ChaseCell);
            return 1;
        }
        if (dist < w.MinRange) return 1;
        if (sim.Aim(store, kind, i, tp) && e.ReloadTicks == 0) sim.Fire(kind, i, weapon, default, tp);
        return 1;
    }

    public static int HandleAttackMove(Simulation sim, MobileStore store, ActorKind kind, int i)
    {
        ref CombatEntry e = ref sim.CombatData.At(kind, i);
        if (store.Status[i] == MissionStatus.Init)
        {
            store.Status[i] = MissionStatus.Waiting;
            e.GuardPending = false;                      // GuardPoint holds the destination
            e.AttackTarget = default; e.RetargetTick = 0; e.ScanTick = 0;
        }
        byte owner = store.Player[i];
        int weapon = sim.WeaponOf(kind, i);
        UnitStance stance = sim.CombatData.StanceOf(kind, i);   // S19 (M13-15)
        if (weapon >= 0 && stance == UnitStance.HoldFire) e.AttackTarget = default;
        else if (weapon >= 0)
        {
            if (!e.AttackTarget.Id.IsNone && !(sim.IsValidAutoTarget(owner, e.AttackTarget) && sim.IsTargetVisible(owner, e.AttackTarget)))
                e.AttackTarget = default;
            if (stance == UnitStance.Defensive && !e.AttackTarget.Id.IsNone
                && !(sim.TargetPoint(e.AttackTarget, store.Position[i], out LeptonCoord at)
                    && LeptonMath.Distance(store.Position[i], at) <= sim.RulesOf(owner).Weapons[weapon].Range))
                e.AttackTarget = default;   // a defensive marcher fires at what is in range and never chases it
            if (e.AttackTarget.Id.IsNone && sim.Tick >= e.ScanTick)
            {
                e.AttackTarget = AcquireFor(sim, owner, store.Position[i], kind, i, sim.RulesOf(store.Player[i]).Weapons[weapon], stance);
                e.ScanTick = sim.Tick + AttackMoveScanTicks;
                e.RetargetTick = 0;
            }
            if (!e.AttackTarget.Id.IsNone)
            {
                Engage(sim, store, kind, i, weapon, e.AttackTarget);
                return 1;
            }
        }
        CellCoord here = store.Position[i].ToCell();
        if (here == e.GuardPoint)
        {
            e.GuardPending = true;                       // guard where we arrived
            ToGuard(sim, store, kind, i);
            return 1;
        }
        // S21 (D-219): next to a goal cell that something else holds (a parked scraper, a friend already guarding it) and blocked at least
        // once: the march is over, so guard here. The unit used to keep stepping toward the held cell for good, never guarding, so it
        // never chased anything outside its weapon range.
        if (LeptonMath.ChebyshevCells(here, e.GuardPoint) <= 1 && store.StuckCount[i] > 0)
        {
            e.GuardPoint = here;
            e.GuardPending = true;
            ToGuard(sim, store, kind, i);
            return 1;
        }
        return MoveMission.StepToward(sim, store, kind, i, e.GuardPoint);
    }

    /// <summary>
    /// S21 (D-219, owner): the target search of a guarding or attack-moving unit. Aggressive (the default) engages hostile vehicles and
    /// infantry anywhere in its sight (buildings only in weapon range) and the Guard leash walks it back to its post when they leave;
    /// Defensive keeps to its weapon range (melee units: their sight, M13-11 item 19).
    /// </summary>
    private static ActorRef AcquireFor(Simulation sim, byte owner, LeptonCoord pos, ActorKind kind, int i, WeaponType w, UnitStance stance)
    {
        int melee = AcquireReach(sim, kind, i, w);
        if (melee > 0) return sim.AcquireTarget(owner, pos, w, melee);
        if (stance == UnitStance.Aggressive)
            return sim.AcquireTarget(owner, pos, w, sim.SightOf(kind, i) * SimConstants.LeptonsPerCell, mobilesOnlyBeyondRange: true);
        return sim.AcquireTarget(owner, pos, w);
    }

    /// <summary>Weapons with a reach of one cell or less are melee (M13-11 item 19): their owner acquires anything in sight and closes in.
    /// Longer weapons keep acquiring within their own range. Returns 0 for no extra search range.</summary>
    private static int AcquireReach(Simulation sim, ActorKind kind, int i, WeaponType w) =>
        w.Range <= SimConstants.LeptonsPerCell ? sim.SightOf(kind, i) * SimConstants.LeptonsPerCell : 0;

    private static void ToGuard(Simulation sim, MobileStore store, ActorKind kind, int i) =>
        sim.AssignMission(new ActorRef(kind, store.Slots.IdOf(i)), MissionKind.Guard);

    /// <summary>One tick of fighting <paramref name="target"/>: close in if out of range, hold if inside the minimum range,
    /// otherwise aim the mount and fire when ready.</summary>
    private static void Engage(Simulation sim, MobileStore store, ActorKind kind, int i, int weapon, ActorRef target)
    {
        WeaponType w = sim.RulesOf(store.Player[i]).Weapons[weapon];
        LeptonCoord pos = store.Position[i];
        if (!sim.TargetPoint(target, pos, out LeptonCoord tp)) return;
        ref CombatEntry e = ref sim.CombatData.At(kind, i);
        int dist = LeptonMath.Distance(pos, tp);
        if (dist > w.Range)
        {
            if (sim.Tick >= e.RetargetTick)
            {
                // M13-05: a building never moves, so a chase cell already on its footprint is kept (a new cell restarts the route search;
                // the path service turns the footprint cell into the nearest free cell next to it).
                bool onFootprint = target.Kind == ActorKind.Building && sim.Map.BuildingAt(e.ChaseCell) == target.Id;
                if (!onFootprint) e.ChaseCell = tp.ToCell();
                e.RetargetTick = sim.Tick + ChaseRetargetTicks;
            }
            MoveMission.StepToward(sim, store, kind, i, e.ChaseCell);
            return;
        }
        if (dist < w.MinRange) return;
        if (sim.Aim(store, kind, i, tp) && e.ReloadTicks == 0) sim.Fire(kind, i, weapon, target, tp);
    }
}
