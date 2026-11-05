// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Sim.Core;
using Rts.Sim.Movement;
using Rts.Sim.Numerics;
using Rts.Sim.Pathing;
using Rts.Sim.Rules;
using Rts.Sim.World;

namespace Rts.Sim.Missions;

/// <summary>
/// Move and Stop mission handlers plus the path-following locomotor. OWNER: M1-08 (movement and locomotion).
/// <para>Route: a Move asks <c>sim.Paths</c> for a path (commands of one player to the same cell in one tick are batched into one
/// group request by Simulation.RunMovement) and walks it cell by cell. Vehicles turn in place until aligned (D-042), reserve
/// the next cell (see <see cref="Occupancy"/>) and drive to its centre; infantry turn instantly and walk to a free sub-slot of the next cell.
/// Speed = type Speed x SpeedPercent(cell being entered, locomotion) / 100, at least 1.</para>
/// <para>Blockage (next cell taken): building or impassable terrain gives a repath; an allied vehicle that is moving is waited for
/// (10 ticks, then repath); an allied idle actor is asked to scatter and waited for; an enemy vehicle is waited for 10 ticks
/// and then walked around; enemy infantry in front of a non-crusher is walked around at once; a Crusher kills enemy infantry
/// as it starts moving into their cell. Head-on deadlocks between allied movers are broken by the higher slot index sidestepping.
/// If the blocked cell is the final one the actor stops where it is.</para>
/// </summary>
internal static class MoveMission
{
    /// <summary>Ticks to wait for an occupied next cell before repathing / sidestepping.</summary>
    public const int WaitTicks = 10;
    /// <summary>Blocked-wait cycles without any progress after which a Move gives up and stops.</summary>
    public const int MaxStuck = 12;

    private readonly struct Stats
    {
        public readonly Locomotion Loco; public readonly int Speed; public readonly int TurnRate; public readonly bool Crusher;
        public Stats(Locomotion loco, int speed, int turnRate, bool crusher) { Loco = loco; Speed = speed; TurnRate = turnRate; Crusher = crusher; }
    }

    private struct Block
    {
        public bool Invalid, Enterable, Kill, AllyIdle, AllyMoving, EnemyVeh, EnemyInf, Mutual;
        public int MutualIndex;
        public readonly bool Hostile => EnemyVeh || EnemyInf;
    }

    private static Stats StatsOf(Simulation sim, ActorKind kind, MobileStore store, int i)
    {
        if (kind == ActorKind.Vehicle)
        {
            VehicleType t = sim.RulesOf(store.Player[i]).Vehicles[store.TypeIndex[i]];
            return new Stats(MoveClass.Of(t), (int)((long)t.Speed * sim.MoveSpeedPermille(kind, i) / 1000), t.TurnRate, t.Crusher);   // M3-06: aura speed
        }
        return new Stats(Locomotion.Foot, (int)((long)sim.RulesOf(store.Player[i]).Infantry[store.TypeIndex[i]].Speed * sim.MoveSpeedPermille(kind, i) / 1000), 0, false);   // M3-06: aura speed
    }

    // ---- handlers -----------------------------------------------------------------------------

    public static int HandleStop(Simulation sim, MobileStore store, ActorKind kind, int i)
    {
        ResetPath(sim, store, i);
        Occupancy.DropReservation(sim, kind, store, i);
        store.Destination[i] = store.Position[i];
        store.NextMission[i] = MissionKind.Guard;
        store.NextCell[i] = default;
        store.NextTarget[i] = default;
        return 1;
    }

    public static int HandleMove(Simulation sim, MobileStore store, ActorKind kind, int i)
    {
        if (store.Health[i] <= 0) return 1;
        if (AirMission.IsAir(sim, kind, store, i)) return AirMission.HandleMove(sim, store, i);   // M3-06
        if (store.Status[i] == MissionStatus.Init)
        {
            // A fresh Move forgets the old route (an unfinished step is still completed first). -1: a command waits for its
            // batched request, -2: a scatter preset the one-cell route.
            if (store.PathId[i] == -2) store.PathId[i] = 0;
            else if (store.PathId[i] != -1) ResetPath(sim, store, i);
            store.Status[i] = MissionStatus.Waiting;
        }
        if (store.PathId[i] == -1) return 1;
        CellCoord goal = store.Destination[i].ToCell();
        int sleep = Follow(sim, store, kind, i, goal, true, out bool arrived);
        return arrived ? Arrive(store, i) : sleep;
    }

