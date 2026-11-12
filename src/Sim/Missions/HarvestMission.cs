// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Sim.Core;
using Rts.Sim.Economy;
using Rts.Sim.Movement;
using Rts.Sim.Numerics;
using Rts.Sim.Rules;
using Rts.Sim.World;

namespace Rts.Sim.Missions;

/// <summary>
/// Harvest and Return mission handlers. OWNER: M2-07 (economy). State lives in <see cref="EconomyState"/> (cargo, the
/// field being worked, the dock dance); the missions only move the harvester through the normal movement system
/// (<see cref="MoveMission.StepToward"/>), so blockage, occupancy and path rules are unchanged.
/// <para>Harvest: find the richest resource cell on the nearest ring (cheb. distance 0..<see cref="SearchRadius"/>) around the
/// last field cell (or the harvester when it has none yet; then around the harvester if that ring search finds nothing), drive
/// there, load one unit every HarvestTicksPerUnit (density -1, overlay cleared at 0), seek the next cell when it runs out, and
/// switch to Return when full or when nothing is left in range while carrying cargo (empty and nothing found: retry every
/// <see cref="RetryTicks"/> ticks, so regrowth is picked up). When the near search (<see cref="SearchRadius"/>) finds nothing and
/// the harvester is empty, the search widens ring by ring (<see cref="RingsPerTick"/> rings per tick, cursor in
/// EconomyState.SearchRing) up to the whole map, so the nearest field by Chebyshev distance wins wherever it is. A harvester with HarvestCapacity > 0 that has never been given a
/// mission starts Harvest on the tick after it appears (Simulation.RunEconomy, no hook in OnActorSpawned).</para>
/// <para>Return: pick the nearest own refinery (squared cell distance harvester to dock cell, ties by slot order), drive to within
/// <see cref="QueueRadius"/> cells of its dock cell, take a queue ticket and wait there until the dock is free and no
/// lower ticket waits, enter the dock cell (EconomyState.DockOccupant holds the claim), face the building, unload one unit
/// every UnloadTicksPerUnit into Stored up to the storage cap (the rest is lost), then Harvest again. A claim whose holder has
/// left (other mission, dead) counts as free. A refinery that disappears sends the harvester to the next one, or Guard if none.</para>
/// <para>Commands: Harvest (Subject = vehicle, Cell = optional field to work near; (0,0) = none) and Return (Subject).</para>
/// </summary>
internal static class HarvestMission
{
    /// <summary>Largest ring (Chebyshev radius in cells) of the near search, done in one tick.</summary>
    public const int SearchRadius = 16;
    /// <summary>Rings scanned per tick by the widening search that follows an empty near search (up to the whole map).</summary>
    public const int RingsPerTick = 8;
    /// <summary>Ticks between searches while nothing is left to harvest.</summary>
    public const int RetryTicks = 40;
    /// <summary>Distance (cells) from the dock cell at which a returning harvester joins the queue.</summary>
    public const int QueueRadius = 3;
    /// <summary>S20 (SP-04): Chebyshev radius around an unreachable goal whose cells are skipped.</summary>
    public const int SkipRadius = 4;
    /// <summary>S20 (SP-04): ticks an unreachable field stays skipped for the harvester that failed to reach it.</summary>
    public const int SkipTicks = 1200;
    /// <summary>Ticks between dock-free checks while queued.</summary>
    public const int QueuePollTicks = 3;

    // ---- Harvest ------------------------------------------------------------------------------

