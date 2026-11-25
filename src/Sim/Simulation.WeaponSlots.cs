// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Sim.Combat;
using Rts.Sim.Core;
using Rts.Sim.Missions;
using Rts.Sim.Numerics;
using Rts.Sim.Rules;
using Rts.Sim.World;

namespace Rts.Sim;

// S44 (M-04): second and third weapon slots of a vehicle (VehicleType.SecondaryWeapon / TertiaryWeapon; design 07 §5).
//
// The primary weapon keeps its whole existing pipeline (mission handlers, chase, aim). The extra slots run after the mission handlers in
// RunMissions: each has its own cooldown (CombatEntry.Reload2 / Reload3, counted down in RunCombat) and chooses its own best target, so
// both slots can fire in the same tick at different targets. Extra mounts are hull-fixed: they need no aiming and never move the unit.
// A unit without a second slot never enters this code, so games without such units are unchanged.
public sealed partial class Simulation
{
    /// <summary>Ticks between target scans of an extra weapon slot that is ready to fire (staggered by unit slot; derived from Tick, not state).</summary>
    internal const int SlotScanTicks = 5;

    private void ResetFiredThisTick()
    {
        if (_firedThisTick[0].Length != _units.Capacity) _firedThisTick[0] = new bool[_units.Capacity];
        else Array.Clear(_firedThisTick[0]);
        if (_firedThisTick[1].Length != _infantry.Capacity) _firedThisTick[1] = new bool[_infantry.Capacity];
        else Array.Clear(_firedThisTick[1]);
    }

    /// <summary>Weapon index of slot 1 (secondary) or 2 (tertiary) of a vehicle type, or -1.</summary>
    internal static int ExtraWeaponOf(VehicleType vt, int slot) => slot == 1 ? vt.SecondaryWeapon : vt.TertiaryWeapon;

    private void RunSecondaryWeapons()
    {
        for (int i = 1; i < _units.Capacity; i++)
        {
            if (!_units.Slots.IsIndexAlive(i) || _units.Health[i] <= 0) continue;
            SimRules r = RulesOf(_units.Player[i]);
            VehicleType vt = r.Vehicles[_units.TypeIndex[i]];
            if (vt.SecondaryWeapon < 0 && vt.TertiaryWeapon < 0) continue;
            if (_combat.StanceOf(ActorKind.Vehicle, i) == UnitStance.HoldFire) continue;
            for (int slot = 1; slot <= 2; slot++) FireExtraSlot(i, vt, r, slot);
        }
    }

    private void FireExtraSlot(int i, VehicleType vt, SimRules r, int slot)
    {
        int weapon = ExtraWeaponOf(vt, slot);
        if (weapon < 0 || r.Weapons == null || weapon >= r.Weapons.Count) return;
        ref CombatEntry e = ref _combat.At(ActorKind.Vehicle, i);
        if ((slot == 1 ? e.Reload2 : e.Reload3) > 0) return;
        if (_tunnels.VehicleLayer[i] != ActorLayer.Surface) return;   // riders, tunnel transit and diving units do not shoot (FireSlot repeats the rule)
        WeaponType w = r.Weapons[weapon];
        byte owner = _units.Player[i];
        LeptonCoord pos = CombatPositionAt(ActorKind.Vehicle, i);
        // The unit's own target first (an Attack order or a guard target this slot can hit), else the nearest valid one.
        ActorRef target = default;
        ActorRef own = e.AttackTarget;
        if (!own.Id.IsNone && own.Kind <= ActorKind.Building && WeaponCanTarget(w, own.Kind, own.Id.Index)
            && (_units.Mission[i] == MissionKind.Attack ? IsValidTarget(owner, own) : IsValidAutoTarget(owner, own) && IsTargetVisible(owner, own))
            && InRange(pos, own, w.Range, w.MinRange))
            target = own;
        if (target.Id.IsNone)
        {
            if ((Tick + i) % SlotScanTicks != 0) return;
            target = AcquireTarget(owner, pos, w);
        }
        if (target.Id.IsNone || !TargetPoint(target, pos, out LeptonCoord tp)) return;
        FireSlot(ActorKind.Vehicle, i, weapon, target, tp, slot);
    }
}