    internal static int Arrive(MobileStore store, int i)
    {
        store.PathActive[i] = false;
        store.PathCells[i] = null;
        store.PathPos[i] = 0;
        store.PathId[i] = 0;
        store.BlockedTicks[i] = 0;
        store.StuckCount[i] = 0;
        store.NextMission[i] = MissionKind.Guard;
        store.NextCell[i] = default;
        store.NextTarget[i] = default;
        return 1;
    }

    /// <summary>
    /// CONTRACT for other missions (M1-09 Attack/AttackMove/Guard chase): advance actor <paramref name="i"/> one tick
    /// toward <paramref name="cell"/> using normal movement rules, WITHOUT finishing the current mission or queueing
    /// Guard on arrival. Returns ticks to sleep (≥1); returns 1 and does nothing once the actor stands in that cell.
    /// M1-08: follows a path (requested from sim.Paths, so the first call only waits for the answer) with occupancy,
    /// blockage handling and crushing. The route is kept while the same cell is passed every tick; passing a different
    /// cell starts a new route, so callers chasing a moving target should hand in a stable cell. An unreachable cell
    /// makes the call return 10 (retry later) instead of 1.
    /// </summary>
    public static int StepToward(Simulation sim, MobileStore store, ActorKind kind, int i, CellCoord cell)
    {
        if (store.Health[i] <= 0) return 1;
        if (AirMission.IsAir(sim, kind, store, i)) return AirMission.StepToward(sim, store, i, cell);   // M3-06
        return Follow(sim, store, kind, i, cell, false, out _);
    }

    // ---- route state --------------------------------------------------------------------------

    /// <summary>Cancel / release the actor's path request without touching the cells it holds.</summary>
    internal static void DropRequest(Simulation sim, MobileStore store, int i)
    {
        int id = store.PathId[i];
        if (id > 0)
        {
            // A group id is shared by many actors: cancelling it would wipe the others' results, so only release our own entry.
            if (store.PathIsGroup[i]) sim.Paths.Poll(id, store.PathIndex[i], out _);
            else sim.Paths.Cancel(id);
        }
        store.PathId[i] = 0;
        store.PathIsGroup[i] = false;
        store.PathIndex[i] = 0;
    }

    private static void ResetPath(Simulation sim, MobileStore store, int i)
    {
        DropRequest(sim, store, i);
        store.PathActive[i] = false;
        store.PathCells[i] = null;
        store.PathPos[i] = 0;
        store.BlockedTicks[i] = 0;
        store.StuckCount[i] = 0;
    }

    // ---- locomotor ----------------------------------------------------------------------------

