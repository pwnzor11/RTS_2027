// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Rts.Game.Controls;

namespace Rts.Game.Render;

/// <summary>Draws the <see cref="CursorArt"/> sprites at the mouse as crisp rectangles (no textures), and makes the invisible OS cursor.</summary>
public static class SoftwareCursor
{
    /// <summary>Draws <paramref name="kind"/> with its hot spot on <paramref name="mouse"/>, each art pixel <paramref name="scale"/> screen pixels wide.</summary>
    public static void Draw(SpriteBatch batch, Texture2D pixel, CursorKind kind, Point mouse, int scale)
    {
        scale = Math.Max(1, scale);
        CursorArt.Sprite s = CursorArt.Of(kind);
        int ox = mouse.X - s.HotX * scale, oy = mouse.Y - s.HotY * scale;
        for (int y = 0; y < CursorArt.Size; y++)
        {
            int x = 0;
            while (x < CursorArt.Size)
            {
                Color c = s.Pixels[y * CursorArt.Size + x];
                if (c.A == 0) { x++; continue; }
                int run = 1;
                while (x + run < CursorArt.Size && s.Pixels[y * CursorArt.Size + x + run] == c) run++;
                batch.Draw(pixel, new Rectangle(ox + x * scale, oy + y * scale, run * scale, scale), c);
                x += run;
            }
        }
    }

    /// <summary>A fully transparent OS cursor, or null when no windowing backend is available (tests, headless).</summary>
    public static MouseCursor? MakeHidden(GraphicsDevice device)
    {
        try
        {
            var texture = new Texture2D(device, 1, 1);
            texture.SetData(new[] { Color.Transparent });
            return MouseCursor.FromTexture2D(texture, 0, 0);
        }
        catch (Exception e) when (e is not OutOfMemoryException)
        {
            return null;
        }
    }
}
