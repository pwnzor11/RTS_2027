// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Sim.Core;
using Rts.Sim.Missions;
using Rts.Sim.Movement;
using Rts.Sim.Rules;
using Rts.Sim.World;

namespace Rts.Sim;

// Movement part of Simulation. OWNER: M1-08 (movement and locomotion).
public sealed partial class Simulation
{
    /// <summary>A Move command applied this tick whose path request is created in <see cref="RunMovement"/> (not saved: empty between ticks).</summary>
    private sealed class PendingMove
    {
        public ActorKind Kind;
        public EntityId Id;
        public byte Player;
        public CellCoord Goal;
        public bool Done;
    }

    private readonly List<PendingMove> _pendingMoves = new();
    private readonly List<PendingMove> _members = new();

    /// <summary>Move / Stop / Scatter commands (Subject kind in Command.Arg, see Command.cs).</summary>
    private void ApplyMovementCommand(Command c)
    {
        switch (c.Kind)
        {
            case CommandKind.Move:
                if (OwnsSubject(c, out var store) && InMap(c.Cell))
                {
                    var kind = (ActorKind)c.Arg;
                    int i = c.Subject.Index;
                    // The path request is made in RunMovement so Moves to one cell in one tick share one group search.
                    // PathId -1 = "request pending"; a repeated Move of the same actor in the tick replaces the earlier one.
                    MoveMission.DropRequest(this, store!, i);
                    store!.PathId[i] = -1;
                    for (int p = 0; p < _pendingMoves.Count; p++)
                        if (_pendingMoves[p].Kind == kind && _pendingMoves[p].Id == c.Subject) { _pendingMoves.RemoveAt(p); break; }
                    _pendingMoves.Add(new PendingMove { Kind = kind, Id = c.Subject, Player = c.Player, Goal = c.Cell });
                    Queue(store, i, MissionKind.Move, c.Cell, default);
                }
                break;
            case CommandKind.Stop:
                if (OwnsSubject(c, out store)) Queue(store!, c.Subject.Index, MissionKind.Stop, default, default);
                break;
            case CommandKind.Scatter:
                if (OwnsSubject(c, out store)) RequestScatter((ActorKind)c.Arg, store!, c.Subject.Index);
                break;
        }
    }

    /// <summary>
    /// Deterministic integer from the sim RNG in [0, maxExclusive). Movement code (scatter) draws from the same stream as everything
    /// else, in slot-index order inside the mission pass, so results never depend on anything but the sim state.
    /// </summary>
    internal int NextRandom(int maxExclusive) => _rng.Next(maxExclusive);

    /// <summary>
    /// Make the actor sidestep to the first free adjacent cell (N, NE, E, SE, S, SW, W, NW; first direction drawn from the RNG).
    /// Queues a Move mission with a preset one-cell route (so no path search is needed). False if every neighbour is taken.
    /// </summary>
    internal bool RequestScatter(ActorKind kind, MobileStore store, int i)
    {
        if (!Occupancy.FindScatterCell(this, kind, store, i, NextRandom(8), out CellCoord cell)) return false;
        MoveMission.DropRequest(this, store, i);
        store.PathActive[i] = true;
        store.PathGoal[i] = cell;
        store.PathCells[i] = new[] { cell };
        store.PathPos[i] = 0;
        store.PathId[i] = -2;
        store.PathVersion[i] = _map.Version;
        store.BlockedTicks[i] = 0;
        store.StuckCount[i] = 0;
        Queue(store, i, MissionKind.Move, cell, default);
        return true;
    }

    /// <summary>Movement system: runs once per tick after missions. Path requests are made and solved here.</summary>
    private void RunMovement()
    {
        if (_pendingMoves.Count > 0)
        {
            StartPendingMoves();
            _pendingMoves.Clear();
        }
        _paths.Process();
    }

    // Takes the Moves applied this tick in command order. Moves of one player to one cell are one batch: if there are several, each
    // actor first gets its own destination from a deterministic spiral around the cell (vehicles one per cell, infantry up to
    // 5), then actors of one locomotion share one group search to the common cell (RequestGroup).
    private void StartPendingMoves()
    {
        for (int a = 0; a < _pendingMoves.Count; a++)
        {
            PendingMove first = _pendingMoves[a];
            if (first.Done) continue;
            _members.Clear();
            for (int b = a; b < _pendingMoves.Count; b++)
            {
                PendingMove pm = _pendingMoves[b];
                if (pm.Done || pm.Player != first.Player || pm.Goal != first.Goal) continue;
                pm.Done = true;
                MobileStore? s = StoreOf(pm.Kind);
                if (s == null || !s.Slots.IsAlive(pm.Id) || s.PathId[pm.Id.Index] != -1) continue;
                if (s.Mission[pm.Id.Index] != MissionKind.Move) { s.PathId[pm.Id.Index] = 0; continue; }   // overridden later this tick
                _members.Add(pm);
            }
            if (_members.Count > 0) StartBatch(first.Goal);
        }
    }