    private static int Follow(Simulation sim, MobileStore store, ActorKind kind, int i, CellCoord goal, bool settle, out bool arrived)
    {
        arrived = false;
        var map = sim.Map;
        if (!store.PathActive[i] || store.PathGoal[i] != goal)
        {
            ResetPath(sim, store, i);
            store.PathActive[i] = true;
            store.PathGoal[i] = goal;
            store.PathVersion[i] = map.Version;
        }
        Stats st = StatsOf(sim, kind, store, i);
        if (st.Speed <= 0) return 1;

        // 1. finish the step that was already started
        if (store.NextCellReserved[i]) return AdvanceAndCheck(sim, store, kind, i, st, goal, settle, out arrived);

        CellCoord cur = Occupancy.CurCell(kind, store, i);
        LeptonCoord pos = store.Position[i];
        LeptonCoord home = HomeOf(store, kind, i, cur);

        // 2. in the goal cell: settle on the exact spot (Move) or do nothing (StepToward)
        if (cur == goal)
        {
            if (!settle) return 1;
            if (pos == home) { arrived = true; return 1; }
            DriveWithin(sim, store, kind, i, st, cur, home);
            return 1;
        }
        // Off the cell's home spot (a Stop in mid-step): get back to it before leaving, so a step always starts on it.
        if (pos != home)
        {
            DriveWithin(sim, store, kind, i, st, cur, home);
            return 1;
        }

        // 3. a route is needed
        CellCoord[]? cells = store.PathCells[i];
        if (cells == null || store.PathPos[i] >= cells.Length)
        {
            if (store.PathId[i] <= 0)
            {
                store.PathId[i] = sim.Paths.Request(cur, goal, st.Loco, sim.GatePasser(store.Player[i]));   // M3-04: gates
                store.PathIndex[i] = 0;
                store.PathIsGroup[i] = false;
                store.Status[i] = MissionStatus.Waiting;
                return 1;
            }
            PathStatus status = sim.Paths.Poll(store.PathId[i], store.PathIndex[i], out IReadOnlyList<CellCoord> path);
            if (status == PathStatus.Pending) { store.Status[i] = MissionStatus.Waiting; return 1; }
            bool wasGroup = store.PathIsGroup[i];
            store.PathId[i] = 0;
            store.PathIsGroup[i] = false;
            store.PathIndex[i] = 0;
            if ((status == PathStatus.Found || status == PathStatus.Partial) && (path.Count > 0 || wasGroup))
            {
                var arr = new CellCoord[path.Count];
                for (int k = 0; k < arr.Length; k++) arr[k] = path[k];
                if (wasGroup) arr = TruncateToward(arr, cur, goal);
                store.PathCells[i] = arr;
                store.PathPos[i] = 0;
                store.PathVersion[i] = map.Version;
                cells = arr;
                if (cells.Length == 0) return 1; // already as close as the group route gets; next tick asks for the tail
            }
            else if (wasGroup && status == PathStatus.NotFound)
            {
                store.PathId[i] = sim.Paths.Request(cur, goal, st.Loco, sim.GatePasser(store.Player[i]));   // the group search is bounded; try a plain one
                return 1;
            }
            else
            {
                arrived = settle;            // no way forward: stop here
                return settle ? 1 : WaitTicks;
            }
        }

        // 4. the map changed: keep the route only if it is still walkable
        if (store.PathVersion[i] != map.Version)
        {
            store.PathVersion[i] = map.Version;
            for (int k = store.PathPos[i]; k < cells!.Length; k++)
                if (!Occupancy.TerrainOk(sim, cells[k], st.Loco, store.Player[i])) { store.PathCells[i] = null; return 1; }   // M3-04
        }
        CellCoord n = cells![store.PathPos[i]];
        if (Occupancy.Chebyshev(cur, n) != 1) { store.PathCells[i] = null; return 1; }

        // 5. turn toward the next cell (vehicles, D-042), then see whether we may enter it
        if (kind == ActorKind.Vehicle && TurnInPlace(store, i, LeptonCoord.CenterOf(n), st.TurnRate)) return 1;
        Block b = Inspect(sim, store, kind, i, st, cur, n);
        if (b.Invalid)
        {
            store.PathCells[i] = null;
            if (++store.StuckCount[i] > MaxStuck) arrived = settle;
            return 1;
        }
        if (!b.Enterable) return Blocked(sim, store, kind, i, st, cur, n, goal, settle, b, out arrived);

        store.BlockedTicks[i] = 0;
        store.StuckCount[i] = 0;
        Reserve(sim, store, kind, i, st, n, b);
        return AdvanceAndCheck(sim, store, kind, i, st, goal, settle, out arrived);
    }

    private static LeptonCoord HomeOf(MobileStore store, ActorKind kind, int i, CellCoord cell) =>
        kind == ActorKind.Vehicle ? LeptonCoord.CenterOf(cell) : InfantryStore.SlotPosition(cell, ((InfantryStore)store).SubSlot[i]);

