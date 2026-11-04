// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Rts.Game.Sprites;

namespace Rts.Game.Render;

/// <summary>
/// GPU copy of a <see cref="PlaceholderPixels"/>: two textures (BASE and MASK) drawn exactly as a
/// <see cref="SpriteSheet"/> is, so the renderer does not care which kind it has.
/// </summary>
public sealed class PlaceholderSheet : IDisposable
{
    private readonly Texture2D _base;
    private readonly Texture2D _mask;
    private readonly PlaceholderPixels _pixels;
    private bool _disposed;

    /// <summary>Uploads the pixels. The graphics device must outlive the sheet.</summary>
    public PlaceholderSheet(GraphicsDevice device, PlaceholderPixels pixels)
    {
        ArgumentNullException.ThrowIfNull(device);
        _pixels = pixels ?? throw new ArgumentNullException(nameof(pixels));
        _base = new Texture2D(device, pixels.Width, pixels.Height);
        _base.SetData(pixels.Base);
        _mask = new Texture2D(device, pixels.Width, pixels.Height);
        _mask.SetData(pixels.Mask);
    }

    /// <summary>The frame layout this sheet was made with.</summary>
    public SpriteManifest Manifest => _pixels.Manifest;

    /// <summary>
    /// Draws a frame with its origin pixel on <paramref name="position"/>: BASE in white, then MASK tinted with
    /// <paramref name="house"/>. The batch must be begun with the default alpha blend.
    /// </summary>
    /// <param name="batch">Batch in progress.</param>
    /// <param name="frame">Row-major frame index.</param>
    /// <param name="position">Screen position of the origin pixel.</param>
    /// <param name="house">House colour.</param>
    /// <param name="scale">Uniform scale (the camera zoom).</param>
    /// <param name="alpha">Opacity 0..1 (the textures are premultiplied, so this scales every channel).</param>
    public void Draw(SpriteBatch batch, int frame, Vector2 position, Color house, float scale, float alpha = 1f)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(batch);
        if ((uint)frame >= (uint)_pixels.Columns) throw new ArgumentOutOfRangeException(nameof(frame));
        int fw = _pixels.Manifest.FrameW;
        var rect = new Rectangle(frame * fw, 0, fw, _pixels.Height);
        var origin = new Vector2(_pixels.Manifest.OriginX, _pixels.Manifest.OriginY);
        batch.Draw(_base, position, rect, alpha >= 1f ? Color.White : Color.White * alpha, 0f, origin, scale, SpriteEffects.None, 0f);
        batch.Draw(_mask, position, rect, alpha >= 1f ? house : house * alpha, 0f, origin, scale, SpriteEffects.None, 0f);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _base.Dispose();
        _mask.Dispose();
    }
}
