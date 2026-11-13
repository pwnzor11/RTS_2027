// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Microsoft.Xna.Framework;
using Tomlyn;
using Tomlyn.Model;

namespace Rts.Game.Ui.Skin;

/// <summary>One skin piece: a PNG file name (relative to the skin folder) and, for a 9-slice, its slice margins.</summary>
/// <param name="File">PNG file name inside the skin folder.</param>
/// <param name="Slice">Left, top, right, bottom margins in source pixels, or null for a piece that is stretched whole.</param>
public sealed record SkinPiece(string File, SkinSlice? Slice);

/// <summary>The four 9-slice margins of a skin piece, in source pixels.</summary>
/// <param name="Left">Left margin.</param>
/// <param name="Top">Top margin.</param>
/// <param name="Right">Right margin.</param>
/// <param name="Bottom">Bottom margin.</param>
public readonly record struct SkinSlice(int Left, int Top, int Right, int Bottom);

/// <summary>
/// A parsed faction UI skin (<c>data/ui/skins/&lt;faction&gt;/skin.toml</c>, S24). Pure data: names, three colours and the pieces by
/// key. Anything missing or malformed is simply absent, and the caller then draws that piece procedurally as before.
/// </summary>
public sealed class UiSkin
{
    /// <summary>The piece keys the game draws.</summary>
    public static readonly IReadOnlyList<string> SliceKeys = new[]
    {
        "panel", "button", "button_hover", "button_pressed", "button_disabled", "tab", "tab_active", "minimap_frame", "card_frame",
    };

    /// <summary>The key of the stretched sidebar background.</summary>
    public const string SidebarBackKey = "sidebar_back";

    private readonly Dictionary<string, SkinPiece> _pieces;

    private UiSkin(string name, Color? text, Color? dim, Color? accent, Dictionary<string, SkinPiece> pieces)
    {
        Name = name;
        TextColor = text;
        TextDim = dim;
        Accent = accent;
        _pieces = pieces;
    }

    /// <summary>The skin's display name (may be empty).</summary>
    public string Name { get; }

    /// <summary>Main text colour, or null to keep the default.</summary>
    public Color? TextColor { get; }

    /// <summary>Dimmed text colour, or null to keep the default.</summary>
    public Color? TextDim { get; }

    /// <summary>Accent colour, or null to keep the default.</summary>
    public Color? Accent { get; }

    /// <summary>The piece for <paramref name="key"/>, or null when the skin does not define it.</summary>
    public SkinPiece? Piece(string key) => _pieces.TryGetValue(key, out SkinPiece? p) ? p : null;

    /// <summary>Number of pieces defined.</summary>
    public int PieceCount => _pieces.Count;

    /// <summary>Parses skin text; never throws. Bad text gives an empty skin (every piece falls back).</summary>
    public static UiSkin Parse(string text)
    {
        TomlTable root;
        try
        {
            root = TomlSerializer.Deserialize<TomlTable>(text) ?? new TomlTable();
        }
        catch (Exception)
        {
            return new UiSkin("", null, null, null, new Dictionary<string, SkinPiece>());
        }

        var pieces = new Dictionary<string, SkinPiece>(StringComparer.Ordinal);
        foreach (string key in SliceKeys) AddPiece(root, key, true, pieces);
        AddPiece(root, SidebarBackKey, false, pieces);
        string name = root.TryGetValue("name", out object? n) && n is string ns ? ns : "";
        return new UiSkin(name, Colour(root, "text_color"), Colour(root, "text_dim"), Colour(root, "accent"), pieces);
    }

    /// <summary>Parses "#RRGGBB" (case-insensitive); null when it is not exactly that.</summary>
    public static Color? ParseColour(string? s)
    {
        if (s is null || s.Length != 7 || s[0] != '#') return null;
        return int.TryParse(s.AsSpan(1), System.Globalization.NumberStyles.AllowHexSpecifier, System.Globalization.CultureInfo.InvariantCulture, out int v)
            ? new Color((v >> 16) & 0xFF, (v >> 8) & 0xFF, v & 0xFF)
            : null;
    }

    private static Color? Colour(TomlTable root, string key) => root.TryGetValue(key, out object? v) && v is string s ? ParseColour(s) : null;

    private static void AddPiece(TomlTable root, string key, bool wantSlice, Dictionary<string, SkinPiece> into)
    {
        if (!root.TryGetValue(key, out object? raw) || raw is not TomlTable t) return;
        if (!t.TryGetValue("file", out object? f) || f is not string file || file.Length == 0) return;
        if (file.Contains('/') || file.Contains('\\') || file.Contains("..", StringComparison.Ordinal)) return;   // stays inside the skin folder
        SkinSlice? slice = null;
        if (wantSlice)
        {
            if (!t.TryGetValue("slice", out object? sr) || sr is not TomlArray arr || arr.Count != 4) return;
            var m = new int[4];
            for (int i = 0; i < 4; i++)
            {
                if (arr[i] is not long l || l < 0 || l > 4096) return;
                m[i] = (int)l;
            }

            slice = new SkinSlice(m[0], m[1], m[2], m[3]);
        }

        into[key] = new SkinPiece(file, slice);
    }
}
