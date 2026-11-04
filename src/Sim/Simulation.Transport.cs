// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Sim.Core;

namespace Rts.Sim;

// Transports and engineers (design 05 §4). OWNER: M3-06. State: Tunnels.InfantryLayer (Passenger), Tunnels.InfantryCarrier.
// Command routing (ApplyEnter / ApplyDeploy) lives in Simulation.M3.cs.
public sealed partial class Simulation
{
    /// <summary>Enter with Target = own/allied vehicle: board it if it has room (VehicleType.Passengers).</summary>
    private void ApplyBoard(Command c, ActorRef unit)
    {
        // M3-06 fills this
    }

    /// <summary>Deploy on a transport (Passengers &gt; 0): unload everyone.</summary>
    private void ApplyUnload(Command c)
    {
        // M3-06 fills this
    }

    /// <summary>Enter with Target = a building that is not an own entrance: engineer capture (enemy, below half health) or repair
    /// (friendly). Ignore unless the unit is an Engineer.</summary>
    private void ApplyEngineer(Command c, ActorRef unit)
    {
        // M3-06 fills this
    }

    /// <summary>A vehicle is dying: its passengers are lost.</summary>
    private void OnTransportDying(int vehicleIndex)
    {
        // M3-06 fills this
    }
}
