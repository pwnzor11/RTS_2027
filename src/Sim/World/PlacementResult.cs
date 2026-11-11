// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
namespace Rts.Sim.World;

/// <summary>Outcome of <c>Simulation.CanPlace</c>. Append only.</summary>
public enum PlacementResult : byte
{
    /// <summary>The building may be placed.</summary>
    Ok = 0,
    /// <summary>Part of the footprint lies outside the map.</summary>
    OutOfMap = 1,
    /// <summary>Terrain, overlay, another building or a unit occupies a footprint cell.</summary>
    Blocked = 2,
    /// <summary>Placing it would split a region of passable cells (one-cell gap rule).</summary>
    SealsRegion = 3,
    /// <summary>M7-07: an Expedition structure's footprint is not wholly inside the player's beachhead zone.</summary>
    OutsideBeachhead = 4,
    /// <summary>M13-07: the shared building store (walls and gates count) has no free slot.</summary>
    StoreFull = 5,
}