    /// <summary>Advance, and report arrival in the same tick when the step ended on the final spot.</summary>
    private static int AdvanceAndCheck(Simulation sim, MobileStore store, ActorKind kind, int i, Stats st, CellCoord goal, bool settle, out bool arrived)
    {
        int sleep = Advance(sim, store, kind, i, st);
        arrived = false;
        if (settle && !store.NextCellReserved[i])
        {
            CellCoord c = Occupancy.CurCell(kind, store, i);
            arrived = c == goal && store.Position[i] == HomeOf(store, kind, i, c);
        }
        return sleep;
    }

    /// <summary>Drive the reserved step: move toward the claimed cell/slot, hand over the old cell when the boundary is crossed.</summary>
    private static int Advance(Simulation sim, MobileStore store, ActorKind kind, int i, Stats st)
    {
        CellCoord rc = store.ReservedCell[i];
        CellCoord cur = Occupancy.CurCell(kind, store, i);
        if (cur != rc && !sim.Map.BuildingAt(rc).IsNone && !sim.GateOpenFor(sim.Map.BuildingAt(rc), store.Player[i]))   // M3-04
        {
            // A building was placed on the cell we reserved before we got there: back out and repath.
            Occupancy.DropReservation(sim, kind, store, i);
            store.PathCells[i] = null;
            return 1;
        }
        LeptonCoord target = kind == ActorKind.Vehicle ? LeptonCoord.CenterOf(rc) : InfantryStore.SlotPosition(rc, store.ReservedSlot[i]);
        LeptonCoord pos = store.Position[i];
        if (pos == target) { Complete(kind, store, i); return 1; }
        if (kind == ActorKind.Vehicle) { if (TurnInPlace(store, i, target, st.TurnRate)) return 1; }
        else { store.Facing[i] = Facing.FromDelta(target.X - pos.X, target.Y - pos.Y); store.Status[i] = MissionStatus.Driving; }

        LeptonCoord next = Approach(pos, target, SpeedIn(sim, st, rc));
        CellCoord nc = next.ToCell();
        if (kind == ActorKind.Vehicle)
        {
            CellCoord pc = pos.ToCell();
            if (nc != pc && nc != rc)
            {
                // A diagonal step that lands exactly on a cell corner (speeds dividing 256, e.g. 8 or 16): a coordinate that
                // decreases and sits exactly on a boundary still belongs to the cell it is leaving, while the increasing
                // one already belongs to the cell it enters, so the position would fall into the third cell. Count the
                // decreasing axis as entered (1 lepton further) so the vehicle stays within the two cells it holds.
                int nx = next.X, ny = next.Y;
                if (nx < pos.X && (nx & 255) == 0 && nx != target.X) nx--;
                if (ny < pos.Y && (ny & 255) == 0 && ny != target.Y) ny--;
                next = new LeptonCoord(nx, ny);
                nc = next.ToCell();
            }
            if (nc != pc && nc != rc)
            {
                // Cannot happen when steps start on a cell centre; never leave the two cells we hold.
                Occupancy.DropReservation(sim, kind, store, i);
                store.PathCells[i] = null;
                return 1;
            }
            store.Position[i] = next;
            if (nc != pc)
            {
                EntityId me = store.Slots.IdOf(i);
                if (sim.Map.VehicleAt(pc) == me) sim.Map.SetVehicle(pc, EntityId.None);
            }
        }
        else
        {
            var inf = (InfantryStore)store;
            store.Position[i] = next;
            if (nc == rc && inf.SlotCell[i] != rc)
            {
                EntityId me = store.Slots.IdOf(i);
                if (sim.Map.InfantryAt(inf.SlotCell[i], inf.SubSlot[i]) == me) sim.Map.SetInfantry(inf.SlotCell[i], inf.SubSlot[i], EntityId.None);
                inf.SlotCell[i] = rc;
                inf.SubSlot[i] = store.ReservedSlot[i];
            }
        }
        if (next == target) Complete(kind, store, i);
        return 1;
    }

