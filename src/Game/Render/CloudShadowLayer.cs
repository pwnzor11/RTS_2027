// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Rts.Game.Render;

/// <summary>
/// Draws the <see cref="CloudShadows"/> blobs as stretched soft-edged discs over the world (terrain, buildings and units), under the fog and the HUD.
/// The soft texture is built once, from an integer falloff, the first time it is needed. View only.
/// </summary>
public sealed class CloudShadowLayer : IDisposable
{
    private const int TexSize = 64;
    private Texture2D? _soft;
    private CloudShadows.Blob[] _blobs = Array.Empty<CloudShadows.Blob>();
    private int _blobsW = -1, _blobsH = -1;

    /// <summary>Builds the RGBA bytes of the blob texture: black, alpha falling smoothly from 255 at the centre to 0 at the rim (premultiplied, so RGB is 0).</summary>
    public static byte[] BuildSoftTexture(int size = TexSize)
    {
        var rgba = new byte[size * size * 4];
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                long dx = 2L * x + 1 - size, dy = 2L * y + 1 - size;           // from the centre, in half texels
                long d2 = dx * dx + dy * dy, r2 = (long)size * size;           // rim at distance size (half texels)
                int a = d2 >= r2 ? 0 : (int)(255L * (r2 - d2) / r2);
                a = a * a / 255;                                               // a quadratic falloff, softer at the rim
                rgba[(y * size + x) * 4 + 3] = (byte)a;
            }
        }

        return rgba;
    }

    /// <summary>Draws the blobs that touch the view.</summary>
    public void Draw(SpriteBatch batch, int mapWidth, int mapHeight, int tick, WeatherKind weather, Camera camera, Viewport viewport)
    {
        if (_soft is null || _soft.IsDisposed || _soft.GraphicsDevice != batch.GraphicsDevice)
        {
            _soft?.Dispose();
            _soft = new Texture2D(batch.GraphicsDevice, TexSize, TexSize);
            _soft.SetData(BuildSoftTexture());
        }

        if (_blobsW != mapWidth || _blobsH != mapHeight)
        {
            _blobs = CloudShadows.Layout(mapWidth, mapHeight);
            _blobsW = mapWidth;
            _blobsH = mapHeight;
        }

        Vector2 wind = CloudShadows.Wind(weather);
        Vector2 topLeft = camera.Position;
        Vector2 bottomRight = topLeft + new Vector2(viewport.Width, viewport.Height) / camera.Zoom;
        foreach (CloudShadows.Blob b in _blobs)
        {
            Vector2 c = CloudShadows.Position(b, tick, wind, mapWidth, mapHeight);
            if (!CloudShadows.Touches(b, c, topLeft.X, topLeft.Y, bottomRight.X, bottomRight.Y)) continue;
            Rectangle dest = camera.ScreenRect(c.X - b.RadiusX, c.Y - b.RadiusY, c.X + b.RadiusX, c.Y + b.RadiusY);
            byte a = CloudShadows.AlphaOf(b);
            batch.Draw(_soft, dest, new Color(0, 0, 0, (int)a));   // black with alpha: premultiplied, so only the alpha counts
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _soft?.Dispose();
        _soft = null;
    }
}
