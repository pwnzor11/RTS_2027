// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).

namespace Rts.Game.Render;

/// <summary>One flat rectangle of the procedural road art, in zoom-1 world pixels.</summary>
/// <param name="X">Left.</param>
/// <param name="Y">Top.</param>
/// <param name="W">Width.</param>
/// <param name="H">Height.</param>
/// <param name="Colour">Fill colour.</param>
/// <param name="Alpha">Opacity 0..255.</param>
public readonly record struct PieceRect(int X, int Y, int W, int H, TerrainPalette.Rgb Colour, byte Alpha);
