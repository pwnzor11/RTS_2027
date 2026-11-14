// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Rts.Game.Ui.Card;

/// <summary>
/// Loads <c>data/portraits/&lt;id&gt;.png</c> lazily for the selection card (M10-16). Each id is looked up once; a missing or invalid
/// file is remembered as null and never retried.
/// </summary>
public sealed class PortraitProvider : IDisposable
{
    private readonly GraphicsDevice _device;
    private readonly string _root;
    private readonly Dictionary<string, Texture2D?> _cache = new(StringComparer.Ordinal);

    /// <summary>Creates the provider.</summary>
    /// <param name="device">Graphics device.</param>
    /// <param name="root">The portrait folder (see <see cref="PortraitFiles.RootFor"/>).</param>
    public PortraitProvider(GraphicsDevice device, string root)
    {
        _device = device;
        _root = root;
    }

    /// <summary>The portrait texture for a rules id, or null when it has none.</summary>
    /// <param name="id">Rules id.</param>
    public Texture2D? Get(string id)
    {
        if (_cache.TryGetValue(id, out Texture2D? cached)) return cached;
        Texture2D? tex = null;
        if (PortraitFiles.Resolve(_root, id) is { } path)
        {
            try
            {
                using FileStream stream = File.OpenRead(path);
                tex = Texture2D.FromStream(_device, stream);
            }
            catch (Exception e) when (e is IOException or InvalidOperationException or UnauthorizedAccessException)
            {
                tex = null;
            }
        }

        _cache[id] = tex;
        return tex;
    }

    /// <summary>Draws the portrait scaled to fill <paramref name="area"/>; false when the id has none.</summary>
    /// <param name="batch">Sprite batch (already begun).</param>
    /// <param name="id">Rules id.</param>
    /// <param name="area">Target rectangle.</param>
    public bool TryDraw(SpriteBatch batch, string id, Rectangle area)
    {
        if (area.Width <= 0 || area.Height <= 0 || Get(id) is not { } tex) return false;
        batch.Draw(tex, area, Color.White);
        return true;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        foreach (Texture2D? t in _cache.Values) t?.Dispose();
        _cache.Clear();
    }
}
