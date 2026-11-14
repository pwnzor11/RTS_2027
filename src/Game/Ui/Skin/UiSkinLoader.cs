// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Microsoft.Xna.Framework.Graphics;

namespace Rts.Game.Ui.Skin;

/// <summary>
/// Loads and caches faction UI skins and their textures (S24). A missing folder, file or key gives null for that piece, and the
/// caller draws it procedurally. Never throws on bad data.
/// </summary>
public sealed class UiSkinLoader
{
    /// <summary>The skin folder name for a faction name (authority, directorate, left_behind, expedition); null for any other.</summary>
    public static string? FolderFor(Rts.Sim.Rules.Faction faction) => faction switch
    {
        Rts.Sim.Rules.Faction.Authority => "authority",
        Rts.Sim.Rules.Faction.Directorate => "directorate",
        Rts.Sim.Rules.Faction.LeftBehind => "left_behind",
        Rts.Sim.Rules.Faction.Expedition => "expedition",
        _ => null,
    };

    private readonly string _root;
    private readonly GraphicsDevice? _device;
    private readonly Dictionary<string, UiSkin?> _skins = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Texture2D?> _textures = new(StringComparer.Ordinal);

    /// <summary>Creates a loader.</summary>
    /// <param name="skinsRoot">The <c>data/ui/skins</c> folder.</param>
    /// <param name="device">Graphics device for textures; null = parsing only (tests).</param>
    public UiSkinLoader(string skinsRoot, GraphicsDevice? device = null)
    {
        _root = skinsRoot;
        _device = device;
    }

    /// <summary>The skin of a faction folder, cached; null when the folder or its skin.toml is missing or unreadable.</summary>
    public UiSkin? Skin(string? folder)
    {
        if (string.IsNullOrEmpty(folder)) return null;
        if (_skins.TryGetValue(folder, out UiSkin? cached)) return cached;
        UiSkin? skin = null;
        try
        {
            string file = Path.Combine(_root, folder, "skin.toml");
            if (File.Exists(file)) skin = UiSkin.Parse(File.ReadAllText(file));
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            skin = null;
        }

        _skins[folder] = skin;
        return skin;
    }

    /// <summary>The texture of a piece, cached; null when the piece, the file or the graphics device is missing or the PNG is unreadable.</summary>
    public Texture2D? Texture(string? folder, string key)
    {
        UiSkin? skin = Skin(folder);
        SkinPiece? piece = skin?.Piece(key);
        if (piece is null || _device is null) return null;
        string path = Path.Combine(_root, folder!, piece.File);
        if (_textures.TryGetValue(path, out Texture2D? cached)) return cached;
        Texture2D? tex = null;
        try
        {
            if (File.Exists(path))
            {
                using FileStream fs = File.OpenRead(path);
                tex = Texture2D.FromStream(_device, fs);
            }
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or InvalidOperationException or NotSupportedException)
        {
            tex = null;
        }

        _textures[path] = tex;
        return tex;
    }
}
