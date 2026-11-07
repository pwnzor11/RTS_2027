// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Rts.Game.Ui;

namespace Rts.Game.Render;

/// <summary>The crosshair and label drawn at the cursor while an order (attack-move) is armed (audit O2).</summary>
public static class ArmedCursor
{
    private static readonly Color Tint = new(0xE8, 0x40, 0x30);

    /// <summary>Draws a red crosshair centred on <paramref name="mouse"/> and <paramref name="label"/> to its lower right.</summary>
    public static void Draw(SpriteBatch batch, Texture2D pixel, Point mouse, string label, int uiScale)
    {
        int arm = 6 * uiScale, gap = 2 * uiScale, t = Math.Max(1, uiScale);
        Color shadow = new(0, 0, 0, 200);
        for (int pass = 0; pass < 2; pass++)
        {
            Color c = pass == 0 ? shadow : Tint;
            int o = pass == 0 ? t : 0;
            batch.Draw(pixel, new Rectangle(mouse.X - arm + o, mouse.Y - t / 2 + o, arm - gap, t), c);
            batch.Draw(pixel, new Rectangle(mouse.X + gap + o, mouse.Y - t / 2 + o, arm - gap, t), c);
            batch.Draw(pixel, new Rectangle(mouse.X - t / 2 + o, mouse.Y - arm + o, t, arm - gap), c);
            batch.Draw(pixel, new Rectangle(mouse.X - t / 2 + o, mouse.Y + gap + o, t, arm - gap), c);
        }

        var at = new Vector2(mouse.X + arm + 2, mouse.Y + arm + 2);
        BitmapFont.Draw(batch, pixel, label, at + new Vector2(1, 1), shadow, uiScale);
        BitmapFont.Draw(batch, pixel, label, at, Tint, uiScale);
    }
}