    public static int HandleHarvest(Simulation sim, MobileStore store, ActorKind kind, int i)
    {
        if (store.Health[i] <= 0) return 1;
        if (kind != ActorKind.Vehicle) return ToGuard(sim, store, kind, i);
        SimRules rules = sim.RulesOf(store.Player[i]);
        int capacity = rules.Vehicles[store.TypeIndex[i]].HarvestCapacity;
        if (capacity <= 0) return ToGuard(sim, store, kind, i);
        EconomyState eco = sim.Economy;
        EntityId me = store.Slots.IdOf(i);
        eco.Touch(i, me);
        if (store.Status[i] == MissionStatus.Init)
        {
            store.Status[i] = MissionStatus.Waiting;
            eco.HarvestState[i] = (byte)HarvestStep.Seek;
            eco.DockState[i] = 0; eco.DockTarget[i] = default; eco.Ticket[i] = 0; eco.Timer[i] = 0; eco.BadCell[i] = default; eco.SearchRing[i] = 0;
        }
        if (eco.Cargo[i] >= capacity) return Assign(sim, store, kind, i, MissionKind.Return);

        CellCoord cur = store.Position[i].ToCell();
        EconomyRules er = rules.Economy;
        switch ((HarvestStep)eco.HarvestState[i])
        {
            case HarvestStep.Seek:
            {
                Locomotion loco = rules.Vehicles[store.TypeIndex[i]].Locomotion;
                CellCoord last = eco.LastFieldCell[i];
                CellCoord goal;
                int ring = eco.SearchRing[i];
                bool found;
                if (ring == 0)
                {
                    found = FindRich(sim, i, me, last != default ? last : cur, loco, 0, SearchRadius, out goal)
                        || (last != default && FindRich(sim, i, me, cur, loco, 0, SearchRadius, out goal));
                    if (!found && eco.Cargo[i] == 0) ring = SearchRadius + 1;   // nothing near and nothing to deliver: widen
                }
                else
                {
                    // widening search: a few rings per tick, nearest ring first, resuming next tick
                    int maxRing = Math.Max(sim.Setup.Map.Width, sim.Setup.Map.Height);
                    int to = Math.Min(maxRing, ring + RingsPerTick - 1);
                    found = FindRich(sim, i, me, last != default ? last : cur, loco, ring, to, out goal);
                    ring = found ? 0 : to + 1;
                    if (!found && to >= maxRing)
                    {
                        eco.SearchRing[i] = 0;      // the whole map is empty: sleep, then start over (regrowth is picked up)
                        eco.BadCell[i] = default;
                        return RetryTicks;
                    }
                }
                eco.SearchRing[i] = found ? 0 : ring;
                if (found)
                {
                    eco.Goal[i] = goal;
                    eco.HarvestState[i] = (byte)HarvestStep.Go;
                    return 1;
                }
                if (ring != 0) return 1;
                eco.BadCell[i] = default;
                if (eco.Cargo[i] > 0) return Assign(sim, store, kind, i, MissionKind.Return);
                return RetryTicks;
            }
            case HarvestStep.Go:
            {
                CellCoord goal = eco.Goal[i];
                if (!IsResource(sim, goal)) { eco.HarvestState[i] = (byte)HarvestStep.Seek; return 1; }
                if (!store.NextCellReserved[i] && cur == goal)
                {
                    eco.HarvestState[i] = (byte)HarvestStep.Load;
                    eco.LastFieldCell[i] = cur;
                    eco.Timer[i] = sim.Tick + Math.Max(1, er.HarvestTicksPerUnit);
                    return 1;
                }
                EntityId held = sim.Map.VehicleAt(goal);
                if (!held.IsNone && held != me && !store.NextCellReserved[i] && Occupancy.Chebyshev(cur, goal) <= 1)
                {
                    eco.HarvestState[i] = (byte)HarvestStep.Seek;   // somebody else took the spot
                    return 1;
                }
                int sleep = MoveMission.StepToward(sim, store, kind, i, goal);
                if (sleep >= MoveMission.WaitTicks) { eco.BadCell[i] = goal; eco.SkipCell[i] = goal; eco.SkipUntil[i] = sim.Tick + SkipTicks; eco.HarvestState[i] = (byte)HarvestStep.Seek; }
                return sleep;
            }
            default:
            {
                if (cur != eco.Goal[i] || store.NextCellReserved[i]) { eco.HarvestState[i] = (byte)HarvestStep.Go; return 1; }
                if (!IsResource(sim, cur)) { eco.LastFieldCell[i] = cur; eco.HarvestState[i] = (byte)HarvestStep.Seek; return 1; }
                if (sim.Tick < eco.Timer[i]) return eco.Timer[i] - sim.Tick;
                OverlayKind overlay = sim.Map.OverlayAt(cur);
                int density = sim.Map.DensityAt(cur) - 1;
                eco.Cargo[i]++;
                eco.CargoValue[i] += UnitValue(er, overlay);
                sim.Map.SetOverlay(cur, density <= 0 ? OverlayKind.None : overlay, (byte)Math.Max(0, density));
                eco.LastFieldCell[i] = cur;
                int interval = Math.Max(1, er.HarvestTicksPerUnit);
                eco.Timer[i] = sim.Tick + interval;
                if (eco.Cargo[i] >= capacity) return Assign(sim, store, kind, i, MissionKind.Return);
                if (density <= 0) { eco.HarvestState[i] = (byte)HarvestStep.Seek; return 1; }
                return interval;
            }
        }
    }

