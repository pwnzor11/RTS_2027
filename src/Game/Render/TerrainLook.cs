// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Rts.Sim.Map;

namespace Rts.Game.Render;

/// <summary>
/// Draws a Rust Belt map from one texture baked by <see cref="TerrainBake"/>. The bake runs once when the map first
/// appears and again only if a different map object is drawn; nothing is baked per frame.
/// </summary>
public sealed class TerrainLook
{
    private static readonly Lazy<TerrainPalette> SharedPalette = new(() =>
        TerrainPalette.Load(Path.Combine(AppContext.BaseDirectory, "data", "theaters", "rustbelt.toml")));

    private MapData? _map;
    private Texture2D? _texture;

    /// <summary>The Rust Belt palette, loaded from the game data on first use.</summary>
    public static TerrainPalette Palette => SharedPalette.Value;

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
        TerrainBake.Result baked = TerrainBake.Bake(map, Palette);
        var texture = new Texture2D(device, baked.Width, baked.Height);
        texture.SetData(baked.Rgba);
        _texture?.Dispose();
        _texture = texture;
        _map = map;
    }
}
