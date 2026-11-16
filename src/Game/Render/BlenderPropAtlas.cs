// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
namespace Rts.Game.Render;

/// <summary>
/// Prop frames modelled and rendered in Blender from primitives by <c>tools/art/props/build_props.py</c> (original, AI-assisted procedural art, docs/CREDITS.md): GlassCrest,
/// IceSlab, WreckHulk and BrokenPylon, three variants each, one PNG per theater under <c>data/sprites/props/blender</c> with a <c>props.toml</c> in the same format as the
/// 0 A.D. atlas (so it reads through <see cref="ZeroAdPropAtlas"/>). Covers only the kinds the manifest lists, so it sits above the procedural atlas in <see cref="PropAtlases"/>.
/// </summary>
public sealed class BlenderPropAtlas : IPropSource
{
    private readonly ZeroAdPropAtlas _inner;

    private BlenderPropAtlas(ZeroAdPropAtlas inner) => _inner = inner;

    /// <inheritdoc />
    public int Width => _inner.Width;

    /// <inheritdoc />
    public int Height => _inner.Height;

    /// <inheritdoc />
    public byte[] Rgba => _inner.Rgba;

    /// <summary>Folder holding <c>props.toml</c>, found by walking up from the executable; null when absent.</summary>
    public static string? FindDirectory()
    {
        string? dir = AppContext.BaseDirectory;
        while (dir is not null)
        {
            string candidate = Path.Combine(dir, "data", "sprites", "props", "blender");
            if (File.Exists(Path.Combine(candidate, "props.toml"))) return candidate;
            dir = Path.GetDirectoryName(dir);
        }

        return null;
    }

    /// <summary>Loads the atlas of a theater, or null when the data folder, the theater's PNG or the manifest is missing or unreadable (the caller then uses the next source).</summary>
    public static BlenderPropAtlas? TryLoad(string theater, string? directory = null)
    {
        try
        {
            string? dir = directory ?? FindDirectory();
            return dir is null ? null : Load(theater, dir);
        }
        catch (Exception e) when (e is IOException or InvalidDataException or UnauthorizedAccessException or Tomlyn.TomlException)
        {
            return null;
        }
    }

    /// <summary>Loads the atlas of a theater from <paramref name="directory"/>. Throws when a file is missing or malformed.</summary>
    public static BlenderPropAtlas Load(string theater, string directory) => new(ZeroAdPropAtlas.Load(theater, directory));

    /// <inheritdoc />
    public bool Covers(PropKind kind) => _inner.Covers(kind);

    /// <inheritdoc />
    public int VariantCount(PropKind kind) => _inner.VariantCount(kind);

    /// <inheritdoc />
    public PropRect Frame(PropKind kind, int variant) => _inner.Frame(kind, variant);
}
