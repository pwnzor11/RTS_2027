// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Rts.Game.Screens;

/// <summary>Full-window fills shared by the menu screens.</summary>
internal static class Backdrop
{
    private static readonly Color Opaque = new(0x0E, 0x10, 0x0C);
    private static readonly Color Shade = new(0, 0, 0, 120);
    private static readonly Color Dim = new(0, 0, 0, 150);

    /// <summary>
    /// Covers the whole window with the menu background: the animated scene dimmed a little so the panel on top reads
    /// clearly, or a plain colour when there is no scene (headless runs).
    /// </summary>
    public static void FillOpaque(SpriteBatch sb, GameServices s)
    {
        if (s.Background is { } background)
        {
            background.Draw(sb, s.Viewport);
            sb.Draw(s.Pixel, new Rectangle(0, 0, s.Viewport.Width, s.Viewport.Height), Shade);
            return;
        }
        sb.Draw(s.Pixel, new Rectangle(0, 0, s.Viewport.Width, s.Viewport.Height), Opaque);
    }

    /// <summary>Darkens whatever is drawn below.</summary>
    public static void FillDim(SpriteBatch sb, GameServices s) =>
        sb.Draw(s.Pixel, new Rectangle(0, 0, s.Viewport.Width, s.Viewport.Height), Dim);
}
