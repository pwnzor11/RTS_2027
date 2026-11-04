// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Sim.Combat;
using Rts.Sim.Core;
using Rts.Sim.Missions;
using Rts.Sim.Movement;
using Rts.Sim.Numerics;
using Rts.Sim.Rules;
using Rts.Sim.World;

namespace Rts.Sim;

// Garrisons (design 08 §3). OWNER: M4-03 (M4-15 adds the neutral owner flip in wave 2). Call sites are in Simulation.M3.cs
// (ApplyEnter routing, OnM3BuildingRemoving) and Simulation.cs (Apply: CommandKind.Unload).
// State: Tunnels.InfantryLayer = Garrisoned, Tunnels.InfantryCarrier = the building (no new fields).
// ASSUMED (M4-03): while a soldier walks to a building (MissionKind.Capture), CombatEntry.AttackTarget holds ActorRef(Building, id).
// CaptureMission uses it to tell a garrison walk from a boarding walk (both store an EntityId in Target). ApplyBoard (M3) does not
// clear it, so a stale mark only misfires if a vehicle has the exact same id as the abandoned building (index and generation).
// ASSUMED (M4-03): an occupant sits at the building centre (Position) so vision, projectiles and snapshots work unchanged; it holds no cell.
// ASSUMED (M4-03): ejection puts soldiers on the nearest free cells in rings 1-2 around the footprint; the rest are lost (no death
// weapon, no destruction effect) like passengers of a dying transport.
public sealed partial class Simulation
{
    // ---- entering -----------------------------------------------------------------------------

    /// <summary>Enter routed to a garrisonable building (own, allied, or neutral and empty). Subject = own live mobile actor,
    /// already checked by ApplyEnter. Walks in with MissionKind.Capture, then GarrisonNow on arrival (at once when already adjacent).</summary>
    private void ApplyGarrison(Command c, ActorRef subject)
    {
        if (subject.Kind != ActorKind.Infantry) return;
        int i = subject.Id.Index;
        if (!CanGarrisonNow(i, c.Target)) return;
        if (GarrisonReach(i, c.Target.Index) <= RulesOf(c.Player).Garrison.EnterRangeCells) { GarrisonNow(i, c.Target); return; }
        _combat.At(ActorKind.Infantry, i).AttackTarget = new ActorRef(ActorKind.Building, c.Target);   // marks a garrison walk (see header)
        Queue(_infantry, i, MissionKind.Capture, default, c.Target);
    }

    /// <summary>True if infantry slot <paramref name="i"/> may board <paramref name="building"/> right now: it is a free, able soldier,
    /// the building is alive, may be entered by its owner and has room.</summary>
    private bool CanGarrisonNow(int i, EntityId building)
    {
        if (!_infantry.Slots.IsIndexAlive(i) || _infantry.Health[i] <= 0 || _tunnels.InfantryLayer[i] != ActorLayer.Surface) return false;
        byte player = _infantry.Player[i];
        if (!RulesOf(player).Infantry[_infantry.TypeIndex[i]].CanGarrison) return false;
        if (!_buildings.Slots.IsAlive(building) || _buildings.Health[building.Index] <= 0) return false;
        int b = building.Index;
        return IsGarrisonable(b) && CanGarrisonIn(player, b) && OccupantsIn(building) < BuildingTypeAt(b).Garrison;
    }

    /// <summary>Chebyshev distance in cells from the soldier's cell to the building footprint (0 = inside it).</summary>
    internal int GarrisonReach(int infantry, int building)
    {
        CellCoord cur = _infantry.SlotCell[infantry];
        BuildingType bt = BuildingTypeAt(building);
        CellCoord tl = _buildings.Cell[building];
        int dx = Math.Max(Math.Max(tl.X - cur.X, cur.X - (tl.X + bt.FootprintW - 1)), 0);
        int dy = Math.Max(Math.Max(tl.Y - cur.Y, cur.Y - (tl.Y + bt.FootprintH - 1)), 0);
        return Math.Max(dx, dy);
    }

    /// <summary>The soldier goes inside the building now. False (nothing changes) when it no longer may.</summary>
    internal bool GarrisonNow(int infantry, EntityId building)
    {
        if (!CanGarrisonNow(infantry, building)) return false;
        Occupancy.Release(this, ActorKind.Infantry, _infantry, infantry);
        MoveMission.DropRequest(this, _infantry, infantry);
        StopPassenger(infantry);   // clears missions, path and attack target
        _tunnels.InfantryLayer[infantry] = ActorLayer.Garrisoned;
        _tunnels.InfantryCarrier[infantry] = building;
        _infantry.Position[infantry] = BuildingCentre(building.Index);
        Queue(_infantry, infantry, MissionKind.Garrisoned, default, default);
        return true;
    }

