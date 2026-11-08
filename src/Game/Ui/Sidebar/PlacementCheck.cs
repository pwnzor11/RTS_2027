// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Sim.World;

namespace Rts.Game.Ui.Sidebar;

/// <summary>Why one footprint cell cannot take a building (M11-10f). Ordered by how permanent the problem is.</summary>
public enum CellProblem
{
    /// <summary>The cell is fine.</summary>
    None,
    /// <summary>A land building over water.</summary>
    Water,
    /// <summary>A water building over land.</summary>
    NeedsWater,
    /// <summary>Cliff, rock or other ground nothing can be built on.</summary>
    Terrain,
    /// <summary>Another building or an overlay (wall, pitchblende) is there.</summary>
    Building,
    /// <summary>A unit stands there.</summary>
    Unit,
}

/// <summary>
/// The words and per-cell causes for the placement ghost (audit P1, P2). The verdict itself always comes from the sim
/// (<c>Simulation.CanPlace</c> plus the build radius); this only explains it, so the ghost can never say green when the sim says no.
/// </summary>
public static class PlacementCheck
{
    /// <summary>The cause for one footprint cell, mirroring the sim's per-cell terrain test (cause only; the sim still decides).</summary>
    public static CellProblem Cell(bool wantWater, bool isWater, bool buildable, bool hasBuildingOrOverlay, bool hasUnit)
    {
        if (wantWater)
        {
            if (!isWater) return CellProblem.NeedsWater;
            if (hasBuildingOrOverlay) return CellProblem.Building;
        }
        else if (isWater) return CellProblem.Water;
        else if (!buildable) return hasBuildingOrOverlay ? CellProblem.Building : CellProblem.Terrain;
        return hasUnit ? CellProblem.Unit : CellProblem.None;
    }

    /// <summary>The most permanent problem of a set (the one worth naming).</summary>
    public static CellProblem Worst(IEnumerable<CellProblem> cells)
    {
        CellProblem worst = CellProblem.None;
        foreach (CellProblem c in cells)
            if (c != CellProblem.None && (worst == CellProblem.None || c < worst)) worst = c;
        return worst;
    }

    /// <summary>One line saying why the spot is refused, or null when it is fine.</summary>
    /// <param name="result">The sim's terrain and occupancy verdict.</param>
    /// <param name="inRadius">True when the sim's build radius test passes.</param>
    /// <param name="worst">The worst per-cell cause, used to name a plain Blocked.</param>
    public static string? Reason(PlacementResult result, bool inRadius, CellProblem worst) => result switch
    {
        PlacementResult.Ok => inRadius ? null : "Too far from your base",
        PlacementResult.OutOfMap => "Off the map",
        PlacementResult.SealsRegion => "Would seal off an area",
        PlacementResult.OutsideBeachhead => "Outside your beachhead",
        _ => worst switch
        {
            CellProblem.Water => "Not on water",
            CellProblem.NeedsWater => "Must be built on water",
            CellProblem.Terrain => "Blocked by terrain",
            CellProblem.Building => "Blocked by a building",
            CellProblem.Unit => "Blocked by a unit",
            _ => "Blocked",
        },
    };
}
