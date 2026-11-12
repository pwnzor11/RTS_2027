// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Rts.Game.Render;

/// <summary>Draws the weather particles of the current map over the world view (M10-12). View only; see <see cref="WeatherModel"/>.</summary>
public static class WeatherLayer
{
    /// <summary>Draws the layer into <paramref name="view"/>. Does nothing for <see cref="WeatherKind.None"/>.</summary>
    /// <param name="batch">Batch in progress.</param>
    /// <param name="pixel">1x1 white texture.</param>
    /// <param name="kind">Weather of the map.</param>
    /// <param name="view">Screen rectangle of the map view.</param>
    /// <param name="seconds">Presentation clock in seconds.</param>
    /// <param name="camX">Camera x in screen pixels (world x times zoom).</param>
    /// <param name="camY">Camera y in screen pixels.</param>
    public static void Draw(SpriteBatch batch, Texture2D pixel, WeatherKind kind, Rectangle view, double seconds, double camX, double camY)
    {
        int n = WeatherModel.Count(kind, view.Width, view.Height);
        if (n == 0) return;
        WeatherProfile prof = WeatherModel.ProfileOf(kind);
        var rgb = new Color((int)(prof.Rgb >> 16 & 0xFF), (int)(prof.Rgb >> 8 & 0xFF), (int)(prof.Rgb & 0xFF));
        for (int i = 0; i < n; i++)
        {
            WeatherParticle p = WeatherModel.Place(kind, i, seconds, camX, camY, view.Width, view.Height);
            Shapes.Fill(batch, pixel, new Rectangle(view.X + p.X, view.Y + p.Y, p.Width, p.Height), Shapes.Alpha(rgb, (byte)p.Alpha));
        }
    }
}
