// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using System.Security.Cryptography;
using Rts.Data;
using Rts.Game.Render;
using Rts.Sim.Core;
using Rts.Sim.Map;
using Rts.Sim.Rules;

namespace Rts.Game.Tests;

/// <summary>Rust Belt palette, texture bake and resource speckle. Pinned to the classic (pre-S15) look: the python bake in make_rustbelt_m2.py only knows that algorithm (D-181).</summary>
public sealed class TerrainPaletteTests
{
    /// <summary>SHA-256 of the baked texture for data/maps/rustbelt_m2.map.toml. Python prints the same value.</summary>
    private const string RustBeltM2BakeSha256 = "54353962097f3604278995490275fa4ff90cb3787050691d1685fb3537517118";

    [Fact]
    public void PaletteFileParses()
    {
        TerrainPalette palette = TerrainPalette.Load(RepoFile("data/theaters/variants/classic/rustbelt.toml"));

        Assert.Equal(8, palette.TexelCells);
        Assert.True(palette.AshfallMaxPermille > 0);
        Assert.True(palette.GlintOneIn >= 1);
    }

    [Fact]
    public void EveryLandTypeHasAColour()
    {
        TerrainPalette palette = TerrainPalette.Load(RepoFile("data/theaters/variants/classic/rustbelt.toml"));

        foreach (LandType type in Enum.GetValues<LandType>())
        {
            TerrainPalette.LandShades shades = palette.ShadesFor(type);
            Assert.NotEqual(default, shades.Base);
            Assert.NotEqual(default, shades.Dark);
            Assert.NotEqual(default, shades.Light);
        }
    }

    [Fact]
    public void MissingLandTypeIsRejected()
    {
        string text = File.ReadAllText(RepoFile("data/theaters/variants/classic/rustbelt.toml"));
        string broken = text.Replace("[land.cliff]", "[land.cliffless]");

        Assert.Throws<InvalidDataException>(() => TerrainPalette.FromToml(broken));
    }

    [Fact]
    public void OnlyTheRustBeltTheaterUsesThePalette()
    {
        Assert.True(TerrainPalette.IsRustBelt("rustbelt"));
        Assert.False(TerrainPalette.IsRustBelt("rust_belt"));
        Assert.False(TerrainPalette.IsRustBelt("RUSTBELT"));
    }

    [Fact]
    public void TextureIsDeterministic()
    {
        MapData map = MapFile.Load(RepoFile("data/maps/rustbelt_m2.map.toml"));

        TerrainBake.Result first = TerrainBake.Bake(map, TerrainLook.Palette);
        TerrainBake.Result second = TerrainBake.Bake(map, TerrainLook.Palette);

        Assert.Equal(map.Width * TerrainLook.Palette.TexelCells, first.Width);
        Assert.Equal(map.Height * TerrainLook.Palette.TexelCells, first.Height);
        Assert.Equal(first.Rgba, second.Rgba);
    }

    [Fact]
    public void TextureMatchesThePythonBake()
    {
        MapData map = MapFile.Load(RepoFile("data/maps/rustbelt_m2.map.toml"));

        TerrainBake.Result baked = TerrainBake.Bake(map, TerrainLook.Palette);
        string sha = Convert.ToHexString(SHA256.HashData(baked.Rgba)).ToLowerInvariant();

        Assert.Equal(RustBeltM2BakeSha256, sha);
    }

    [Fact]
    public void SpeckleGrowsWithAshfallDensity()
    {
        TerrainPalette palette = TerrainLook.Palette;
        int none = CountSpeck(palette, 0);
        int low = CountSpeck(palette, 32);
        int high = CountSpeck(palette, 224);

        Assert.Equal(0, none);
        Assert.True(low > none, $"density 32 gave {low} speckled texels, no more than density 0");
        Assert.True(high > low, $"density 224 gave {high} speckled texels, no more than density 32");
    }

    [Fact]
    public void SpeckleIsOnlyOnTheAshfallCell()
    {
        TerrainPalette palette = TerrainLook.Palette;
        MapData map = SingleCellMap(OverlayKind.Ashfall, 224);

        TerrainBake.Result baked = TerrainBake.Bake(map, palette);
        int s = palette.TexelCells;
        int speckOutside = 0;
        for (int ty = 0; ty < baked.Height; ty++)
        {
            for (int tx = 0; tx < baked.Width; tx++)
            {
                bool inside = tx / s == 1 && ty / s == 1;
                if (!inside && IsSpeck(baked, tx, ty, palette)) speckOutside++;
            }
        }

        Assert.Equal(0, speckOutside);
    }

    private static int CountSpeck(TerrainPalette palette, int density)
    {
        MapData map = SingleCellMap(OverlayKind.Ashfall, density);
        TerrainBake.Result baked = TerrainBake.Bake(map, palette);
        int count = 0;
        for (int ty = 0; ty < baked.Height; ty++)
        {
            for (int tx = 0; tx < baked.Width; tx++)
            {
                if (IsSpeck(baked, tx, ty, palette)) count++;
            }
        }

        return count;
    }

    private static bool IsSpeck(TerrainBake.Result baked, int tx, int ty, TerrainPalette palette)
    {
        int o = (ty * baked.Width + tx) * 4;
        var c = new TerrainPalette.Rgb(baked.Rgba[o], baked.Rgba[o + 1], baked.Rgba[o + 2]);
        return c == palette.AshfallSpeck;
    }

    /// <summary>A 4x4 ash map with one overlay cell at (1, 1) and every other cell clear.</summary>
    private static MapData SingleCellMap(OverlayKind overlay, int density)
    {
        const int n = 4;
        var land = new LandType[n * n];
        var ovl = new OverlayKind[n * n];
        var dens = new byte[n * n];
        Array.Fill(land, LandType.Ash);
        int k = 1 * n + 1;
        ovl[k] = overlay;
        dens[k] = (byte)density;
        return new MapData("test", "rustbelt", n, n, new ushort[n * n], land, new byte[n * n], ovl, dens,
            new[] { new CellCoord(0, 0) });
    }

    private static string RepoFile(string relative)
    {
        string? dir = AppContext.BaseDirectory;
        while (dir is not null)
        {
            string candidate = Path.Combine(dir, relative);
            if (File.Exists(candidate)) return candidate;
            dir = Path.GetDirectoryName(dir);
        }

        throw new FileNotFoundException($"{relative} not found above {AppContext.BaseDirectory}");
    }
}
