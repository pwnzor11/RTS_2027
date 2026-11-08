// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using System.Security.Cryptography;
using Rts.Data;
using Rts.Game.Render;
using Rts.Game.Ui;
using Rts.Sim.Core;
using Rts.Sim.Map;
using Rts.Sim.Rules;

namespace Rts.Game.Tests.Render;

/// <summary>M4-10: theater palettes load by id, the bake and the minimap use the same one, and Rust Belt is unchanged.</summary>
public sealed class TheaterTests
{
    /// <summary>Same pin as TerrainPaletteTests: the Rust Belt bake of the shipped M2 map.</summary>
    private const string RustBeltM2BakeSha256 = "54353962097f3604278995490275fa4ff90cb3787050691d1685fb3537517118";

    public static readonly string[] Theaters = { "rustbelt", "glasscoast", "mountains", "ashplains" };

    [Theory]
    [InlineData("rustbelt")]
    [InlineData("glasscoast")]
    [InlineData("mountains")]
    [InlineData("ashplains")]
    [InlineData("glass_coast")]
    [InlineData("ash_plains")]
    [InlineData("rust_belt")]
    public void EveryTheaterLoadsWithAllLandTypes(string id)
    {
        TerrainPalette palette = TerrainPalette.Load(id);
        foreach (LandType type in Enum.GetValues<LandType>())
            Assert.NotEqual(default, palette.ShadesFor(type).Base);
    }

    [Fact]
    public void TheatersLookDifferent()
    {
        var bases = new HashSet<TerrainPalette.Rgb>();
        foreach (string t in Theaters) bases.Add(TerrainPalette.Load(t).ShadesFor(LandType.Clear).Base);
        Assert.Equal(Theaters.Length, bases.Count);
        Assert.NotEqual(TerrainPalette.Load("rustbelt").ShadesFor(LandType.Shallow).Base,
            TerrainPalette.Load("glasscoast").ShadesFor(LandType.Shallow).Base);
    }

    [Fact]
    public void RustBeltIsAnAliasAndUnknownFallsBackWithAWarning()
    {
        Assert.Same(TerrainPalette.Load("rustbelt"), TerrainPalette.Load("rust_belt"));

        var warnings = new List<string>();
        void Sink(string m) => warnings.Add(m);
        TerrainPalette.Warned += Sink;
        try
        {
            TerrainPalette fallback = TerrainPalette.Load("no_such_theater_m410");
            TerrainPalette.Load("no_such_theater_m410");
            Assert.Same(TerrainPalette.Load("rustbelt"), fallback);
        }
        finally { TerrainPalette.Warned -= Sink; }

        Assert.Single(warnings);
        Assert.Contains("no_such_theater_m410", warnings[0]);
    }

    [Fact]
    public void BiomeInMetaPicksTheTheaterAndNoMetaIsRustBelt()
    {
        MapData map = Synthetic("rustbelt");
        MapMeta glass = MapMeta.DefaultFor(map) with { Biome = "glass_coast" };
        Assert.Equal("glasscoast", TerrainPalette.TheaterIdFor(map, glass));
        Assert.Equal("rustbelt", TerrainPalette.TheaterIdFor(map, null));
        Assert.Equal("rustbelt", TerrainPalette.TheaterIdFor(map, MapMeta.DefaultFor(map)));

        byte[] viaMeta = TerrainBake.Bake(map, glass).Rgba;
        byte[] viaPalette = TerrainBake.Bake(map, TerrainPalette.Load("glasscoast")).Rgba;
        Assert.Equal(viaPalette, viaMeta);
        Assert.NotEqual(TerrainBake.Bake(map, (MapMeta?)null).Rgba, viaMeta);
    }

    [Fact]
    public void ClassicRustBeltBakeIsByteIdenticalForTheShippedMap()
    {
        // D-181: the shipped look is C, so the old pin is kept on the classic palette (the pre-S15 data, unchanged apart from a header comment).
        (MapData map, _) = MapFile.LoadWithMeta(RepoFile("data/maps/rustbelt_m2.map.toml"));
        TerrainPalette classic = TerrainPalette.Load(RepoFile("data/theaters/variants/classic/rustbelt.toml"));
        Assert.Equal(RustBeltM2BakeSha256, Convert.ToHexString(SHA256.HashData(TerrainBake.Bake(map, classic).Rgba)).ToLowerInvariant());
    }

    [Theory]
    [InlineData("rustbelt")]
    [InlineData("glass_coast")]
    [InlineData("mountains")]
    [InlineData("ash_plains")]
    public void ShippedPaletteIsVariantCAndRustBeltAliasesAgree(string file)
    {
        string Body(string path) => string.Join("\n", File.ReadAllText(RepoFile(path)).Replace("\r\n", "\n").Split('\n').Skip(2));
        Assert.Equal(Body($"data/theaters/variants/c/{file}.toml"), Body($"data/theaters/{file}.toml"));

        Assert.Same(TerrainPalette.Load("rustbelt"), TerrainPalette.Load("rust_belt"));
    }

