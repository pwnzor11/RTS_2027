// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).

namespace Rts.Game.Render;

/// <summary>A road cell that gets a junction patch or an end cap, found once per map.</summary>
/// <param name="X">Cell x.</param>
/// <param name="Y">Cell y.</param>
/// <param name="Piece">Its kind.</param>
/// <param name="Mask">Road neighbours (<see cref="RoadPieces.North"/> and so on).</param>
public readonly record struct RoadCell(int X, int Y, RoadPiece Piece, byte Mask);
