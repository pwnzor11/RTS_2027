// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using System.Diagnostics;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Rts.Sim.Map;

namespace Rts.Game.Render;

/// <summary>
/// Draws a map's decor on top of the baked terrain and under everything else: the decal texture (one quad) and, since M12-02, the
/// land-linked sprites. Built once per map object, never per frame; draws nothing while <see cref="DecorSettings.Enabled"/> is false
/// or when the theater has no <c>[decor]</c> table (the classic look).
/// </summary>
public sealed class DecorLayer
{
    private MapData? _map;
    private Texture2D? _decals;

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
    }

    private void Rebuild(GraphicsDevice device, MapData map)
    {
        long t0 = Stopwatch.GetTimestamp();
        TerrainPalette palette = TerrainPalette.ForMap(map);
        _decals?.Dispose();
        _decals = null;
        if (palette.Decor.Decals.Count > 0)
        {
            DecorBake.Result baked = DecorBake.Bake(map, palette, MapHasher.Compute(map));
            if (baked.Count > 0)
            {
                _decals = new Texture2D(device, baked.Width, baked.Height);
                _decals.SetData(baked.Rgba);
            }
        }

        LastBakeMs = (long)Stopwatch.GetElapsedTime(t0).TotalMilliseconds;
        _map = map;
    }
}
