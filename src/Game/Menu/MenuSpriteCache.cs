// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Rts.Game.Sprites;

namespace Rts.Game.Menu;

/// <summary>
/// The paint ramp used to recolour sprites in the menu scenes. A sprite manifest's own <c>[house]</c> ramp wins; a sprite whose manifest
/// has none (the building sheets do not declare one yet, though their paint patches use the standard ramp) gets the standard one,
/// so no raw magenta shows in the menu.
/// </summary>
public static class HouseRamp
{
    /// <summary>The standard 8-shade house ramp, darkest first (the one in the infantry and vehicle manifests).</summary>
    // ASSUMED (M3-16): the building sheets should use this ramp too (their paint pixels match it exactly).
    public static readonly uint[] Standard =
    {
        0x400040, 0x5B125B, 0x772577, 0x923792, 0xAD49AD, 0xC85BC8, 0xE46EE4, 0xFF80FF,
    };

    /// <summary>The ramp to split <paramref name="manifest"/>'s sheet with.</summary>
    public static uint[] For(SpriteManifest manifest) => manifest.HouseRamp.Count > 0 ? manifest.HouseRamp.ToArray() : Standard;
}

/// <summary>
/// Sprite textures for the menu scenes, recoloured once per (sprite, house colour): the sheet is split with the house ramp, tinted
/// and premultiplied into one texture. Unlike drawing through <see cref="SpriteSheet"/> this lets the scene dim a sprite, and gives the
/// buildings their paint colour. Textures are made on first use; the cache owns them.
/// </summary>
public sealed class MenuSpriteCache : IDisposable
{
    private readonly GraphicsDevice _device;
    private readonly SpriteLibrary? _library;
    private readonly Dictionary<(string, uint), Texture2D?> _textures = new();

    /// <summary>Creates a cache over the library's manifests (the PNGs are read from beside them).</summary>
    public MenuSpriteCache(GraphicsDevice device, SpriteLibrary? library)
    {
        _device = device;
        _library = library;
    }

    /// <summary>The recoloured sheet and its manifest, or false when the sprite is unknown or cannot be read.</summary>
    public bool TryGet(string id, Rgba house, out Texture2D texture, out SpriteManifest manifest)
    {
        texture = null!;
        manifest = null!;
        if (_library is null || !_library.Ids.Contains(id)) return false;
        manifest = _library.Manifest(id);
        uint key = ((uint)house.R << 16) | ((uint)house.G << 8) | house.B;
        if (!_textures.TryGetValue((id, key), out Texture2D? tex))
        {
            tex = Build(manifest, house);
            _textures[(id, key)] = tex;
        }
        if (tex is null) return false;
        texture = tex;
        return true;
    }

    private Texture2D? Build(SpriteManifest m, Rgba house)
    {
        try
        {
            string path = Path.Combine(Path.GetDirectoryName(m.SourcePath) ?? ".", m.SheetFile);
            using FileStream stream = File.OpenRead(path);
            using Texture2D source = Texture2D.FromStream(_device, stream);
            var pixels = new byte[source.Width * source.Height * 4];
            source.GetData(pixels);
            var baseP = new byte[pixels.Length];
            var mask = new byte[pixels.Length];
            HouseSplit.Split(pixels, HouseRamp.For(m), baseP, mask);
            HouseSplit.Premultiply(baseP);
            HouseSplit.Premultiply(mask);
            var composed = new byte[pixels.Length];
            HouseSplit.Compose(baseP, mask, house.ToXna(), composed);
            var tex = new Texture2D(_device, source.Width, source.Height);
            tex.SetData(composed);
            return tex;
        }
        catch (Exception e) when (e is IOException or InvalidDataException or InvalidOperationException or UnauthorizedAccessException)
        {
            Console.Error.WriteLine($"menu sprite {m.Id}: {e.Message}");
            return null;
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        foreach (Texture2D? t in _textures.Values) t?.Dispose();
        _textures.Clear();
    }
}
