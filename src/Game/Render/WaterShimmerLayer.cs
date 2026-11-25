// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Rts.Sim.Core;
using Rts.Sim.Map;

namespace Rts.Game.Render;

/// <summary>Draws the <see cref="WaterShimmer"/> sparkles for the lit, visible water cells in one pass of small rectangles. View only.</summary>
public sealed class WaterShimmerLayer
{
    private readonly List<WaterShimmer.LitCell> _lit = new();
    private MapData? _map;
    private Color _colour = Color.White;
    private TerrainPalette.Rgb _rgb;

    /// <summary>Draws the shimmer over the water of the visible part of <paramref name="map"/>.</summary>
    public void Draw(SpriteBatch batch, Texture2D pixel, MapData map, SimSnapshot snapshot, Camera camera, Viewport viewport)
    {
        if (!ReferenceEquals(_map, map))
        {
            _map = map;
            _rgb = WaterShimmer.ColourFor(TerrainPalette.ForMap(map));
            _colour = new Color(_rgb.R, _rgb.G, _rgb.B);
        }

        if (!camera.VisibleCells(map.Width, map.Height, viewport, out int x0, out int y0, out int x1, out int y1)) return;
        _lit.Clear();
        IReadOnlyList<byte>? vision = snapshot.Viewer >= 0 ? snapshot.Vision : null;
        WaterShimmer.Select(map.Land, map.Width, map.Height, vision, x0, y0, x1, y1, snapshot.Tick, _lit);
        foreach (WaterShimmer.LitCell c in _lit)
        {
            Color col = Shapes.Alpha(_colour, c.Alpha);
            float left = c.X * Camera.CellPx, top = c.Y * Camera.CellPx;
            for (int i = 0; i < WaterShimmer.SparklesPerCell; i++)
            {
                Rectangle r = WaterShimmer.Sparkle(c.X, c.Y, i, snapshot.Tick);
                Shapes.Fill(batch, pixel, camera.ScreenRect(left + r.X, top + r.Y, left + r.Right, top + r.Bottom), col);
            }
        }
    }
}
