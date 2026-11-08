// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using System.Diagnostics;
using Rts.Data;
using Rts.Game.Render;
using Rts.Sim.Core;
using Rts.Sim.Map;
using Rts.Sim.Rules;

namespace Rts.Game.Tests.Render;

/// <summary>M12-01: the derived decal pass: theater tables, determinism, where decals may and may not appear, cost.</summary>
public sealed class DecalTests
{
    private static readonly string[] Files = { "rustbelt", "glass_coast", "mountains", "ash_plains" };

    public static IEnumerable<object[]> ShippedFiles() => Files.Select(f => new object[] { f });

    private static MapData Generated(Biome biome, ulong seed = 7, MapSize size = MapSize.Medium) =>
        MapGenerator.Generate(new MapGenParams(biome, size, 2, seed));

    [Theory]
    [MemberData(nameof(ShippedFiles))]
    public void ShippedTheatersHaveDecalTablesAndClassicHasNone(string file)
    {
        TerrainPalette shipped = TerrainPalette.Load(RepoFile($"data/theaters/{file}.toml"));
        Assert.True(shipped.Decor.Decals.Count >= 5, "each biome has its own decal table");
        Assert.NotNull(shipped.Decor.Rock);
        Assert.NotNull(shipped.Decor.Rough);
        foreach (DecalRule r in shipped.Decor.Decals)
        {
            Assert.InRange(r.Permille, 1, 1000);
            Assert.True(r.LandMask != 0, r.Name);
            Assert.True(r.SizeMin <= r.SizeMax, r.Name);
        }

        // Rules that share a land type must leave room for the roll: their chances add up to at most 1000.
        foreach (LandType land in Enum.GetValues<LandType>())
            Assert.InRange(shipped.Decor.Decals.Where(r => r.Allows(land)).Sum(r => r.Permille), 0, 1000);

        TerrainPalette classic = TerrainPalette.Load(RepoFile($"data/theaters/variants/classic/{file}.toml"));
        Assert.True(classic.Decor.IsEmpty);
    }

    [Fact]
    public void TablesDifferByBiome()
    {
        var names = Files.Select(f => string.Join(",", TerrainPalette.Load(RepoFile($"data/theaters/{f}.toml")).Decor.Decals.Select(d => d.Name))).ToList();
        Assert.Equal(Files.Length, names.Distinct().Count());
    }

    [Fact]
    public void BadDecalTablesAreRejected()
    {
        string good = File.ReadAllText(RepoFile("data/theaters/rustbelt.toml"));
        Assert.Throws<InvalidDataException>(() => TerrainPalette.FromToml(good.Replace("shape = \"crack\"", "shape = \"spiral\"")));
        Assert.Throws<InvalidDataException>(() => TerrainPalette.FromToml(good.Replace("\"road\", \"clear\"]", "\"roadz\", \"clear\"]")));
        Assert.Throws<InvalidDataException>(() => TerrainPalette.FromToml(good.Replace("rock = \"boulder\"", "rock = \"statue\"")));
    }

    [Theory]
    [InlineData(Biome.RustBelt)]
    [InlineData(Biome.GlassCoast)]
    [InlineData(Biome.Mountains)]
    [InlineData(Biome.AshPlains)]
    public void BakeIsDeterministicAndSeedDependent(Biome biome)
    {
        MapData map = Generated(biome);
        TerrainPalette p = TerrainPalette.ForMap(map);
        ulong seed = MapHasher.Compute(map);

        DecorBake.Result a = DecorBake.Bake(map, p, seed);
        DecorBake.Result b = DecorBake.Bake(map, p, seed);
        DecorBake.Result other = DecorBake.Bake(map, p, seed + 1);

        Assert.Equal(map.Width * p.TexelCells, a.Width);
        Assert.Equal(a.Rgba, b.Rgba);
        Assert.Equal(a.Count, b.Count);
        Assert.NotEqual(a.Rgba, other.Rgba);
        Assert.True(a.Count > map.Width * map.Height / 40, $"{biome}: {a.Count} decals on {map.Width}x{map.Height}");
        Assert.True(a.Count < map.Width * map.Height / 4, $"{biome}: {a.Count} decals is too many");
    }

