// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).

namespace Rts.Game.Render;

/// <summary>
/// How one kind of weather looks and moves, in screen pixels at a 1080 px tall view (the renderer scales sizes up for 4K views).
/// </summary>
/// <param name="PerHundredKilopixels">Particles per 100 000 screen pixels.</param>
/// <param name="VelocityX">Horizontal drift in pixels per second (negative = left).</param>
/// <param name="VelocityY">Fall speed in pixels per second.</param>
/// <param name="SizeMin">Smallest particle edge in pixels.</param>
/// <param name="SizeMax">Largest particle edge in pixels.</param>
/// <param name="Streak">Extra length along the fall direction in pixels (rain), 0 for dots.</param>
/// <param name="Sway">Side-to-side wobble in pixels (snow, ash), 0 for none.</param>
/// <param name="Rgb">Colour 0xRRGGBB.</param>
/// <param name="MaxAlpha">Opacity of the nearest particles, 0..255.</param>
public readonly record struct WeatherProfile(
    float PerHundredKilopixels, float VelocityX, float VelocityY, int SizeMin, int SizeMax, int Streak, float Sway, uint Rgb, int MaxAlpha);
