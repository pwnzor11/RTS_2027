// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Rts.Sim.Map;
using Rts.Sim.Rules;

namespace Rts.Game.Render;

/// <summary>Draws the <see cref="RoadPieces"/> of the map over the baked terrain. The plan is made once per map; each frame only the cells in view are drawn. View only.</summary>
public sealed class RoadPieceLayer
{
    private MapData? _map;
    private List<RoadCell> _plan = new();
    private TerrainPalette? _palette;
    private readonly List<PieceRect> _ops = new();

    /// <summary>Draws the pieces for the visible part of <paramref name="map"/>.</summary>
    public void Draw(SpriteBatch batch, Texture2D pixel, MapData map, Camera camera, Viewport viewport)
    {
        if (!ReferenceEquals(_map, map))
        {
            _map = map;
            _palette = TerrainPalette.ForMap(map);
            _plan = RoadPieces.Plan(map.Land, map.Width, map.Height);
        }

        if (_plan.Count == 0 || _palette is null) return;
        if (!camera.VisibleCells(map.Width, map.Height, viewport, out int x0, out int y0, out int x1, out int y1)) return;
        var (road, edge, edgeT) = Colours(_palette);
        TerrainPalette palette = _palette;
        foreach (RoadCell c in _plan)
        {
            if (c.X < x0 - 1 || c.X > x1 + 1 || c.Y < y0 - 1 || c.Y > y1 + 1) continue;
            _ops.Clear();
            RoadPieces.Ops(c, map.Land, map.Width, map.Height, palette.TexelCells, road, edge, edgeT, l => palette.ShadesFor(l).Base, _ops);
            foreach (PieceRect r in _ops)
                Shapes.Fill(batch, pixel, camera.ScreenRect(r.X, r.Y, r.X + r.W, r.Y + r.H), Shapes.Alpha(new Color(r.Colour.R, r.Colour.G, r.Colour.B), r.Alpha));
        }
    }

    /// <summary>The road colour, its edge line colour and the line width of a palette, as the bake uses them (edge falls back to the road's dark shade).</summary>
    public static (TerrainPalette.Rgb Road, TerrainPalette.Rgb Edge, int EdgeTexels) Colours(TerrainPalette palette)
    {
        TerrainPalette.LandShades shades = palette.ShadesFor(LandType.Road);
        return (shades.Base, palette.Style.RoadEdge ?? shades.Dark, palette.Style.RoadEdgeTexels);
    }
}