    private void StartBatch(CellCoord target)
    {
        int count = _members.Count;
        var dest = new CellCoord[count];
        for (int m = 0; m < count; m++) dest[m] = target;
        if (count >= 2) Spread(target, dest);

        var from = new CellCoord[count];
        for (int m = 0; m < count; m++)
        {
            MobileStore s = StoreOf(_members[m].Kind)!;
            int i = _members[m].Id.Index;
            from[m] = s.NextCellReserved[i] ? s.ReservedCell[i] : Occupancy.CurCell(_members[m].Kind, s, i);
            s.Destination[i] = _members[m].Kind == ActorKind.Infantry
                ? InfantryStore.SlotPosition(dest[m], _infantry.SubSlot[i])
                : LeptonCoord.CenterOf(dest[m]);
            s.PathActive[i] = true;
            s.PathGoal[i] = dest[m];
            s.PathCells[i] = null;
            s.PathPos[i] = 0;
            s.PathVersion[i] = _map.Version;
            s.BlockedTicks[i] = 0;
            s.StuckCount[i] = 0;
            s.PathId[i] = 0;
        }

        // one request per locomotion class, in order of first appearance
        var done = new bool[count];
        var group = new List<int>();
        for (int m = 0; m < count; m++)
        {
            if (done[m]) continue;
            Locomotion loco = LocoOf(_members[m]);
            group.Clear();
            for (int k = m; k < count; k++)
                if (!done[k] && LocoOf(_members[k]) == loco)
                {
                    done[k] = true;
                    if (from[k] != dest[k]) group.Add(k);   // an actor already standing on its spot needs no route
                }
            if (group.Count == 1)
            {
                int k = group[0];
                MobileStore s = StoreOf(_members[k].Kind)!;
                int i = _members[k].Id.Index;
                s.PathId[i] = _paths.Request(from[k], dest[k], loco);
                s.PathIndex[i] = 0;
                s.PathIsGroup[i] = false;
            }
            else if (group.Count > 1)
            {
                var froms = new CellCoord[group.Count];
                for (int g = 0; g < froms.Length; g++) froms[g] = from[group[g]];
                int id = _paths.RequestGroup(froms, target, loco);
                for (int g = 0; g < froms.Length; g++)
                {
                    MobileStore s = StoreOf(_members[group[g]].Kind)!;
                    int i = _members[group[g]].Id.Index;
                    s.PathId[i] = id;
                    s.PathIndex[i] = g;
                    s.PathIsGroup[i] = true;
                }
            }
        }
    }

    private Locomotion LocoOf(PendingMove pm) =>
        pm.Kind == ActorKind.Vehicle ? Setup.Rules.Vehicles[_units.TypeIndex[pm.Id.Index]].Locomotion : Locomotion.Foot;

    // Spiral spreading: members in command order each take the first spiral cell that is walkable for them and not yet used
    // (a vehicle needs a cell nobody else was given, infantry share a cell up to 5 and never with a vehicle). A cell held by a
    // vehicle outside the batch is skipped for vehicles.
    private void Spread(CellCoord target, CellCoord[] dest)
    {
        var cells = new List<CellCoord>();
        var counts = new List<int>();        // vehicles: -1; infantry: how many
        for (int m = 0; m < _members.Count; m++)
        {
            PendingMove pm = _members[m];
            bool vehicle = pm.Kind == ActorKind.Vehicle;
            Locomotion loco = LocoOf(pm);
            dest[m] = target;
            for (int n = 0; Spiral.Cell(target, n, out CellCoord c); n++)
            {
                if (!Occupancy.TerrainOk(this, c, loco)) continue;
                int slot = cells.IndexOf(c);
                if (slot >= 0)
                {
                    if (vehicle || counts[slot] < 0 || counts[slot] >= InfantryStore.SubSlotCount) continue;
                }
                else if (!HeldByBatchOrNobody(c, vehicle)) continue;
                dest[m] = c;
                if (slot >= 0) counts[slot]++;
                else { cells.Add(c); counts.Add(vehicle ? -1 : 1); }
                break;
            }
        }
    }

    private bool HeldByBatchOrNobody(CellCoord c, bool vehicleMover)
    {
        EntityId v = _map.VehicleAt(c);
        if (!v.IsNone)
        {
            if (!vehicleMover) return false;
            bool member = false;
            for (int k = 0; k < _members.Count; k++)
                if (_members[k].Kind == ActorKind.Vehicle && _members[k].Id == v) { member = true; break; }
            if (!member) return false;
        }
        return true;
    }

    /// <summary>Called after a vehicle or infantry slot is created (spawn or production): claim its cell / sub-slot.</summary>
    private void OnActorSpawned(ActorKind kind, int index)
    {
        MobileStore? store = StoreOf(kind);
        if (store != null) Occupancy.Claim(this, kind, store, index);
    }

    /// <summary>Called just before a dead vehicle or infantry slot is freed: release occupancy and path request; drop targets (M1-09).</summary>
    private void OnActorRemoving(ActorKind kind, int index)
    {
        MobileStore? store = StoreOf(kind);
        if (store != null)
        {
            MoveMission.DropRequest(this, store, index);
            Occupancy.Release(this, kind, store, index);
        }
        OnCombatActorRemoving(kind, index);
    }
}
