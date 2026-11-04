// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Sim.Core;

namespace Rts.Sim;

// Garrisons (design 08 §3). OWNER: M4-03 (M4-15 adds the neutral owner flip in wave 2). Stubs placed by M4-01; the call sites are in
// Simulation.M3.cs (ApplyEnter routing, OnM3BuildingRemoving) and Simulation.cs (Apply: CommandKind.Unload).
public sealed partial class Simulation
{
    /// <summary>Enter routed to a garrisonable building (own, allied, or neutral and empty). Subject = own live mobile actor,
    /// already checked by ApplyEnter. M4-03 fills: walk in with MissionKind.Capture, then GarrisonNow on arrival.</summary>
    private void ApplyGarrison(Command c, ActorRef subject)
    {
        // M4-03 fills
    }

    /// <summary>CommandKind.Unload: Subject = a building (Arg low byte = Building) or a transport vehicle (Arg low byte = Vehicle,
    /// alias of Deploy-unload). M4-03 fills.</summary>
    private void ApplyUnloadCommand(Command c)
    {
        // M4-03 fills
    }

    /// <summary>A building is being removed (destroyed or sold), before its cells are freed: eject the occupants. M4-03 fills.</summary>
    private void OnGarrisonBuildingRemoving(int b)
    {
        // M4-03 fills
    }
}
