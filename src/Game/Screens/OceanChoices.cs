// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Data;

namespace Rts.Game.Screens;

/// <summary>
/// The "Ocean" choices of the skirmish setup screen for generated maps (M7-14): none, one edge, or two opposite edges. Pure, so it is
/// unit-tested without graphics. The generator also floods the symmetry images of a chosen edge.
/// </summary>
public static class OceanChoices
{
    /// <summary>The choices in cycle order: None, North, East, South, West, North+South, East+West.</summary>
    public static IReadOnlyList<OceanEdges> All { get; } = new[]
    {
        OceanEdges.None, OceanEdges.North, OceanEdges.East, OceanEdges.South, OceanEdges.West,
        OceanEdges.North | OceanEdges.South, OceanEdges.East | OceanEdges.West,
    };

    /// <summary>The choice <paramref name="delta"/> steps from <paramref name="current"/>, wrapping. A value not in the list counts as None.</summary>
    public static OceanEdges Step(OceanEdges current, int delta)
    {
        int at = 0;
        for (int i = 0; i < All.Count; i++) if (All[i] == current) at = i;
        return All[((at + delta) % All.Count + All.Count) % All.Count];
    }

    /// <summary>Row text, for example "Ocean: None", "Ocean: North" or "Ocean: North and South".</summary>
    public static string Label(OceanEdges edges)
    {
        if (edges == OceanEdges.None) return "Ocean: None";
        var names = new List<string>();
        if ((edges & OceanEdges.North) != 0) names.Add("North");
        if ((edges & OceanEdges.East) != 0) names.Add("East");
        if ((edges & OceanEdges.South) != 0) names.Add("South");
        if ((edges & OceanEdges.West) != 0) names.Add("West");
        return "Ocean: " + string.Join(" and ", names);
    }
}