    private static void Complete(ActorKind kind, MobileStore store, int i)
    {
        if (kind == ActorKind.Infantry)
        {
            var inf = (InfantryStore)store;
            inf.SlotCell[i] = store.ReservedCell[i];
            inf.SubSlot[i] = store.ReservedSlot[i];
        }
        store.NextCellReserved[i] = false;
        CellCoord[]? cells = store.PathCells[i];
        int p = store.PathPos[i];
        if (cells != null && p < cells.Length && cells[p] == store.ReservedCell[i]) store.PathPos[i] = p + 1;
    }

    /// <summary>Claim the next cell (vehicle: the cell; infantry: a free sub-slot) and, for a Crusher, kill the enemy infantry in it.</summary>
    private static void Reserve(Simulation sim, MobileStore store, ActorKind kind, int i, Stats st, CellCoord n, in Block b)
    {
        EntityId me = store.Slots.IdOf(i);
        if (kind == ActorKind.Vehicle)
        {
            if (b.Kill)
                for (int s = 0; s < InfantryStore.SubSlotCount; s++)
                {
                    EntityId id = sim.Map.InfantryAt(n, s);
                    if (Occupancy.LiveInfantry(sim, id) && sim.CanAttackOwnedBy(store.Player[i], sim.Infantry.Player[id.Index])) sim.Infantry.Health[id.Index] = 0;
                }
            sim.Map.SetVehicle(n, me);
        }
        else
        {
            int slot = ((InfantryStore)store).SubSlot[i];
            if (!sim.Map.InfantryAt(n, slot).IsNone) slot = NearestFreeSlot(sim, store, i, n);
            sim.Map.SetInfantry(n, slot, me);
            store.ReservedSlot[i] = (byte)slot;
        }
        store.NextCellReserved[i] = true;
        store.ReservedCell[i] = n;
    }

    /// <summary>
    /// Free sub-slot of cell <paramref name="n"/> nearest to the walker (M2-01). The map's own "first free slot" order is
    /// not point-symmetric (NW comes before SE), so two mirrored soldiers would take mirrored cells but unmirrored corners.
    /// Ties (two corners equally near) go to the one on the left of the walking direction; the caller checked one is free.
    /// </summary>
    private static int NearestFreeSlot(Simulation sim, MobileStore store, int i, CellCoord n)
    {
        LeptonCoord pos = store.Position[i];
        LeptonCoord c = LeptonCoord.CenterOf(n);
        int best = -1; long bestD = long.MaxValue; int bestSide = 0;
        for (int s = 0; s < InfantryStore.SubSlotCount; s++)
        {
            if (!sim.Map.InfantryAt(n, s).IsNone) continue;
            LeptonCoord sp = InfantryStore.SlotPosition(n, s);
            long dx = sp.X - pos.X, dy = sp.Y - pos.Y, d = dx * dx + dy * dy;
            long cross = (long)(c.X - pos.X) * (sp.Y - pos.Y) - (long)(c.Y - pos.Y) * (sp.X - pos.X);
            int side = cross < 0 ? 0 : cross > 0 ? 2 : 1;   // y grows south: cross < 0 is the left-hand side
            if (d < bestD || (d == bestD && side < bestSide)) { best = s; bestD = d; bestSide = side; }
        }
        return best;
    }

    // ---- entering a cell ----------------------------------------------------------------------

    private static bool IsIdle(MobileStore s, int idx) =>
        s.NextMission[idx] == MissionKind.None
        && (s.Mission[idx] == MissionKind.None || s.Mission[idx] == MissionKind.Sleep
            || s.Mission[idx] == MissionKind.Guard || s.Mission[idx] == MissionKind.Stop);

    private static CellCoord NextCellOf(MobileStore s, int idx)
    {
        if (s.NextCellReserved[idx]) return s.ReservedCell[idx];
        CellCoord[]? cells = s.PathCells[idx];
        return cells != null && s.PathPos[idx] < cells.Length ? cells[s.PathPos[idx]] : new CellCoord(-1, -1);
    }

