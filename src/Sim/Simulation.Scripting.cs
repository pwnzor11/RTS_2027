// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Sim.Core;
using Rts.Sim.Missions;
using Rts.Sim.World;

namespace Rts.Sim;

// Scripting entry points (M2-15). Setup phase or inside Step only, like the Spawn* methods.
public sealed partial class Simulation
{
    /// <summary>
    /// Setup/scripting entry: order an own mobile actor to attack-move to a cell, exactly as an AttackMove command would
    /// (the order commences at the start of the next RunMissions). False if the actor is gone, is a building or the cell is off the map.
    /// </summary>
    public bool AssignAttackMove(ActorRef actor, CellCoord cell)
    {
        RequireSetupPhase();
        MobileStore? store = StoreOf(actor.Kind);
        if (store == null || !store.Slots.IsAlive(actor.Id) || !InMap(cell)) return false;
        int i = actor.Id.Index;
        ref var e = ref _combat.At(actor.Kind, i);
        e.GuardPoint = cell; e.GuardPending = true; e.AttackTarget = default;
        Queue(store, i, MissionKind.AttackMove, cell, default);
        return true;
    }
}
