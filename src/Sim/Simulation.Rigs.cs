// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Sim.Core;

namespace Rts.Sim;

// M6-03 (OWNER: M6-03 package; hooks placed by the lead in session 9). Rig pack/unpack (docs/design/10_m6_plan.md §3.2–3.3, D-102 Q-A).
// Hooks: Apply (CommandKind.Pack) -> ApplyPack; StepCore -> RunRigs (after RunProduction); MissionKind.Pack -> Missions/RigMission.cs;
// PlayerView.PackProgressPermille -> PackProgressOf.
public sealed partial class Simulation
{
    /// <summary>CommandKind.Pack: Subject = an own rig building (pack) or an own packed rig vehicle (unpack at Cell = top-left).</summary>
    private void ApplyPack(Command c) { }

    /// <summary>Per tick after production: advance packing and unpacking timers.</summary>
    private void RunRigs() { }

    /// <summary>Pack or unpack progress of an own actor in permille (0 = idle or not a rig; 1000 = done).</summary>
    internal int PackProgressOf(byte player, ActorRef actor) => 0;
}