    private static Block Inspect(Simulation sim, MobileStore store, ActorKind kind, int i, Stats st, CellCoord cur, CellCoord n)
    {
        var b = new Block();
        if (!Occupancy.TerrainOk(sim, n, st.Loco, store.Player[i])) { b.Invalid = true; return b; }   // M3-04: own gates
        for (int d = 0; d < 8; d++)
            if (cur.X + Occupancy.Dx[d] == n.X && cur.Y + Occupancy.Dy[d] == n.Y)
            {
                if (!Occupancy.CornersOk(sim, cur, d, st.Loco, store.Player[i])) b.Invalid = true;   // M3-04
                break;
            }
        if (b.Invalid) return b;

        byte player = store.Player[i];
        EntityId me = store.Slots.IdOf(i);
        bool blocked = false;
        EntityId v = sim.Map.VehicleAt(n);
        if (!v.IsNone && !(kind == ActorKind.Vehicle && v == me))
        {
            if (!sim.Units.Slots.IsAlive(v)) sim.Map.SetVehicle(n, EntityId.None);   // stale claim, never expected
            else
            {
                blocked = true;
                if (!sim.IsFriendly(player, sim.Units.Player[v.Index])) b.EnemyVeh = true;
                else if (IsIdle(sim.Units, v.Index)) b.AllyIdle = true;
                else
                {
                    b.AllyMoving = true;
                    if (NextCellOf(sim.Units, v.Index) == cur) { b.Mutual = true; b.MutualIndex = v.Index; }
                }
            }
        }
        for (int s = 0; s < InfantryStore.SubSlotCount; s++)
        {
            EntityId id = sim.Map.InfantryAt(n, s);
            if (id.IsNone || (kind == ActorKind.Infantry && id == me)) continue;
            if (!sim.Infantry.Slots.IsAlive(id)) { sim.Map.SetInfantry(n, s, EntityId.None); continue; }
            if (kind == ActorKind.Vehicle && sim.Infantry.Health[id.Index] <= 0) continue;   // dying, cleaned up this tick
            bool ally = sim.IsFriendly(player, sim.Infantry.Player[id.Index]);
            if (kind == ActorKind.Vehicle && !ally && st.Crusher) { b.Kill = true; continue; }
            if (kind == ActorKind.Vehicle) blocked = true;
            if (!ally) b.EnemyInf = true;
            else if (IsIdle(sim.Infantry, id.Index)) b.AllyIdle = true;
            else b.AllyMoving = true;
        }
        if (kind == ActorKind.Infantry && sim.Map.FreeInfantrySlot(n) < 0) blocked = true;
        b.Enterable = !blocked;
        return b;
    }

    private static int Blocked(Simulation sim, MobileStore store, ActorKind kind, int i, Stats st, CellCoord cur, CellCoord n,
        CellCoord goal, bool settle, in Block b, out bool arrived)
    {
        arrived = false;
        store.Status[i] = MissionStatus.Waiting;
        int t = store.BlockedTicks[i];
        if (t == 0)
        {
            Shove(sim, store, kind, i, n);
            // Enemy infantry never gets out of the way and a non-crusher cannot crush it: go around at once.
            if (kind == ActorKind.Vehicle && b.EnemyInf && !b.EnemyVeh && !b.AllyIdle && !b.AllyMoving && n != goal
                && TryDetour(sim, store, kind, i, cur, n, goal))
            {
                if (++store.StuckCount[i] > MaxStuck) arrived = settle;
                return 1;
            }
        }
        if (t + 1 < WaitTicks) { store.BlockedTicks[i] = t + 1; return 1; }

        // waited long enough
        store.BlockedTicks[i] = 0;
        int stuck = ++store.StuckCount[i];
        if (stuck > MaxStuck) { arrived = settle; return 1; }
        if (n == goal)
        {
            // The final cell is taken: keep waiting while its occupant is itself on the move (or was just asked to move),
            // otherwise stop short in the cell we are in (the nearest free one along the route).
            if (!(b.AllyMoving && !b.Hostile)) arrived = settle;
            return 1;
        }
        // Head-on with an allied mover that is waiting for us: the higher slot index (infantry always) backs off, the other
        // keeps its place and simply waits, so the pair cannot both give way at once.
        bool yields = b.Mutual && (kind == ActorKind.Infantry || i > b.MutualIndex);
        bool holds = b.Mutual && !yields;
        if ((b.Hostile || b.AllyIdle || yields || (stuck >= 3 && !holds)) && TryDetour(sim, store, kind, i, cur, n, goal)) return 1;
        if (holds) return 1;
        store.PathCells[i] = null;   // plain repath
        store.PathPos[i] = 0;
        return 1;
    }