    /// <summary>True for a map cell holding harvestable resource.</summary>
    internal static bool IsResource(Simulation sim, CellCoord c)
    {
        if (!sim.Setup.Map.Contains(c)) return false;
        OverlayKind o = sim.Map.OverlayAt(c);
        return (o == OverlayKind.Ashfall || o == OverlayKind.Pitchblende) && sim.Map.DensityAt(c) > 0;
    }

    private static int UnitValue(EconomyRules er, OverlayKind o) => o == OverlayKind.Pitchblende ? er.CreditsPitchblende : er.CreditsAshfall;

    /// <summary>Nearest ring (<paramref name="fromRing"/>..<paramref name="toRing"/>) around <paramref name="center"/> that has a usable resource cell; the richest
    /// (density times credits per unit) wins. M4-16: ties are broken without a preferred direction, so point-mirrored harvesters pick
    /// mirrored cells: nearer to the centre (squared), then nearer to the harvester (squared), then <see cref="TieKey"/>, then scan order
    /// (only left for cells the key cannot tell apart; a row-major first-found tie-break favoured the top-left start).</summary>
    private static bool FindRich(Simulation sim, int self, EntityId me, CellCoord center, Locomotion loco, int fromRing, int toRing, out CellCoord best)
    {
        best = default;
        EconomyRules er = sim.RulesOf(sim.Units.Player[self]).Economy;
        CellCoord at = sim.Units.Position[self].ToCell();
        bool skipping = sim.Economy.SkipUntil[self] > sim.Tick;
        CellCoord skipCell = sim.Economy.SkipCell[self];
        for (int r = fromRing; r <= toRing; r++)
        {
            long bestValue = -1, bestD2c = 0, bestD2h = 0, bestKey = 0;
            for (int y = center.Y - r; y <= center.Y + r; y++)
            {
                if (y < 0 || y >= sim.Setup.Map.Height) continue;
                bool edgeRow = y == center.Y - r || y == center.Y + r;
                int step = edgeRow ? 1 : 2 * r;
                for (int x = center.X - r; x <= center.X + r; x += step)
                {
                    var c = new CellCoord(x, y);
                    if (!IsResource(sim, c) || c == sim.Economy.BadCell[self]) continue;
                    if (skipping && Occupancy.Chebyshev(c, skipCell) <= SkipRadius) continue;
                    long value = (long)sim.Map.DensityAt(c) * UnitValue(er, sim.Map.OverlayAt(c));
                    if (value < bestValue) continue;
                    long d2c = Sq(c.X - center.X) + Sq(c.Y - center.Y), d2h = Sq(c.X - at.X) + Sq(c.Y - at.Y);
                    long key = TieKey(c, center, at, sim.Units.Facing[self]);
                    if (value == bestValue && (d2c > bestD2c || (d2c == bestD2c && (d2h > bestD2h || (d2h == bestD2h && key >= bestKey)))))
                        continue;   // not better on the tie-break (equal keys keep the first: only reachable for the same cell offset)
                    if (!Occupancy.TerrainOk(sim, c, loco)) continue;
                    EntityId held = sim.Map.VehicleAt(c);
                    if (!held.IsNone && held != me) continue;
                    if (ClaimedByOther(sim, self, c)) continue;
                    bestValue = value; best = c; bestD2c = d2c; bestD2h = d2h; bestKey = key;
                }
            }
            if (bestValue >= 0) return true;
        }
        return false;
    }

    private static long Sq(int v) => (long)v * v;

    /// <summary>M4-16: orientation-free tie key for equal-value cells (like PathService.LineKey). Reference direction: centre -> harvester,
    /// or the harvester's facing when it stands on the centre. Key = distance of <paramref name="c"/> from that line (cross product,
    /// smaller first), then the side (left first), then ahead before behind. Rotating or mirroring the whole situation (facing included)
    /// maps the key onto itself.</summary>
    private static long TieKey(CellCoord c, CellCoord center, CellCoord at, byte facing)
    {
        long rx, ry;
        if (at == center) { rx = Facing.Sin(facing); ry = -Facing.Cos(facing); }
        else { rx = (at.X - center.X) * 256L; ry = (at.Y - center.Y) * 256L; }
        long ox = c.X - center.X, oy = c.Y - center.Y;
        long cross = ox * ry - oy * rx, dot = ox * rx + oy * ry;
        long line = cross >= 0 ? cross * 2 : -cross * 2 + 1;
        // |dot| <= 2 * 128 * 256 * 128 < 2^24 for any map; ahead (dot > 0) sorts first
        return (line << 26) + ((1L << 25) - dot);
    }

