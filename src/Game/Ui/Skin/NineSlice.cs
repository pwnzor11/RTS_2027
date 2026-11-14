// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Rts.Game.Ui.Skin;

/// <summary>
/// 9-slice drawing (S24): corners keep their pixel size, edges and the centre stretch, every rectangle lands on whole pixels.
/// <see cref="Plan"/> is pure rect maths (unit-tested); <see cref="Draw"/> issues the SpriteBatch calls.
/// </summary>
public static class NineSlice
{
    /// <summary>The source and destination rectangles of the nine patches (row by row; empty patches are left out).</summary>
    /// <param name="texW">Texture width.</param>
    /// <param name="texH">Texture height.</param>
    /// <param name="slice">Margins in source pixels.</param>
    /// <param name="dest">Target rectangle.</param>
    public static IReadOnlyList<(Rectangle Src, Rectangle Dst)> Plan(int texW, int texH, SkinSlice slice, Rectangle dest)
    {
        var result = new List<(Rectangle, Rectangle)>(9);
        if (texW <= 0 || texH <= 0 || dest.Width <= 0 || dest.Height <= 0) return result;
        // Margins cannot exceed the texture; when the target is smaller than the two margins, they shrink proportionally.
        int l = Math.Clamp(slice.Left, 0, texW), r = Math.Clamp(slice.Right, 0, texW - l);
        int t = Math.Clamp(slice.Top, 0, texH), b = Math.Clamp(slice.Bottom, 0, texH - t);
        int dl = l, dr = r, dt = t, db = b;
        if (l + r > dest.Width) { dl = l * dest.Width / (l + r); dr = dest.Width - dl; }
        if (t + b > dest.Height) { dt = t * dest.Height / (t + b); db = dest.Height - dt; }
        int[] sx = { 0, l, texW - r, texW };
        int[] sy = { 0, t, texH - b, texH };
        int[] dx = { dest.X, dest.X + dl, dest.Right - dr, dest.Right };
        int[] dy = { dest.Y, dest.Y + dt, dest.Bottom - db, dest.Bottom };
        for (int row = 0; row < 3; row++)
        {
            for (int col = 0; col < 3; col++)
            {
                var src = new Rectangle(sx[col], sy[row], sx[col + 1] - sx[col], sy[row + 1] - sy[row]);
                var dst = new Rectangle(dx[col], dy[row], dx[col + 1] - dx[col], dy[row + 1] - dy[row]);
                if (src.Width > 0 && src.Height > 0 && dst.Width > 0 && dst.Height > 0) result.Add((src, dst));
            }
        }

        return result;
    }

    /// <summary>Draws <paramref name="texture"/> as a 9-slice into <paramref name="dest"/>.</summary>
    public static void Draw(SpriteBatch batch, Texture2D texture, SkinSlice slice, Rectangle dest, Color tint)
    {
        foreach ((Rectangle src, Rectangle dst) in Plan(texture.Width, texture.Height, slice, dest))
            batch.Draw(texture, dst, src, tint);
    }
}
