// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).

namespace Rts.Game.Render;

/// <summary>The kind of road piece a road cell is, from which of its four neighbours are road (design 18 section 3, roads row).</summary>
public enum RoadPiece
{
    /// <summary>No road neighbour: a lone road cell.</summary>
    Isolated,

    /// <summary>One road neighbour: a dead end.</summary>
    EndCap,

    /// <summary>Two opposite road neighbours.</summary>
    Straight,

    /// <summary>Two road neighbours at a right angle.</summary>
    Corner,

    /// <summary>Three road neighbours.</summary>
    TJunction,

    /// <summary>Four road neighbours.</summary>
    Cross,
}