    private static bool ClaimedByOther(Simulation sim, int self, CellCoord c)
    {
        EconomyState eco = sim.Economy;
        UnitStore units = sim.Units;
        for (int j = 1; j < units.Capacity; j++)
        {
            if (j == self || !units.Slots.IsIndexAlive(j) || units.Mission[j] != MissionKind.Harvest || eco.Goal[j] != c) continue;
            if (eco.HarvestState[j] != (byte)HarvestStep.Seek) return true;
        }
        return false;
    }

    // ---- Return -------------------------------------------------------------------------------

    public static int HandleReturn(Simulation sim, MobileStore store, ActorKind kind, int i)
    {
        if (store.Health[i] <= 0) return 1;
        if (kind != ActorKind.Vehicle) return ToGuard(sim, store, kind, i);
        byte player = store.Player[i];
        SimRules rules = sim.RulesOf(player);
        if (rules.Vehicles[store.TypeIndex[i]].HarvestCapacity <= 0) return ToGuard(sim, store, kind, i);
        EconomyState eco = sim.Economy;
        EntityId me = store.Slots.IdOf(i);
        eco.Touch(i, me);
        if (store.Status[i] == MissionStatus.Init)
        {
            store.Status[i] = MissionStatus.Waiting;
            eco.DockState[i] = (byte)DockStep.Pick;
            eco.DockTarget[i] = default; eco.Ticket[i] = 0;
        }
        EconomyRules er = rules.Economy;
        CellCoord cur = store.Position[i].ToCell();
        var step = (DockStep)eco.DockState[i];

        if (step != DockStep.Pick)
        {
            EntityId rid = eco.DockTarget[i];
            if (!RefineryOk(sim, rid, player))
            {
                ReleaseDock(eco, rid, me);
                eco.DockTarget[i] = default; eco.Ticket[i] = 0;
                eco.DockState[i] = (byte)DockStep.Pick;
                return 1;
            }
        }

        switch (step)
        {
            case DockStep.Pick:
            {
                EntityId rid = PickRefinery(sim, player, cur);
                if (rid.IsNone) { eco.NoRefinery[i] = 1; return ToGuard(sim, store, kind, i); }   // D-205: resumes when a refinery is rebuilt
                eco.DockTarget[i] = rid; eco.Ticket[i] = 0;
                eco.DockState[i] = (byte)DockStep.Go;
                return 1;
            }
            case DockStep.Go:
            {
                CellCoord dock = DockCell(sim, eco.DockTarget[i]);
                if (Occupancy.Chebyshev(cur, dock) <= QueueRadius && !store.NextCellReserved[i])
                {
                    eco.Ticket[i] = eco.NextTicket++;
                    eco.DockState[i] = (byte)DockStep.Wait;
                    return 1;
                }
                return MoveMission.StepToward(sim, store, kind, i, dock);
            }
            case DockStep.Wait:
            {
                if (!TryClaim(sim, eco, i, me)) return QueuePollTicks;
                eco.DockOccupant[eco.DockTarget[i].Index] = me;
                eco.DockState[i] = (byte)DockStep.Enter;
                return 1;
            }
            case DockStep.Enter:
            {
                EntityId rid = eco.DockTarget[i];
                CellCoord dock = DockCell(sim, rid);
                if (eco.DockOccupant[rid.Index] != me) { eco.DockState[i] = (byte)DockStep.Wait; return 1; }
                if (!store.NextCellReserved[i] && cur == dock)
                {
                    LeptonCoord pos = store.Position[i];
                    LeptonCoord to = sim.PositionAt(ActorKind.Building, rid.Index);
                    store.Facing[i] = Facing.FromDelta(to.X - pos.X, to.Y - pos.Y);
                    eco.DockState[i] = (byte)DockStep.Unload;
                    eco.Timer[i] = sim.Tick + Math.Max(1, er.UnloadTicksPerUnit);
                    return 1;
                }
                int sleep = MoveMission.StepToward(sim, store, kind, i, dock);
                if (sleep >= MoveMission.WaitTicks)
                {
                    ReleaseDock(eco, rid, me);   // cannot get in from here: queue again
                    eco.DockState[i] = (byte)DockStep.Go;
                }
                return sleep;
            }
            default:   // Unload
            {
                EntityId rid = eco.DockTarget[i];
                if (eco.DockOccupant[rid.Index] != me) { eco.DockState[i] = (byte)DockStep.Go; return 1; }
                if (eco.Cargo[i] > 0)
                {
                    if (sim.Tick < eco.Timer[i]) return eco.Timer[i] - sim.Tick;
                    int pay = eco.CargoValue[i] / eco.Cargo[i];
                    eco.CargoValue[i] -= pay;
                    eco.Cargo[i]--;
                    pay = sim.HandicapIncome(player, pay);   // S19 (M13-15)
                    int space = Math.Max(0, sim.StorageCap(player) - sim.Players.Stored[player]);
                    sim.Players.Stored[player] += Math.Min(pay, space);   // the overflow is lost
                    sim.OnStatsHarvested(player, Math.Min(pay, space));   // M4-06
                    eco.Timer[i] = sim.Tick + Math.Max(1, er.UnloadTicksPerUnit);
                    if (eco.Cargo[i] > 0) return Math.Max(1, er.UnloadTicksPerUnit);
                }
                eco.CargoValue[i] = 0;
                ReleaseDock(eco, rid, me);
                eco.DockTarget[i] = default; eco.Ticket[i] = 0; eco.DockState[i] = 0;
                if (SalvageMission.TryResume(sim, store, kind, i)) return 1;   // M6-04: a salvager goes back to the nearest wreck
                return Assign(sim, store, kind, i, MissionKind.Harvest);
            }
        }
    }

