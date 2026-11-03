// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Tomlyn;
using Tomlyn.Model;

namespace Rts.Game.Ui.Fonts;

/// <summary>
/// One face at one pixel size: the line metrics, glyph rectangles and kerning from a generated <c>.font.toml</c>, and,
/// once <see cref="EnsureTexture"/> succeeds, its atlas texture. The texture is premultiplied on load so it blends
/// with the default <c>SpriteBatch</c> alpha blend.
/// </summary>
public sealed class FontAtlas
{
    /// <summary>The codepoint drawn for any character the atlas does not hold.</summary>
    public const int UnknownCodepoint = '?';

    private readonly Dictionary<int, FontGlyph> _glyphs;
    private readonly Dictionary<(int Left, int Right), int> _kerning;

    private FontAtlas(string face, int size, int lineHeight, int baseline, string textureFile, int textureWidth,
        int textureHeight, Dictionary<int, FontGlyph> glyphs, Dictionary<(int, int), int> kerning)
    {
        Face = face;
        Size = size;
        LineHeight = lineHeight;
        Baseline = baseline;
        TextureFile = textureFile;
        TextureWidth = textureWidth;
        TextureHeight = textureHeight;
        _glyphs = glyphs;
        _kerning = kerning;
    }

    /// <summary>Face name, for example <c>noto-sans-regular</c>.</summary>
    public string Face { get; }

    /// <summary>Pixel size (em) the atlas was rasterised at.</summary>
    public int Size { get; }

    /// <summary>Distance in pixels from one baseline to the next.</summary>
    public int LineHeight { get; }

    /// <summary>Distance in pixels from the top of the line box to the baseline.</summary>
    public int Baseline { get; }

    /// <summary>File name of the atlas PNG, relative to the fonts folder.</summary>
    public string TextureFile { get; }

    /// <summary>Atlas width in pixels.</summary>
    public int TextureWidth { get; }

    /// <summary>Atlas height in pixels.</summary>
    public int TextureHeight { get; }

    /// <summary>The premultiplied atlas texture, or null until <see cref="EnsureTexture"/> succeeds.</summary>
    public Texture2D? Texture { get; private set; }

    /// <summary>Number of glyphs in the atlas.</summary>
    public int GlyphCount => _glyphs.Count;

    /// <summary>
    /// Reads <c>&lt;face&gt;_&lt;size&gt;.font.toml</c> from <paramref name="fontDirectory"/>. Returns false with a reason
    /// when the file is missing or malformed; it never throws.
    /// </summary>
    /// <param name="fontDirectory">The folder that holds the generated files (data/fonts).</param>
    /// <param name="face">Face name, for example <c>noto-sans-bold</c>.</param>
    /// <param name="size">Pixel size.</param>
    /// <param name="atlas">The parsed atlas on success, otherwise null.</param>
    /// <param name="error">The reason for failure, otherwise null.</param>
    public static bool TryLoad(string fontDirectory, string face, int size, out FontAtlas? atlas, out string? error)
    {
        atlas = null;
        error = null;
        string path = Path.Combine(fontDirectory, $"{face}_{size}.font.toml");
        if (!File.Exists(path))
        {
            error = $"font metrics not found: {path}";
            return false;
        }

        try
        {
            TomlTable root = TomlSerializer.Deserialize<TomlTable>(File.ReadAllText(path))
                ?? throw new InvalidDataException("empty file");
            atlas = Parse(root, face, size);
            return true;
        }
        catch (Exception ex)
        {
            error = $"bad font metrics {path}: {ex.Message}";
            return false;
        }
    }

    /// <summary>
    /// Parses a metrics table (the root of a <c>.font.toml</c>). Throws <see cref="InvalidDataException"/> when the
    /// table is inconsistent, for example a glyph outside the texture or no glyph for '?'.
    /// </summary>
    /// <param name="root">The parsed TOML document.</param>
    /// <param name="face">Expected face name.</param>
    /// <param name="size">Expected pixel size.</param>
    public static FontAtlas Parse(TomlTable root, string face, int size)
    {
        if (Int(root, "format") != 1) throw new InvalidDataException("unsupported format");
        string fileFace = Str(root, "face");
        int fileSize = Int(root, "size");
        if (fileFace != face || fileSize != size) throw new InvalidDataException($"file says {fileFace} {fileSize}");

        string texture = Str(root, "texture");
        int texWidth = Int(root, "texture_width");
        int texHeight = Int(root, "texture_height");
        int lineHeight = Int(root, "line_height");
        int baseline = Int(root, "baseline");
        if (lineHeight <= 0 || baseline < 0 || baseline > lineHeight) throw new InvalidDataException("bad line metrics");

        var glyphs = new Dictionary<int, FontGlyph>();
        if (root.TryGetValue("glyph", out object? rawGlyphs) && rawGlyphs is IEnumerable<object?> glyphList)
        {
            foreach (object? item in glyphList)
            {
                if (item is not TomlTable t) throw new InvalidDataException("glyph entry is not a table");
                var g = new FontGlyph(Int(t, "codepoint"), Int(t, "x"), Int(t, "y"), Int(t, "w"), Int(t, "h"),
                    Int(t, "xoffset"), Int(t, "yoffset"), Int(t, "advance"));
                if (g.W < 0 || g.H < 0 || g.Advance < 0) throw new InvalidDataException($"bad glyph U+{g.Codepoint:X4}");
                if (g.W > 0 && (g.X < 0 || g.Y < 0 || g.X + g.W > texWidth || g.Y + g.H > texHeight))
                {
                    throw new InvalidDataException($"glyph U+{g.Codepoint:X4} outside the texture");
                }
                glyphs[g.Codepoint] = g;
            }
        }
        if (!glyphs.ContainsKey(UnknownCodepoint)) throw new InvalidDataException("no glyph for '?'");

        var kerning = new Dictionary<(int, int), int>();
        if (root.TryGetValue("kerning", out object? rawKerning) && rawKerning is IEnumerable<object?> kerningList)
        {
            foreach (object? item in kerningList)
            {
                if (item is not TomlTable t) throw new InvalidDataException("kerning entry is not a table");
                kerning[(Int(t, "left"), Int(t, "right"))] = Int(t, "amount");
            }
        }

        return new FontAtlas(face, size, lineHeight, baseline, texture, texWidth, texHeight, glyphs, kerning);
    }

