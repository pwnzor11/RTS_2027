// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).

namespace Rts.Game.Render;

/// <summary>One weather particle placed for a frame: top-left on screen, size, streak and opacity.</summary>
/// <param name="X">Screen x of the top-left corner.</param>
/// <param name="Y">Screen y of the top-left corner.</param>
/// <param name="Width">Width in pixels.</param>
/// <param name="Height">Height in pixels (width plus the streak).</param>
/// <param name="Alpha">Opacity 0..255.</param>
public readonly record struct WeatherParticle(int X, int Y, int Width, int Height, int Alpha);