    // ---- firing out ---------------------------------------------------------------------------

    /// <summary>One step of an occupant (MissionKind.Garrisoned): keep or acquire a target within weapon range plus the building's
    /// range bonus (measured from the nearest footprint cell) and fire with reload × garrison_rof_permille. Returns ticks to sleep.</summary>
    internal int RunGarrisoned(MobileStore store, int i)
    {
        EntityId carrier = _tunnels.InfantryCarrier[i];
        if (_tunnels.InfantryLayer[i] != ActorLayer.Garrisoned) { AssignMission(new ActorRef(ActorKind.Infantry, store.Slots.IdOf(i)), MissionKind.Guard); return 1; }
        if (!_buildings.Slots.IsAlive(carrier)) { RemoveMobileQuietly(ActorKind.Infantry, i); return 1; }   // cannot happen: removal ejects first
        int b = carrier.Index;
        BuildingType bt = BuildingTypeAt(b);
        ref CombatEntry e = ref _combat.At(ActorKind.Infantry, i);
        if (store.Status[i] == MissionStatus.Init)
        {
            store.Status[i] = MissionStatus.Waiting;
            e.AttackTarget = default; e.RetargetTick = 0; e.ScanTick = 0;
        }
        store.Position[i] = BuildingCentre(b);
        byte owner = store.Player[i];
        int weapon = WeaponOf(ActorKind.Infantry, i);
        if (weapon < 0) return MissionHandlers.GuardSleepTicks;
        WeaponType w = RulesOf(owner).Weapons[weapon];
        WeaponType reach = w with { Range = w.Range + bt.GarrisonRangeBonus };

        if (!e.AttackTarget.Id.IsNone)
        {
            bool keep = IsValidAutoTarget(owner, e.AttackTarget) && IsTargetVisible(owner, e.AttackTarget)
                && GarrisonSight(b, e.AttackTarget, out _, out int d) && d <= reach.Range && d >= reach.MinRange;
            if (!keep) e.AttackTarget = default;
        }
        if (e.AttackTarget.Id.IsNone && Tick >= e.ScanTick)
        {
            e.AttackTarget = AcquireFromFootprint(owner, b, reach);
            e.ScanTick = Tick + ScanIntervalTicks;
        }
        if (e.AttackTarget.Id.IsNone) return Math.Clamp(e.ScanTick - Tick, 1, MissionHandlers.GuardSleepTicks);
        if (e.ReloadTicks > 0) return 1;
        if (GarrisonSight(b, e.AttackTarget, out LeptonCoord tp, out _) && Aim(store, ActorKind.Infantry, i, tp)
            && Fire(ActorKind.Infantry, i, weapon, e.AttackTarget, tp))
            e.ReloadTicks = Math.Max(1, (int)((long)e.ReloadTicks * bt.GarrisonRofPermille / 1000));
        return 1;
    }

    /// <summary>Nearest footprint cell of building <paramref name="b"/> to the target: the target point and the distance in leptons.</summary>
    private bool GarrisonSight(int b, ActorRef target, out LeptonCoord point, out int distance)
    {
        point = default; distance = int.MaxValue;
        BuildingType bt = BuildingTypeAt(b);
        CellCoord tl = _buildings.Cell[b];
        long best = long.MaxValue;
        for (int y = 0; y < bt.FootprintH; y++)
            for (int x = 0; x < bt.FootprintW; x++)
            {
                LeptonCoord from = LeptonCoord.CenterOf(new CellCoord(tl.X + x, tl.Y + y));
                if (!TargetPoint(target, from, out LeptonCoord tp)) return false;
                long d2 = LeptonMath.DistanceSquared(from, tp);
                if (d2 >= best) continue;
                best = d2; point = tp; distance = LeptonMath.Distance(from, tp);
            }
        return best != long.MaxValue;
    }

    private ActorRef AcquireFromFootprint(byte owner, int b, WeaponType reach)
    {
        BuildingType bt = BuildingTypeAt(b);
        CellCoord tl = _buildings.Cell[b];
        ActorRef best = default;
        long bestD = long.MaxValue;
        for (int y = 0; y < bt.FootprintH; y++)
            for (int x = 0; x < bt.FootprintW; x++)
            {
                LeptonCoord from = LeptonCoord.CenterOf(new CellCoord(tl.X + x, tl.Y + y));
                ActorRef cand = AcquireTarget(owner, from, reach);
                if (cand.Id.IsNone || !TargetPoint(cand, from, out LeptonCoord tp)) continue;
                long d = LeptonMath.DistanceSquared(from, tp);
                if (d < bestD) { bestD = d; best = cand; }
            }
        return best;
    }