    private static bool RefineryOk(Simulation sim, EntityId rid, byte player) =>
        !rid.IsNone && sim.Buildings.Slots.IsAlive(rid) && sim.Buildings.Health[rid.Index] > 0 && sim.Buildings.Player[rid.Index] == player
        && sim.RulesOf(player).Buildings[sim.Buildings.TypeIndex[rid.Index]].Refinery;

    private static CellCoord DockCell(Simulation sim, EntityId rid)
    {
        CellCoord tl = sim.Buildings.Cell[rid.Index];
        BuildingType bt = sim.RulesOf(sim.Buildings.Player[rid.Index]).Buildings[sim.Buildings.TypeIndex[rid.Index]];
        CellCoord d = sim.OrientedOffset(bt.Dock, bt.FootprintW, bt.FootprintH, tl);   // M4-16: mirrored for south-half refineries
        return new CellCoord(tl.X + d.X, tl.Y + d.Y);
    }

    internal static EntityId PickRefinery(Simulation sim, byte player, CellCoord from)
    {
        BuildingStore b = sim.Buildings;
        EntityId best = default;
        long bestDist = long.MaxValue;
        for (int k = 1; k < b.Capacity; k++)
        {
            if (!b.Slots.IsIndexAlive(k)) continue;
            EntityId id = b.Slots.IdOf(k);
            if (!RefineryOk(sim, id, player)) continue;
            CellCoord d = DockCell(sim, id);
            long dx = d.X - from.X, dy = d.Y - from.Y, dist = dx * dx + dy * dy;
            if (dist < bestDist) { bestDist = dist; best = id; }
        }
        return best;
    }

    private static void ReleaseDock(EconomyState eco, EntityId rid, EntityId me)
    {
        if (!rid.IsNone && eco.DockOccupant[rid.Index] == me) eco.DockOccupant[rid.Index] = default;
    }

    /// <summary>True if the dock is free (no holder, or the holder no longer docks there) and this harvester has the lowest ticket of those waiting.</summary>
    private static bool TryClaim(Simulation sim, EconomyState eco, int i, EntityId me)
    {
        EntityId rid = eco.DockTarget[i];
        EntityId occ = eco.DockOccupant[rid.Index];
        if (!occ.IsNone && occ != me && sim.Units.Slots.IsAlive(occ))
        {
            int o = occ.Index;
            var s = (DockStep)eco.DockState[o];
            if (sim.Units.Mission[o] == MissionKind.Return && eco.DockTarget[o] == rid && (s == DockStep.Enter || s == DockStep.Unload))
                return false;
        }
        UnitStore units = sim.Units;
        for (int j = 1; j < units.Capacity; j++)
        {
            if (j == i || !units.Slots.IsIndexAlive(j) || units.Mission[j] != MissionKind.Return) continue;
            if (eco.DockTarget[j] == rid && eco.DockState[j] == (byte)DockStep.Wait && eco.Ticket[j] < eco.Ticket[i]) return false;
        }
        return true;
    }

    // ---- helpers ------------------------------------------------------------------------------

    private static int Assign(Simulation sim, MobileStore store, ActorKind kind, int i, MissionKind mission)
    {
        sim.AssignMission(new ActorRef(kind, store.Slots.IdOf(i)), mission);
        return 1;
    }

    private static int ToGuard(Simulation sim, MobileStore store, ActorKind kind, int i) => Assign(sim, store, kind, i, MissionKind.Guard);
}
