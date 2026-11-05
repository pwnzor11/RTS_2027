// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
namespace Rts.Data;

/// <summary>Map edges the generator turns into sea (M7-13, <see cref="MapGenParams.OceanEdges"/>). Flags; the generator paints every
/// symmetry image of a chosen edge too, so on a rotation-symmetric map a single edge also floods its image edge(s).</summary>
[Flags]
public enum OceanEdges : byte
{
    /// <summary>No ocean edge (the default; the map is byte-identical to the pre-M7 generator).</summary>
    None = 0,
    /// <summary>The top edge (y = 0).</summary>
    North = 1,
    /// <summary>The right edge (x = width - 1).</summary>
    East = 2,
    /// <summary>The bottom edge (y = height - 1).</summary>
    South = 4,
    /// <summary>The left edge (x = 0).</summary>
    West = 8,
}
