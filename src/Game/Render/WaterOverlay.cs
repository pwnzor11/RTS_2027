// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Rts.Sim.Core;
using Rts.Sim.Map;
using Rts.Sim.Rules;

namespace Rts.Game.Render;

/// <summary>
/// Animated water (M10-13), drawn over the baked terrain and under buildings and units: slow glints on water cells, pulsing foam along the
/// shore, and a fading wake behind moving ships. The terrain bake is untouched; this only adds a few small rectangles per visible water cell.
/// The shore masks are built once per map. View only; nothing here reaches the sim.
/// </summary>
public sealed class WaterOverlay
{
    private static readonly Color Glint = new(0xD8, 0xEC, 0xF4);
    private static readonly Color Foam = new(0xEE, 0xF4, 0xF2);
    private static readonly Color Wake = new(0xE4, 0xF0, 0xF2);

    private readonly WaterWake _wake = new();
    private readonly List<(EntityId Id, Vector2 World)> _ships = new();
    private MapData? _map;
    private byte[] _shore = Array.Empty<byte>();

    /// <summary>The wake bookkeeping (exposed for tests).</summary>
    public WaterWake Trail => _wake;

    /// <summary>Forgets wakes and the shore cache (a new game).</summary>
    public void Reset()
    {
        _wake.Reset();
        _map = null;
    }

    /// <summary>Draws glints, foam and wakes for the visible part of <paramref name="map"/>.</summary>
    public void Draw(SpriteBatch batch, Texture2D pixel, MapData map, SceneFrame frame, Camera camera, Viewport viewport)
    {
        if (!ReferenceEquals(_map, map))
        {
            _map = map;
            _shore = WaterShore.Build(map.Land, map.Width, map.Height);
            _wake.Reset();
        }

        int tick = frame.Snapshot.Tick;
        if (camera.VisibleCells(map.Width, map.Height, viewport, out int x0, out int y0, out int x1, out int y1))
        {
            for (int y = y0; y <= y1; y++)
            {
                for (int x = x0; x <= x1; x++)
                {
                    if (!WaterShore.IsWater(map.Land[y * map.Width + x])) continue;
                    DrawCell(batch, pixel, camera, x, y, _shore[y * map.Width + x], tick);
                }
            }
        }

        CollectShips(frame, map);
        _wake.Update(tick, _ships);
        foreach (WakePuff p in _wake.Puffs)
        {
            int age = tick - p.Born;
            byte alpha = WaterWake.AlphaAt(age);
            if (alpha == 0) continue;
            float r = WaterWake.RadiusAt(age);
            Vector2 perp = new(-p.Dir.Y, p.Dir.X);
            float spread = 3f + age * 0.5f;
            for (int side = -1; side <= 1; side += 2)
            {
                Vector2 c = p.Pos + perp * (spread * side);
                Shapes.Fill(batch, pixel, camera.ScreenRect(c.X - r, c.Y - r, c.X + r, c.Y + r), Shapes.Alpha(Wake, alpha));
            }
        }
    }

    private void DrawCell(SpriteBatch batch, Texture2D pixel, Camera camera, int x, int y, byte shore, int tick)
    {
        float left = x * Camera.CellPx, top = y * Camera.CellPx, size = Camera.CellPx;
        for (int i = 0; i < WaterWaves.GlintsPerCell; i++)
        {
            byte a = WaterWaves.Glint(x, y, i, tick, out Rectangle g);
            if (a > 0) Shapes.Fill(batch, pixel, camera.ScreenRect(left + g.X, top + g.Y, left + g.Right, top + g.Bottom), Shapes.Alpha(Glint, a));
        }

        if (shore == 0) return;
        float w = WaterWaves.FoamWidthPx(x, y, tick);
        Color c = Shapes.Alpha(Foam, WaterWaves.FoamAlpha(x, y, tick));
        if ((shore & WaterShore.North) != 0) Shapes.Fill(batch, pixel, camera.ScreenRect(left, top, left + size, top + w), c);
        if ((shore & WaterShore.South) != 0) Shapes.Fill(batch, pixel, camera.ScreenRect(left, top + size - w, left + size, top + size), c);
        if ((shore & WaterShore.West) != 0) Shapes.Fill(batch, pixel, camera.ScreenRect(left, top, left + w, top + size), c);
        if ((shore & WaterShore.East) != 0) Shapes.Fill(batch, pixel, camera.ScreenRect(left + size - w, top, left + size, top + size), c);
    }

    private void CollectShips(SceneFrame frame, MapData map)
    {
        _ships.Clear();
        if (frame.Rules is not { } rules) return;
        foreach (RenderUnit u in frame.Units)
        {
            UnitView v = u.View;
            if (v.Kind != UnitKind.Vehicle || (uint)v.TypeIndex >= (uint)rules.Vehicles.Count) continue;
            if (rules.Vehicles[v.TypeIndex].Locomotion != Locomotion.Naval) continue;
            CellCoord c = Camera.CellAt(u.World);
            if ((uint)c.X >= (uint)map.Width || (uint)c.Y >= (uint)map.Height || !WaterShore.IsWater(map.Land[c.Y * map.Width + c.X])) continue;
            _ships.Add((v.Id, u.World));
        }
    }
}