    /// <summary>Ask allied idle occupants of the cell to scatter. An actor that already has a mission queued is left alone.</summary>
    private static void Shove(Simulation sim, MobileStore store, ActorKind kind, int i, CellCoord n)
    {
        byte player = store.Player[i];
        EntityId me = store.Slots.IdOf(i);
        EntityId v = sim.Map.VehicleAt(n);
        if (!v.IsNone && !(kind == ActorKind.Vehicle && v == me) && sim.Units.Slots.IsAlive(v)
            && sim.IsFriendly(player, sim.Units.Player[v.Index]) && IsIdle(sim.Units, v.Index))
            sim.RequestScatter(ActorKind.Vehicle, sim.Units, v.Index);
        for (int s = 0; s < InfantryStore.SubSlotCount; s++)
        {
            EntityId id = sim.Map.InfantryAt(n, s);
            if (id.IsNone || (kind == ActorKind.Infantry && id == me) || !sim.Infantry.Slots.IsAlive(id)) continue;
            if (sim.IsFriendly(player, sim.Infantry.Player[id.Index]) && sim.Infantry.Health[id.Index] > 0 && IsIdle(sim.Infantry, id.Index))
                sim.RequestScatter(ActorKind.Infantry, sim.Infantry, id.Index);
        }
    }

    /// <summary>
    /// Sidestep around a blocked cell: the free neighbour (not the blocked one) closest to the cell after it becomes a one-cell
    /// route; the rest of the way is requested from there when it is reached.
    /// </summary>
    private static bool TryDetour(Simulation sim, MobileStore store, ActorKind kind, int i, CellCoord cur, CellCoord n, CellCoord goal)
    {
        CellCoord[]? cells = store.PathCells[i];
        int p = store.PathPos[i];
        CellCoord after = cells != null && p + 1 < cells.Length ? cells[p + 1] : goal;
        // Ties (equal Chebyshev distance) are broken by orientation-free rules so mirrored situations sidestep to mirrored
        // cells: smaller squared distance first, then the cell on the left of the heading cur -> after, then scan order.
        int best = -1, bestDist = int.MaxValue, bestSq = int.MaxValue, bestSide = 0;
        for (int d = 0; d < 8; d++)
        {
            var c = new CellCoord(cur.X + Occupancy.Dx[d], cur.Y + Occupancy.Dy[d]);
            if (c == n || !Occupancy.NeighbourFree(sim, kind, store, i, cur, d)) continue;
            int dist = Occupancy.Chebyshev(c, after);
            int sq = (c.X - after.X) * (c.X - after.X) + (c.Y - after.Y) * (c.Y - after.Y);
            long cross = (long)(after.X - cur.X) * (c.Y - cur.Y) - (long)(after.Y - cur.Y) * (c.X - cur.X);
            int side = cross < 0 ? 0 : cross > 0 ? 2 : 1;   // y grows south, so cross < 0 is the left-hand side
            if (dist < bestDist || (dist == bestDist && (sq < bestSq || (sq == bestSq && side < bestSide))))
            { best = d; bestDist = dist; bestSq = sq; bestSide = side; }
        }
        if (best < 0) return false;
        store.PathCells[i] = new[] { new CellCoord(cur.X + Occupancy.Dx[best], cur.Y + Occupancy.Dy[best]) };
        store.PathPos[i] = 0;
        store.PathVersion[i] = sim.Map.Version;
        store.BlockedTicks[i] = 0;
        return true;
    }

