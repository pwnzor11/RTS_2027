// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
namespace Rts.Data.MapGen;

/// <summary>
/// Path-equality result of <see cref="MapAnalysis.Spread"/> (design 08 §1 check 3): for each measure, the largest difference across
/// starts of the BFS path length (Tracked) from a start to (a) its nearest resource field, (b) the map centre and (c) the other starts
/// (sorted lists compared element by element). 0 = equal; <see cref="MapAnalysis.Unreachable"/> when some target cannot be reached.
/// </summary>
public readonly record struct PathSpread(int NearestField, int Centre, int OtherStarts)
{
    /// <summary>True when all three measures are equal across starts.</summary>
    public bool IsExact => NearestField == 0 && Centre == 0 && OtherStarts == 0;
}
