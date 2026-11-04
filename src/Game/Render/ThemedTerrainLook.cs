// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Rts.Sim.Map;

namespace Rts.Game.Render;

/// <summary>
/// Draws a map from one texture baked by <see cref="TerrainBake"/> with the palette of the map's theater
/// (<see cref="TerrainPalette.ForMap"/>). Baked once per map object, never per frame.
/// </summary>
public sealed class ThemedTerrainLook
{
    private MapData? _map;
    private Texture2D? _texture;

    /// <summary>Draws the whole map at the camera's scale. <paramref name="pixel"/> supplies the graphics device.</summary>
    public void Draw(SpriteBatch batch, Texture2D pixel, MapData map, Camera camera)
    {
        ArgumentNullException.ThrowIfNull(map);
        if (!ReferenceEquals(_map, map)) Rebuild(pixel.GraphicsDevice, map);

        Rectangle dest = camera.ScreenRect(0, 0, map.Width * Camera.CellPx, map.Height * Camera.CellPx);
        batch.Draw(_texture!, dest, Color.White);
    }

    private void Rebuild(GraphicsDevice device, MapData map)
    {
        TerrainBake.Result baked = TerrainBake.Bake(map, TerrainPalette.ForMap(map));
        var texture = new Texture2D(device, baked.Width, baked.Height);
        texture.SetData(baked.Rgba);
        _texture?.Dispose();
        _texture = texture;
        _map = map;
    }
}
