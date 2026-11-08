// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Game.Ui;
using Rts.Game.Ui.Fonts;

namespace Rts.Game.Tests;

/// <summary>Checks the generated Noto Sans atlases (metrics only; no graphics device is needed) and the 5x7 fallback.</summary>
public sealed class FontAtlasTests
{
    private static readonly (FontWeight Weight, string Face)[] Faces =
    {
        (FontWeight.Regular, "noto-sans-regular"),
        (FontWeight.Bold, "noto-sans-bold"),
    };

    private static string DefaultDirectory()
    {
        FontLibrary.UseDirectory(null);
        string? dir = FontLibrary.Directory;
        Assert.NotNull(dir);
        Assert.True(Directory.Exists(dir), $"font folder missing: {dir}");
        return dir;
    }

    [Fact]
    public void EveryGeneratedMetricsFileParses()
    {
        string dir = DefaultDirectory();
        foreach (var (_, face) in Faces)
        {
            foreach (int size in FontLibrary.Sizes)
            {
                bool ok = FontAtlas.TryLoad(dir, face, size, out FontAtlas? atlas, out string? error);
                Assert.True(ok, error);
                Assert.Equal(face, atlas!.Face);
                Assert.Equal(size, atlas.Size);
                Assert.True(atlas.LineHeight > size, $"{face} {size}: line height {atlas.LineHeight} too small");
                Assert.True(atlas.Baseline < atlas.LineHeight);
                Assert.True(atlas.GlyphCount >= 199, $"{face} {size}: only {atlas.GlyphCount} glyphs");
                Assert.True(File.Exists(Path.Combine(dir, atlas.TextureFile)), $"missing {atlas.TextureFile}");
            }
        }
    }

    [Fact]
    public void EveryPrintableLatin1CharacterHasAGlyph()
    {
        string dir = DefaultDirectory();
        var printable = Enumerable.Range(0x20, 0x7F - 0x20).Concat(Enumerable.Range(0xA0, 0x100 - 0xA0));
        foreach (var (_, face) in Faces)
        {
            FontAtlas? atlas = null;
            Assert.True(FontAtlas.TryLoad(dir, face, 16, out atlas, out _));
            foreach (int cp in printable)
            {
                Assert.True(atlas!.ResolveCodepoint((char)cp) == cp, $"{face}: no glyph for U+{cp:X4}");
            }
            foreach (char extra in "–—‘’“”•…")
            {
                Assert.True(atlas!.ResolveCodepoint(extra) == extra, $"{face}: no glyph for U+{(int)extra:X4}");
            }
        }
    }

    [Fact]
    public void MeasureIsTheSumOfAdvancesPlusKerning()
    {
        string dir = DefaultDirectory();
        Assert.True(FontAtlas.TryLoad(dir, "noto-sans-regular", 16, out FontAtlas? atlas, out _));

        const string text = "AVATAR Tank 1,250 ¢ äöü — “done”…";
        int expected = 0;
        int previous = -1;
        foreach (char ch in text)
        {
            int cp = atlas!.ResolveCodepoint(ch);
            if (previous >= 0) expected += atlas.KerningBetween(previous, cp);
            expected += atlas.GetGlyph(cp).Advance;
            previous = cp;
        }

        Assert.Equal(expected, atlas!.Measure(text));
        Assert.Equal(expected, BitmapFont.Measure(text, 2, FontWeight.Regular));
    }

    [Fact]
    public void PairWidthIncludesKerningBetweenTheTwoGlyphs()
    {
        string dir = DefaultDirectory();
        Assert.True(FontAtlas.TryLoad(dir, "noto-sans-bold", 24, out FontAtlas? atlas, out _));
        int a = atlas!.ResolveCodepoint('A');
        int v = atlas.ResolveCodepoint('V');
        Assert.Equal(atlas.GetGlyph(a).Advance + atlas.KerningBetween(a, v) + atlas.GetGlyph(v).Advance, atlas.Measure("AV"));
    }

    [Fact]
    public void UnknownCharactersMeasureAsQuestionMark()
    {
        DefaultDirectory();
        FontAtlas? atlas = FontLibrary.Get(FontWeight.Regular, 16);
        Assert.NotNull(atlas);
        Assert.Equal(atlas!.Measure("?"), atlas.Measure("中"));
        Assert.Equal(atlas.Measure("?"), atlas.Measure("\u0007"));
    }

    [Fact]
    public void MissingFolderFallsBackToTheFiveBySevenFont()
    {
        string missing = Path.Combine(Path.GetTempPath(), "rts-no-such-font-folder-" + Guid.NewGuid().ToString("N"));
        FontLibrary.UseDirectory(missing);
        try
        {
            Assert.False(FontAtlas.TryLoad(missing, "noto-sans-regular", 16, out FontAtlas? atlas, out string? error));
            Assert.Null(atlas);
            Assert.Contains("not found", error, StringComparison.Ordinal);

            Assert.Null(FontLibrary.Get(FontWeight.Bold, 24));
            Assert.Equal(3 * BitmapFont.Advance * 2, BitmapFont.Measure("abc", 2));
            Assert.Equal(3 * BitmapFont.Advance * 2, BitmapFont.Measure("abc", 1.5f, FontWeight.Bold));
            Assert.Equal(BitmapFont.GlyphHeight * 2, BitmapFont.LineHeight(2, FontWeight.Regular));
            Assert.NotNull(FontLibrary.LastError);
        }
        finally
        {
            FontLibrary.UseDirectory(null);
        }
    }

    [Fact]
    public void MalformedMetricsFileIsRejectedWithoutThrowing()
    {
        string dir = Path.Combine(Path.GetTempPath(), "rts-bad-font-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            File.WriteAllText(Path.Combine(dir, "noto-sans-regular_16.font.toml"), "format = 1\nface = \"noto-sans-regular\"\n");
            Assert.False(FontAtlas.TryLoad(dir, "noto-sans-regular", 16, out FontAtlas? atlas, out string? error));
            Assert.Null(atlas);
            Assert.NotNull(error);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Theory]
    [InlineData(720, 1f)]
    [InlineData(800, 1f)]
    [InlineData(801, 1.5f)]
    [InlineData(1200, 1.5f)]
    [InlineData(1201, 2f)]
    [InlineData(2160, 2f)]
    public void AutoUiScaleFollowsBackbufferHeight(int height, float expected) => Assert.Equal(expected, UiScale.Auto(height));

    [Fact]
    public void UiScaleChoosesTheNearestAtlasSize()
    {
        Assert.Equal(12, BitmapFont.SizeForUiScale(1f));
        Assert.Equal(18, BitmapFont.SizeForUiScale(1.5f));
        Assert.Equal(24, BitmapFont.SizeForUiScale(2f));
        Assert.Equal(9, BitmapFont.SizeForUiScale(0.75f));
        Assert.Equal(16, BitmapFont.SizeForScale(2));
    }
}
