// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Sim.Core;

namespace Rts.Sim;

// M6-05 (OWNER: M6-05 package; hooks placed by the lead in session 9). Raiders: plunder, withdraw, spoils (10_m6_plan.md §3.2).
// Hooks: ApplyEnter -> TryApplyRaid first (after the subject check); ApplyPendingDamage -> OnSpoilsLethalHit after OnLethalHit;
// MissionKind.Withdraw -> Missions/WithdrawMission.cs.
public sealed partial class Simulation
{
    /// <summary>Enter from a unit with raider = true on an enemy Silo/Refinery: start the plunder. True = handled.</summary>
    private bool TryApplyRaid(Command c, ActorRef subject) => false;

    /// <summary>A hit from <paramref name="source"/> killed <paramref name="target"/>: pay spoils when the killer is a raider type and
    /// the victim a harvester.</summary>
    private void OnSpoilsLethalHit(ActorRef target, byte source) { }
}