    [Theory]
    [InlineData("rustbelt")]
    [InlineData("glasscoast")]
    [InlineData("mountains")]
    [InlineData("ashplains")]
    public void MinimapColoursMatchTheBake(string theater)
    {
        TerrainPalette palette = TerrainPalette.Load(theater);
        foreach (LandType land in Enum.GetValues<LandType>())
        {
            Microsoft.Xna.Framework.Color mini = Minimap.TerrainColour(palette, land, OverlayKind.None, 0);
            TerrainPalette.Rgb b = palette.ShadesFor(land).Base;
            Assert.Equal((b.R, b.G, b.B), (mini.R, mini.G, mini.B));

            // The mean colour of a cell's interior texels stays near the minimap colour (shades are dark/base/light).
            MapData one = Uniform(theater, land);
            TerrainBake.Result baked = TerrainBake.Bake(one, palette);
            int s = palette.TexelCells, n = 0;
            long r = 0, g = 0, bl = 0;
            for (int ty = s; ty < 2 * s; ty++)
                for (int tx = s; tx < 2 * s; tx++)
                {
                    int o = (ty * baked.Width + tx) * 4;
                    r += baked.Rgba[o]; g += baked.Rgba[o + 1]; bl += baked.Rgba[o + 2]; n++;
                }

            Assert.InRange(Math.Abs(r / n - mini.R), 0, 22);
            Assert.InRange(Math.Abs(g / n - mini.G), 0, 22);
            Assert.InRange(Math.Abs(bl / n - mini.B), 0, 22);
        }
    }

    [Fact]
    public void BakePreviewsPerTheater()
    {
        string dir = Path.Combine(RepoRoot(), "scratch", "theaters");
        Directory.CreateDirectory(dir);
        MapData map = Preview();
        foreach (string t in Theaters)
        {
            TerrainBake.Result baked = TerrainBake.Bake(map, TerrainPalette.Load(t));
            string path = Path.Combine(dir, t + ".png");
            File.WriteAllBytes(path, Png(baked.Width, baked.Height, baked.Rgba));
            Assert.True(new FileInfo(path).Length > 1000);
        }
    }

    /// <summary>A 32x32 map with a band per land type, an ashfall and a pitchblende patch.</summary>
    private static MapData Preview(string theater = "rustbelt")
    {
        const int n = 32;
        var land = new LandType[n * n];
        var ovl = new OverlayKind[n * n];
        var dens = new byte[n * n];
        LandType[] types = Enum.GetValues<LandType>();
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                int k = y * n + x;
                land[k] = types[(x / 4 + (y / 16) * 4) % types.Length];
                if (y % 16 >= 12 && x < 16) { ovl[k] = OverlayKind.Ashfall; dens[k] = (byte)(40 + (x * 14)); }
                if (y % 16 >= 12 && x >= 16) { ovl[k] = OverlayKind.Pitchblende; dens[k] = (byte)(40 + ((x - 16) * 14)); }
            }

        return new MapData("preview", theater, n, n, new ushort[n * n], land, new byte[n * n], ovl, dens, new[] { new CellCoord(0, 0) });
    }

    private static MapData Synthetic(string theater) => Preview(theater);

    private static MapData Uniform(string theater, LandType land)
    {
        const int n = 3;
        var l = new LandType[n * n];
        Array.Fill(l, land);
        return new MapData("u", theater, n, n, new ushort[n * n], l, new byte[n * n], new OverlayKind[n * n], new byte[n * n], new[] { new CellCoord(0, 0) });
    }

    private static byte[] Png(int w, int h, byte[] rgba)
    {
        using var ms = new MemoryStream();
        ms.Write(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A });
        var ihdr = new byte[13];
        Be(ihdr, 0, w); Be(ihdr, 4, h); ihdr[8] = 8; ihdr[9] = 6;
        Chunk(ms, "IHDR", ihdr);
        using var raw = new MemoryStream();
        using (var z = new System.IO.Compression.ZLibStream(raw, System.IO.Compression.CompressionLevel.Fastest, true))
        {
            for (int y = 0; y < h; y++)
            {
                z.WriteByte(0);
                z.Write(rgba, y * w * 4, w * 4);
            }
        }

        Chunk(ms, "IDAT", raw.ToArray());
        Chunk(ms, "IEND", Array.Empty<byte>());
        return ms.ToArray();
    }

    private static void Be(byte[] b, int o, int v) { b[o] = (byte)(v >> 24); b[o + 1] = (byte)(v >> 16); b[o + 2] = (byte)(v >> 8); b[o + 3] = (byte)v; }

    private static void Chunk(Stream s, string type, byte[] data)
    {
        var len = new byte[4]; Be(len, 0, data.Length); s.Write(len);
        byte[] body = new byte[4 + data.Length];
        System.Text.Encoding.ASCII.GetBytes(type).CopyTo(body, 0);
        data.CopyTo(body, 4);
        s.Write(body);
        uint crc = 0xFFFFFFFFu;
        foreach (byte x in body)
        {
            crc ^= x;
            for (int i = 0; i < 8; i++) crc = (crc & 1) != 0 ? (crc >> 1) ^ 0xEDB88320u : crc >> 1;
        }

        var c = new byte[4]; Be(c, 0, (int)~crc); s.Write(c);
    }

    private static string RepoRoot()
    {
        string? dir = AppContext.BaseDirectory;
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir, "RTS_2027.slnx"))) return dir;
            dir = Path.GetDirectoryName(dir);
        }

        throw new FileNotFoundException("RTS_2027.slnx not found");
    }

    private static string RepoFile(string relative) => Path.Combine(RepoRoot(), relative);
}
