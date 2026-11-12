// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Rts.Game.Render;

/// <summary>
/// Draws a <see cref="ResolvedSprite"/> whatever its source: a library or legacy sheet is drawn directly, and a
/// placeholder is uploaded to the GPU the first time it is needed. Owns the placeholder textures, so dispose it when
/// the renderer goes away.
/// </summary>
public sealed class SpritePainter : IDisposable
{
    private readonly Dictionary<ResolvedSprite, PlaceholderSheet> _placeholders = new();
    private bool _disposed;

    /// <summary>
    /// Draws one frame with the sprite's origin pixel on <paramref name="origin"/>. The batch must be begun with the
    /// default alpha blend.
    /// </summary>
    /// <param name="batch">Batch in progress.</param>
    /// <param name="sprite">Resolved sprite.</param>
    /// <param name="frame">Row-major frame index from the sprite's manifest.</param>
    /// <param name="origin">Screen position of the origin pixel.</param>
    /// <param name="house">House colour.</param>
    /// <param name="zoom">Camera zoom.</param>
    /// <param name="alpha">Opacity 0..1; 1 draws exactly as before M3.</param>
    public void Draw(SpriteBatch batch, ResolvedSprite sprite, int frame, Vector2 origin, Color house, float zoom, float alpha = 1f)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(batch);
        ArgumentNullException.ThrowIfNull(sprite);
        switch (sprite.Source)
        {
            case SpriteSource.Library:
                sprite.Library!.Sheet(sprite.Id).Draw(batch, frame, origin, house, zoom, alpha);
                break;
            case SpriteSource.Legacy:
                sprite.Legacy!.Draw(batch, frame, origin, house, zoom, alpha);
                break;
            default:
                if (!_placeholders.TryGetValue(sprite, out PlaceholderSheet? sheet))
                {
                    sheet = new PlaceholderSheet(batch.GraphicsDevice, sprite.Placeholder!);
                    _placeholders.Add(sprite, sheet);
                }
                sheet.Draw(batch, frame, origin, house, zoom, alpha);
                break;
        }
    }

    /// <summary>M10-14: adds the hit flash over a frame just drawn with <see cref="Draw"/> (library and legacy sheets; placeholders do not flash).</summary>
    public void DrawFlash(SpriteBatch batch, ResolvedSprite sprite, int frame, Vector2 origin, float zoom)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        switch (sprite.Source)
        {
            case SpriteSource.Library:
                sprite.Library!.Sheet(sprite.Id).DrawFlash(batch, frame, origin, zoom);
                break;
            case SpriteSource.Legacy:
                sprite.Legacy!.DrawFlash(batch, frame, origin, zoom);
                break;
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        foreach (PlaceholderSheet sheet in _placeholders.Values) sheet.Dispose();
        _placeholders.Clear();
    }
}