    // ---- unloading and ejection ---------------------------------------------------------------

    /// <summary>CommandKind.Unload: Subject = an own building (Arg low byte = Building) or an own transport vehicle (Arg low byte =
    /// Vehicle, alias of Deploy-unload). Soldiers that find no free cell stay inside.</summary>
    private void ApplyUnloadCommand(Command c)
    {
        switch ((ActorKind)(c.Arg & 0xFF))
        {
            case ActorKind.Building:
                if (_buildings.Slots.IsAlive(c.Subject) && _buildings.Player[c.Subject.Index] == c.Player && IsGarrisonable(c.Subject.Index))
                    EjectGarrison(c.Subject.Index, false, false);
                break;
            case ActorKind.Vehicle:
                if (_units.Slots.IsAlive(c.Subject) && _units.Player[c.Subject.Index] == c.Player
                    && RulesOf(c.Player).Vehicles[_units.TypeIndex[c.Subject.Index]].Passengers > 0) ApplyUnload(c);
                break;
        }
    }

    /// <summary>A building is being removed (destroyed or sold), before its cells are freed: eject the occupants with
    /// eject_damage_permille damage; those with no room are lost.</summary>
    private void OnGarrisonBuildingRemoving(int b) => EjectGarrison(b, true, true);

    /// <summary>Put the occupants of building slot <paramref name="b"/> on free cells in rings 1-2 around the footprint, in infantry slot
    /// order. <paramref name="damage"/>: each takes eject_damage_permille of its max health. <paramref name="loseRest"/>: soldiers with
    /// no room vanish (else they stay inside). Emits FxKind.GarrisonExit with the number ejected. Returns that number.</summary>
    internal int EjectGarrison(int b, bool damage, bool loseRest)
    {
        EntityId building = _buildings.Slots.IdOf(b);
        int ejected = 0;
        for (int i = 1; i < _infantry.Capacity; i++)
        {
            if (!_infantry.Slots.IsIndexAlive(i) || _tunnels.InfantryLayer[i] != ActorLayer.Garrisoned || _tunnels.InfantryCarrier[i] != building) continue;
            if (!FindGarrisonExitCell(b, out CellCoord cell, out int slot))
            {
                if (loseRest) { RemoveMobileQuietly(ActorKind.Infantry, i); continue; }
                break;   // the rest stay inside
            }
            _tunnels.InfantryLayer[i] = ActorLayer.Surface;
            _tunnels.InfantryCarrier[i] = default;
            _infantry.SubSlot[i] = (byte)slot;
            _infantry.SlotCell[i] = cell;
            _infantry.Position[i] = InfantryStore.SlotPosition(cell, slot);
            _map.SetInfantry(cell, slot, _infantry.Slots.IdOf(i));
            if (damage)
                _infantry.Health[i] -= (int)((long)RulesOf(_infantry.Player[i]).Infantry[_infantry.TypeIndex[i]].MaxHealth * Setup.Rules.Garrison.EjectDamagePermille / 1000);
            Queue(_infantry, i, MissionKind.Guard, default, default);
            ejected++;
        }
        if (ejected > 0) _fx.Add(new FxEvent(FxKind.GarrisonExit, BuildingCentre(b), ejected));
        return ejected;
    }

    /// <summary>First free cell around the footprint: ring 1 then ring 2 (Chebyshev distance to the footprint), row by row; a cell with
    /// no soldier is preferred over one that only has a free sub-slot (as <see cref="FindUnloadCell"/>).</summary>
    private bool FindGarrisonExitCell(int b, out CellCoord cell, out int slot)
    {
        BuildingType bt = BuildingTypeAt(b);
        CellCoord tl = _buildings.Cell[b];
        for (int pass = 0; pass < 2; pass++)
            for (int r = 1; r <= 2; r++)
                for (int y = tl.Y - r; y <= tl.Y + bt.FootprintH - 1 + r; y++)
                    for (int x = tl.X - r; x <= tl.X + bt.FootprintW - 1 + r; x++)
                    {
                        int dx = Math.Max(Math.Max(tl.X - x, x - (tl.X + bt.FootprintW - 1)), 0);
                        int dy = Math.Max(Math.Max(tl.Y - y, y - (tl.Y + bt.FootprintH - 1)), 0);
                        if (Math.Max(dx, dy) != r) continue;
                        var cc = new CellCoord(x, y);
                        if (!InMap(cc) || !Occupancy.FreeForInfantry(this, cc)) continue;
                        int free = _map.FreeInfantrySlot(cc);
                        if (pass == 0 && free != 0) continue;
                        cell = cc; slot = free;
                        return true;
                    }
        cell = default; slot = 0;
        return false;
    }
}
