// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Sim.Core;
using Rts.Sim.Missions;
using Rts.Sim.World;

namespace Rts.Sim;

// S19 D-205 (M13-15): stances and shift-queued waypoints. The data lives in the "combat" part (CombatState), saved only for slots that
// use it, so games that never set a stance or queue a waypoint keep their old bytes and hashes.
public sealed partial class Simulation
{
    /// <summary>Cells a <see cref="UnitStance.Defensive"/> unit chases a target from its post before it gives up and walks back.</summary>
    public const int DefensiveChaseCells = 3;

    /// <summary>The stance of a live vehicle or soldier (<see cref="UnitStance.Aggressive"/> for anything else).</summary>
    public UnitStance StanceOf(ActorRef actor) =>
        actor.Kind <= ActorKind.Infantry && StoreOf(actor.Kind) is { } s && s.Slots.IsAlive(actor.Id)
            ? _combat.StanceOf(actor.Kind, actor.Id.Index) : UnitStance.Aggressive;

    /// <summary>How many waypoints a live vehicle or soldier has queued (0 for anything else). Cheap: for per-frame UI checks.</summary>
    public int WaypointCountOf(ActorRef actor) =>
        actor.Kind <= ActorKind.Infantry && StoreOf(actor.Kind) is { } s && s.Slots.IsAlive(actor.Id)
            ? _combat.WaypointCount(actor.Kind, actor.Id.Index) : 0;

    /// <summary>The queued waypoints of a live vehicle or soldier, next to run first; empty for anything else. Allocates: for UI and tests.</summary>
    public IReadOnlyList<(CellCoord Cell, bool AttackMove)> WaypointsOf(ActorRef actor)
    {
        var list = new List<(CellCoord, bool)>();
        if (actor.Kind > ActorKind.Infantry || StoreOf(actor.Kind) is not { } s || !s.Slots.IsAlive(actor.Id)) return list;
        for (int k = 0; _combat.TryGetWaypoint(actor.Kind, actor.Id.Index, k, out CellCoord cell, out bool atk); k++) list.Add((cell, atk));
        return list;
    }

    private void ApplySetStance(Command c)
    {
        if (!OwnsSubject(c with { Arg = c.Arg & 0xFF }, out var store)) return;
        int stance = (c.Arg >> 8) & 0xFF;
        if (stance > (int)UnitStance.HoldFire) return;
        var kind = (ActorKind)(c.Arg & 0xFF);
        _combat.SetStance(kind, c.Subject.Index, (UnitStance)stance);
        store!.WakeTick[c.Subject.Index] = 0;   // an idle guard re-reads its stance on the next tick
    }

    private void ApplyQueueOrder(Command c)
    {
        if (!OwnsSubject(c with { Arg = c.Arg & 0xFF }, out var store) || !InMap(c.Cell)) return;
        var kind = (ActorKind)(c.Arg & 0xFF);
        int i = c.Subject.Index;
        bool attackMove = (c.Arg & 0x100) != 0;
        bool idle = store!.NextMission[i] == MissionKind.None && _combat.WaypointCount(kind, i) == 0
            && store.Mission[i] is MissionKind.None or MissionKind.Sleep or MissionKind.Guard or MissionKind.Stop;
        if (!idle) { _combat.AddWaypoint(kind, i, c.Cell, attackMove); return; }
        IssueWaypoint(store, kind, i, c.Cell, attackMove);
    }

    // Gives the unit a plain Move or AttackMove through the normal command paths; true when the order took (the unit's next mission changed).
    private bool IssueWaypoint(MobileStore store, ActorKind kind, int i, CellCoord cell, bool attackMove)
    {
        EntityId id = store.Slots.IdOf(i);
        MissionKind before = store.NextMission[i];
        if (attackMove) ApplyCombatCommand(new Command(Tick, store.Player[i], 0, CommandKind.AttackMove, id, EntityId.None, cell, (int)kind));
        else
        {
            _combat.At(kind, i).GuardPending = false;
            ApplyMovementCommand(new Command(Tick, store.Player[i], 0, CommandKind.Move, id, EntityId.None, cell, (int)kind));
        }
        return store.NextMission[i] != before;
    }

    // Called by Commence when a unit is about to fall idle (next mission Guard) with waypoints left: run the next one that is accepted.
    private void StartQueuedWaypoint(MobileStore store, ActorKind kind, int i)
    {
        while (_combat.PopWaypoint(kind, i, out CellCoord cell, out bool attackMove))
            if (IssueWaypoint(store, kind, i, cell, attackMove)) return;
    }

    // A plain unit order replaces the queue (Shift orders and stance changes do not).
    private void ClearQueueForPlainOrder(Command c)
    {
        if (c.Kind is not (CommandKind.Move or CommandKind.Stop or CommandKind.Scatter or CommandKind.Attack or CommandKind.AttackMove
            or CommandKind.Guard or CommandKind.AttackGround or CommandKind.Enter or CommandKind.Harvest or CommandKind.Return
            or CommandKind.Deploy)) return;
        var kind = (ActorKind)(c.Arg & 0xFF);
        if (kind > ActorKind.Infantry || StoreOf(kind) is not { } s || !s.Slots.IsAlive(c.Subject) || s.Player[c.Subject.Index] != c.Player) return;
        _combat.ClearWaypoints(kind, c.Subject.Index);
    }
}
