// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Sim.Core;
using Rts.Sim.Rules;
using Rts.Sim.View;
using Rts.Sim.World;

namespace Rts.Sim;

// Player-view part of Simulation. OWNER: M1.5-06 (AI host). Additive only: no state, no sim behaviour.
public sealed partial class Simulation
{
    /// <summary>A read-only, fog-honest view of this simulation for one player (D-051). Allocation-free; cheap to
    /// call every tick. The view reads live state, so use it between Steps, never inside one.</summary>
    public PlayerView ViewFor(byte player)
    {
        if (player >= Setup.PlayerCount) throw new ArgumentOutOfRangeException(nameof(player));
        return new PlayerView(this, player);
    }

    /// <summary>True if the player could Place this structure at <paramref name="topLeft"/> now: the terrain/occupancy rules of
    /// <see cref="CanPlace"/> AND the D-062 build radius. Used by <see cref="PlayerView.CanPlace"/> so an AI never duplicates the rules.</summary>
    internal bool CanPlaceInRadius(byte player, int buildingType, CellCoord topLeft)
    {
        if (buildingType < 0 || buildingType >= BuildingTypeCount) return false;
        return CanPlace(player, buildingType, topLeft) == PlacementResult.Ok
            && WithinBuildRadius(player, Setup.Rules.Buildings[buildingType], topLeft);
    }
}
