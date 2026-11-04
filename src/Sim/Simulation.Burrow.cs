// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Sim.Core;

namespace Rts.Sim;

// Free burrowing, surfacing telegraphs, deploy-into-building and the building Upgrade command (design 05 §1, §4). OWNER: M3-03.
// State: Tunnels (layers, Telegraphs, BuildingMorphType/Ticks). Missions: MissionKind.Burrow / Surface / Deploy.
public sealed partial class Simulation
{
    /// <summary>Per tick (inside RunTunnels, after RunTunnelTransit): dive, burrowed travel, telegraph countdown, surfacing,
    /// Bore break-through, building morphs.</summary>
    private void RunBurrowing()
    {
        // M3-03 fills this
    }

    /// <summary>Deploy for a vehicle with DeploysInto &gt;= 0 (Bore: travel burrowed to Cell then break through into a Hatch;
    /// land-train: become the Command Shaft in place). Subject is an own live vehicle.</summary>
    private void ApplyDeployInto(Command c)
    {
        // M3-03 fills this
    }

    /// <summary>Upgrade command: Subject = own building, Arg = target building type (Hatch to Bore Head; cost = difference).</summary>
    private void ApplyUpgrade(Command c)
    {
        // M3-03 fills this
    }
}