    // ---- geometry -----------------------------------------------------------------------------

    private static int SpeedIn(Simulation sim, Stats st, CellCoord cell)
    {
        int pct = sim.Map.SpeedPercent(cell, st.Loco, sim.Setup.Rules.Terrain);
        int s = (int)((long)st.Speed * pct / 100);
        if (st.Loco != Locomotion.Air && sim.IsFoamed(cell)) s = (int)((long)s * sim.Setup.Rules.Foam.SpeedPermille / 1000);   // M4-05: Quick-Set Foam
        return s < 1 ? 1 : s;
    }

    /// <summary>D-042: a large facing error starts a turn in place that runs until aligned (the aligning tick does not move); small errors are corrected while driving. True if this tick was spent turning.</summary>
    private static bool TurnInPlace(MobileStore store, int i, LeptonCoord target, int turnRate)
    {
        LeptonCoord pos = store.Position[i];
        byte desired = Facing.FromDelta(target.X - pos.X, target.Y - pos.Y);
        byte cur = store.Facing[i];
        if (turnRate <= 0) { store.Facing[i] = desired; store.Status[i] = MissionStatus.Driving; return false; }
        int diff = Math.Abs(Facing.Difference(cur, desired));
        if (store.Status[i] == MissionStatus.Turning || diff > MissionHandlers.TurnThreshold)
        {
            byte next = Facing.TurnToward(cur, desired, turnRate);
            store.Facing[i] = next;
            store.Status[i] = next == desired ? MissionStatus.Driving : MissionStatus.Turning;
            return true;
        }
        store.Status[i] = MissionStatus.Driving;
        if (diff != 0) store.Facing[i] = Facing.TurnToward(cur, desired, turnRate);
        return false;
    }

    /// <summary>Move inside the actor's own cell toward <paramref name="target"/> (settling on the exact spot).</summary>
    private static void DriveWithin(Simulation sim, MobileStore store, ActorKind kind, int i, Stats st, CellCoord cur, LeptonCoord target)
    {
        LeptonCoord pos = store.Position[i];
        if (kind == ActorKind.Vehicle) { if (TurnInPlace(store, i, target, st.TurnRate)) return; }
        else { store.Facing[i] = Facing.FromDelta(target.X - pos.X, target.Y - pos.Y); store.Status[i] = MissionStatus.Driving; }
        store.Position[i] = Approach(pos, target, SpeedIn(sim, st, cur));
    }

    /// <summary>One straight step of at most <paramref name="speed"/> leptons on the longer axis, clamped at the target.</summary>
    private static LeptonCoord Approach(LeptonCoord pos, LeptonCoord target, int speed)
    {
        int dx = target.X - pos.X, dy = target.Y - pos.Y;
        int ax = Math.Abs(dx), ay = Math.Abs(dy);
        if (Math.Max(ax, ay) <= speed) return target;
        int sx, sy;
        if (ax >= ay) { sx = speed; sy = (int)((long)ay * speed / ax); }
        else { sy = speed; sx = (int)((long)ax * speed / ay); }
        return new LeptonCoord(pos.X + (dx < 0 ? -sx : sx), pos.Y + (dy < 0 ? -sy : sy));
    }

    /// <summary>
    /// A group route leads to the shared target; keep the part up to the cell closest to this actor's own spread destination
    /// (the rest is searched from there when the route runs out).
    /// </summary>
    private static CellCoord[] TruncateToward(CellCoord[] path, CellCoord start, CellCoord dest)
    {
        int best = -1, bestDist = Occupancy.Chebyshev(start, dest);
        for (int k = 0; k < path.Length; k++)
        {
            int d = Occupancy.Chebyshev(path[k], dest);
            if (d < bestDist) { best = k; bestDist = d; }
        }
        if (best == path.Length - 1) return path;
        var r = new CellCoord[best + 1];
        Array.Copy(path, r, best + 1);
        return r;
    }
}