    [Fact]
    public void IntegerHashHasFixedValues()
    {
        // Pinned so the decor never changes between machines or runs (no GetHashCode, no System.Random).
        Assert.Equal(DecorHash.Mix(12345UL, 3, 4, 1), DecorHash.Mix(12345UL, 3, 4, 1));
        Assert.NotEqual(DecorHash.Mix(12345UL, 3, 4, 1), DecorHash.Mix(12345UL, 4, 3, 1));
        Assert.NotEqual(DecorHash.Mix(12345UL, 3, 4, 1), DecorHash.Mix(12345UL, 3, 4, 2));
        Assert.Equal(180993368u, DecorHash.Mix(0xABCDEF0123456789UL, 17, 42, 3));
    }

    [Fact]
    public void DecalsStayOffForbiddenGroundResourcesAndStarts()
    {
        const int n = 24;
        var land = new LandType[n * n];
        var ovl = new OverlayKind[n * n];
        var dens = new byte[n * n];
        Array.Fill(land, LandType.Clear);
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                if (x >= 12) land[y * n + x] = LandType.Cliff;
                if (x < 12 && y >= 18) ovl[y * n + x] = OverlayKind.Ashfall;
            }

        var map = new MapData("t", "rustbelt", n, n, new ushort[n * n], land, new byte[n * n], ovl, dens, new[] { new CellCoord(2, 2) });
        TerrainPalette p = TerrainPalette.Load(RepoFile("data/theaters/rustbelt.toml"));
        int s = p.TexelCells, found = 0;
        for (ulong seed = 1; seed <= 6; seed++)
        {
            DecorBake.Result r = DecorBake.Bake(map, p, seed);
            for (int ty = 0; ty < r.Height; ty++)
                for (int tx = 0; tx < r.Width; tx++)
                {
                    if (r.Rgba[(ty * r.Width + tx) * 4 + 3] == 0) continue;
                    found++;
                    int cx = tx / s, cy = ty / s;
                    Assert.NotEqual(LandType.Cliff, land[cy * n + cx]);
                    Assert.Equal(OverlayKind.None, ovl[cy * n + cx]);
                }

            // nothing is centred within three cells of the start (a texel can still reach in by at most a cell and a half)
            Assert.Equal(0, CountAlpha(r, s, 0, 0, 4, 4));
        }

        Assert.True(found > 0);
    }

    [Fact]
    public void DecalsOnlyOnTheirOwnLandTypes()
    {
        MapData map = Generated(Biome.RustBelt);
        TerrainPalette p = TerrainPalette.ForMap(map);
        // Only the frost-crack rule lists water, so a water cell far from land can only hold pale blue pixels.
        DecalRule frost = p.Decor.Decals.Single(d => d.Name == "frost crack");
        DecorBake.Result r = DecorBake.Bake(map, p, MapHasher.Compute(map));
        int s = p.TexelCells, water = 0;
        for (int ty = 0; ty < r.Height; ty++)
            for (int tx = 0; tx < r.Width; tx++)
            {
                int o = (ty * r.Width + tx) * 4;
                if (r.Rgba[o + 3] == 0 || map.Land[ty / s * map.Width + tx / s] != LandType.Water) continue;
                water++;
                Assert.True(frost.Colour.B >= 200 && r.Rgba[o + 2] >= r.Rgba[o], "water decals are the pale blue frost cracks");
            }

        Assert.True(water > 0, "this seed has frost cracks on its river");
    }

    [Fact]
    public void BakeCostIsSmallOnTheBiggestMap()
    {
        MapData map = Generated(Biome.AshPlains, 3, MapSize.Large);
        TerrainPalette p = TerrainPalette.ForMap(map);
        ulong seed = MapHasher.Compute(map);
        DecorBake.Bake(map, p, seed);   // warm up the JIT

        long t0 = Stopwatch.GetTimestamp();
        DecorBake.Result r = DecorBake.Bake(map, p, seed);
        double ms = Stopwatch.GetElapsedTime(t0).TotalMilliseconds;

        Assert.True(r.Count > 0);
        Assert.True(ms < 1500, $"decal bake of a 128x128 map took {ms:0} ms (budget 1500 ms on a loaded 2-core box; usually well under 200)");
    }

    private static int CountAlpha(DecorBake.Result r, int s, int cx0, int cy0, int cx1, int cy1)
    {
        int n = 0;
        for (int ty = cy0 * s; ty < cy1 * s; ty++)
            for (int tx = cx0 * s; tx < cx1 * s; tx++)
                if (r.Rgba[(ty * r.Width + tx) * 4 + 3] != 0) n++;
        return n;
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