    /// <summary>
    /// Loads the atlas PNG onto <paramref name="device"/> and premultiplies it. Idempotent: returns true at once when the
    /// texture is already loaded. Returns false with a reason when the PNG is missing or unreadable.
    /// </summary>
    /// <param name="device">The graphics device to create the texture on.</param>
    /// <param name="fontDirectory">The folder that holds the atlas PNG.</param>
    /// <param name="error">The reason for failure, otherwise null.</param>
    public bool EnsureTexture(GraphicsDevice device, string fontDirectory, out string? error)
    {
        error = null;
        if (Texture is not null) return true;
        string path = Path.Combine(fontDirectory, TextureFile);
        if (!File.Exists(path))
        {
            error = $"font atlas not found: {path}";
            return false;
        }

        try
        {
            using FileStream stream = File.OpenRead(path);
            Texture2D texture = Texture2D.FromStream(device, stream);
            var pixels = new Color[texture.Width * texture.Height];
            texture.GetData(pixels);
            for (int i = 0; i < pixels.Length; i++)
            {
                pixels[i] = Premultiply(pixels[i]);
            }
            texture.SetData(pixels);
            if (texture.Width != TextureWidth || texture.Height != TextureHeight)
            {
                texture.Dispose();
                error = $"{path} is {texture.Width}x{texture.Height}, metrics say {TextureWidth}x{TextureHeight}";
                return false;
            }
            Texture = texture;
            return true;
        }
        catch (Exception ex)
        {
            error = $"cannot load font atlas {path}: {ex.Message}";
            return false;
        }
    }

    /// <summary>Maps a character to the codepoint the atlas draws for it ('?' when the atlas has no glyph).</summary>
    /// <param name="ch">A UTF-16 code unit.</param>
    public int ResolveCodepoint(char ch) => _glyphs.ContainsKey(ch) ? ch : UnknownCodepoint;

    /// <summary>Returns the glyph for a resolved codepoint, or the '?' glyph when there is none.</summary>
    /// <param name="codepoint">A codepoint from <see cref="ResolveCodepoint"/>.</param>
    public FontGlyph GetGlyph(int codepoint) =>
        _glyphs.TryGetValue(codepoint, out FontGlyph glyph) ? glyph : _glyphs[UnknownCodepoint];

    /// <summary>Kerning in pixels between two resolved codepoints; 0 when the pair has none.</summary>
    /// <param name="left">The codepoint drawn first.</param>
    /// <param name="right">The codepoint drawn after it.</param>
    public int KerningBetween(int left, int right) => _kerning.TryGetValue((left, right), out int amount) ? amount : 0;

    /// <summary>
    /// Width in pixels of <paramref name="text"/>: the sum of glyph advances plus kerning between neighbouring pairs.
    /// Characters the atlas lacks count as '?'. The last glyph's advance is included, as in the 5x7 font.
    /// </summary>
    /// <param name="text">Text to measure.</param>
    public int Measure(string text)
    {
        int width = 0;
        int previous = -1;
        foreach (char ch in text)
        {
            int codepoint = ResolveCodepoint(ch);
            if (previous >= 0) width += KerningBetween(previous, codepoint);
            width += GetGlyph(codepoint).Advance;
            previous = codepoint;
        }
        return width;
    }

    private static Color Premultiply(Color c) =>
        new((byte)(c.R * c.A / 255), (byte)(c.G * c.A / 255), (byte)(c.B * c.A / 255), c.A);

    private static int Int(TomlTable t, string key)
    {
        if (!t.TryGetValue(key, out object? value) || value is not long number)
        {
            throw new InvalidDataException($"missing or non-integer '{key}'");
        }
        return checked((int)number);
    }

    private static string Str(TomlTable t, string key) =>
        t.TryGetValue(key, out object? value) && value is string s ? s : throw new InvalidDataException($"missing '{key}'");
}
