// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using System.Diagnostics;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Rts.Sim.Map;

namespace Rts.Game.Render;

/// <summary>
/// Draws a map's decor on top of the baked terrain and under everything else: the decal texture (one quad) and the land-linked sprites
/// (only those in view, found by row range). Built once per map object, never per frame; draws nothing while <see cref="DecorSettings.Enabled"/> is false
/// or when the theater has no <c>[decor]</c> table (the classic look).
/// </summary>
public sealed class DecorLayer
{
    private MapData? _map;
    private Texture2D? _decals;
    private Texture2D? _atlasTexture;
    private IPropAtlas? _atlas;
    private PropInstance[] _props = Array.Empty<PropInstance>();

    /// <summary>Milliseconds the last decal bake took (for the debug line and the contact-sheet notes); 0 before the first bake.</summary>
    public long LastBakeMs { get; private set; }

    /// <summary>Draws the layer. <paramref name="pixel"/> supplies the graphics device; the batch is already begun.</summary>
    public void Draw(SpriteBatch batch, Texture2D pixel, MapData map, Camera camera, Viewport viewport)
    {
        ArgumentNullException.ThrowIfNull(map);
        if (!DecorSettings.Enabled) return;
        if (!ReferenceEquals(_map, map)) Rebuild(pixel.GraphicsDevice, map);

        if (_decals is not null)
        {
            Rectangle dest = camera.ScreenRect(0, 0, map.Width * Camera.CellPx, map.Height * Camera.CellPx);
            batch.Draw(_decals, dest, Color.White);
        }

        if (_atlas is not null && _atlasTexture is not null && _props.Length > 0) DrawProps(batch, map, camera, viewport);
    }

    /// <summary>The number of land-linked sprites on the current map (0 before the first draw).</summary>
    public int PropCount => _props.Length;

    private void DrawProps(SpriteBatch batch, MapData map, Camera camera, Viewport viewport)
    {
        if (!camera.VisibleCells(map.Width, map.Height, viewport, out int x0, out int y0, out int x1, out int y1)) return;
        for (int i = PropPlanner.FirstAtRow(_props, y0 - 1); i < _props.Length; i++)
        {
            PropInstance p = _props[i];
            if (p.CellY > y1 + 1) break;
            if (p.CellX < x0 - 1 || p.CellX > x1 + 1) continue;
            PropRect f = _atlas!.Frame(p.Kind, p.Variant);
            float wx = (p.CellX + 0.5f) * Camera.CellPx + p.OffX - f.OriginX, wy = (p.CellY + 0.5f) * Camera.CellPx + p.OffY - f.OriginY;
            Rectangle dest = camera.ScreenRect(wx, wy, wx + f.Width, wy + f.Height);
            batch.Draw(_atlasTexture!, dest, new Rectangle(f.X, f.Y, f.Width, f.Height), Color.White);
        }
    }

    private void Rebuild(GraphicsDevice device, MapData map)
    {
        long t0 = Stopwatch.GetTimestamp();
        TerrainPalette palette = TerrainPalette.ForMap(map);
        ulong seed = MapHasher.Compute(map);
        _decals?.Dispose();
        _decals = null;
        if (palette.Decor.Decals.Count > 0)
        {
            DecorBake.Result baked = DecorBake.Bake(map, palette, seed);
            if (baked.Count > 0)
            {
                _decals = new Texture2D(device, baked.Width, baked.Height);
                _decals.SetData(baked.Rgba);
            }
        }

        _atlasTexture?.Dispose();
        _atlasTexture = null;
        _atlas = null;
        _props = PropPlanner.Plan(map, palette, seed);
        if (_props.Length > 0)
        {
            _atlas = new ProceduralPropAtlas(palette);
            _atlasTexture = new Texture2D(device, _atlas.Width, _atlas.Height);
            _atlasTexture.SetData(_atlas.Rgba);
        }

        LastBakeMs = (long)Stopwatch.GetElapsedTime(t0).TotalMilliseconds;
        _map = map;
    }
}
