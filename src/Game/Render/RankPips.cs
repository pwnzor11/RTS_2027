// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Rts.Sim.Rules;

namespace Rts.Game.Render;

/// <summary>
/// Veterancy rank pips (VET-5): one warm-gold chevron per rank (1..3), drawn just left of a unit's health bar. Recruits get none.
/// Each chevron is a five-by-three pixel V, scaled by the camera zoom.
/// </summary>
public static class RankPips
{
    /// <summary>Most chevrons drawn (the top rank).</summary>
    public const int MaxPips = VeterancyRules.Ranks;

    private const int ChevronWidthPx = 5;
    private const int ChevronHeightPx = 3;
    private const int GapPx = 1;
    private const int BarGapPx = 3;
    private static readonly Color Gold = new(0xFF, 0xC8, 0x3C);

    /// <summary>Chevrons for a rank: the rank itself, clamped to 0..<see cref="MaxPips"/>.</summary>
    public static int PipCount(int rank) => Math.Clamp(rank, 0, MaxPips);

    /// <summary>Draws the chevrons of <paramref name="rank"/> to the left of <paramref name="bar"/>, vertically centred on it.</summary>
    /// <param name="batch">Batch in progress.</param>
    /// <param name="pixel">1x1 white texture.</param>
    /// <param name="bar">The health bar rectangle the pips sit beside.</param>
    /// <param name="rank">Veterancy rank 0..3.</param>
    /// <param name="zoom">Camera zoom (pixel size scales with it).</param>
    public static void Draw(SpriteBatch batch, Texture2D pixel, Rectangle bar, int rank, float zoom)
    {
        int count = PipCount(rank);
        if (count == 0) return;
        int px = Math.Max(1, (int)MathF.Round(zoom));
        int w = ChevronWidthPx * px;
        int h = ChevronHeightPx * px;
        int gap = GapPx * px;
        int total = count * w + (count - 1) * gap;
        int x = bar.X - BarGapPx * px - total;
        int y = bar.Y + (bar.Height - h) / 2;
        for (int c = 0; c < count; c++)
        {
            int left = x + c * (w + gap);
            for (int row = 0; row < ChevronHeightPx; row++)
            {
                int top = y + row * px;
                int leftPixel = left + row * px;
                int rightPixel = left + (ChevronWidthPx - 1 - row) * px;
                Shapes.Fill(batch, pixel, new Rectangle(leftPixel, top, px, px), Gold);
                if (rightPixel != leftPixel) Shapes.Fill(batch, pixel, new Rectangle(rightPixel, top, px, px), Gold);
            }
        }
    }
}
