// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Rts.Sim.Map;

namespace Rts.Game.Render;

/// <summary>
/// Draws a map from one texture baked by <see cref="TerrainBake"/> with the palette of the map's theater
/// (<see cref="TerrainPalette.ForMap"/>). Baked once per map object, never per frame. During play, when the live sim changes a
/// cell (a destroyed prop, M12-16), only that cell and its neighbours are repainted in the texture.
/// </summary>
public sealed class ThemedTerrainLook
{
    private MapData? _map;
    private Texture2D? _texture;
    private TerrainPalette? _palette;
    private CellGrid? _shown;                  // the land, overlay and density the texture shows
    private MapState? _liveSynced;             // the live state last compared with _shown ...
    private int _liveTick = int.MinValue;      // ... and at which tick
    private readonly List<int> _changed = new();

    /// <summary>
    /// Draws the whole map at the camera's scale. <paramref name="pixel"/> supplies the graphics device. <paramref name="live"/> and
    /// <paramref name="tick"/> (the snapshot's) let the texture follow cells the sim changed; null draws the starting map.
    /// </summary>
    public void Draw(SpriteBatch batch, Texture2D pixel, MapData map, Camera camera, MapState? live = null, int tick = 0)
    {
        ArgumentNullException.ThrowIfNull(map);
        if (!ReferenceEquals(_map, map)) Rebuild(pixel.GraphicsDevice, map);
        if (live is not null) Refresh(live, tick);

        Rectangle dest = camera.ScreenRect(0, 0, map.Width * Camera.CellPx, map.Height * Camera.CellPx);
        batch.Draw(_texture!, dest, Color.White);
    }

    private void Rebuild(GraphicsDevice device, MapData map)
    {
        TerrainPalette palette = TerrainPalette.ForMap(map);
        var shown = CellGrid.FromMap(map);
        TerrainBake.Result baked = TerrainBake.Bake(shown, palette);
        var texture = new Texture2D(device, baked.Width, baked.Height);
        texture.SetData(baked.Rgba);
        _texture?.Dispose();
        _texture = texture;
        _map = map;
        _palette = palette;
        _shown = shown;
        _liveSynced = null;
    }

    /// <summary>Once per sim tick: repaints the cells whose live land, overlay or density differ from the texture, and their neighbours.</summary>
    private void Refresh(MapState live, int tick)
    {
        if (ReferenceEquals(live, _liveSynced) && tick == _liveTick) return;
        _liveSynced = live;
        _liveTick = tick;
        if (_texture is null || _palette is null || _shown is null) return;

        _changed.Clear();
        if (_shown.Collect(live, _changed) == 0) return;
        int s = _palette.TexelCells;
        foreach (int i in CellGrid.RepaintSet(_shown.Width, _shown.Height, _changed))
        {
            int cx = i % _shown.Width, cy = i / _shown.Width;
            byte[] rgba = TerrainBake.PaintCell(_palette, _shown, cx, cy);
            _texture.SetData(0, new Rectangle(cx * s, cy * s, s, s), rgba, 0, rgba.Length);
        }
    }
}
