// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using System.Diagnostics;
using Rts.Data;
using Rts.Data.MapGen;
using Rts.Sim.Map;

namespace Rts.Sim.Tests.Map;

/// <summary>M4-02 (design 08 §8): procedural maps are a pure function of their params, the legacy generator is byte-identical,
/// biome parameters load from data/mapgen/biomes.toml, and a Large map generates in under 2 s.</summary>
public sealed class MapGenDeterminismTests
{
    [Theory]
    [InlineData(Biome.RustBelt, MapSize.Medium, 4, 77UL)]
    [InlineData(Biome.GlassCoast, MapSize.Small, 2, 5UL)]
    [InlineData(Biome.Mountains, MapSize.Large, 3, 12345UL)]
    [InlineData(Biome.AshPlains, MapSize.Medium, 2, ulong.MaxValue - 3)]
    public void SameParams_SameHash_ThreeTimes_DifferentSeed_DifferentHash(Biome biome, MapSize size, int players, ulong seed)
    {
        var p = new MapGenParams(biome, size, players, seed);
        ulong a = MapHasher.Compute(MapGenerator.Generate(p));
        Assert.Equal(a, MapHasher.Compute(MapGenerator.Generate(p)));
        Assert.Equal(a, MapHasher.Compute(MapGenerator.Generate(p)));
        // Different seed, same name, so only the cell layers can differ.
        Assert.NotEqual(a, MapHasher.Compute(MapGenerator.Generate(p with { Seed = seed + 1, Name = MapGenerator.DefaultName(p) })));
    }

    /// <summary>Hashes captured with the pre-M4-02 build (<c>Tools map new</c>, which names the map after the output file).</summary>
    [Theory]
    [InlineData(24, 1UL, "leg_24_1", 0xfe636ee9adfa435fUL)]
    [InlineData(32, 5UL, "leg_32_5", 0x94e561fc7b0382adUL)]
    [InlineData(47, 3UL, "leg_47_3", 0x9f7f0e135edf7f64UL)]
    [InlineData(48, 7UL, "leg_48_7", 0x0c98094fb303e9b0UL)]
    [InlineData(64, 1UL, "leg_64_1", 0x604ae512ccfd8b43UL)]
    [InlineData(64, 77UL, "leg_64_77", 0x46c777df6e38673eUL)]
    [InlineData(96, 9UL, "leg_96_9", 0x495e2e6332d5ae4fUL)]
    [InlineData(128, 42UL, "leg_128_42", 0xe2170d533084c549UL)]
    public void LegacyGenerator_MatchesGoldenHashes(int size, ulong seed, string name, ulong expected)
    {
        Assert.Equal(expected, MapHasher.Compute(MapGenerator.Generate(size, seed, name)));
    }

    [Fact]
    public void DefaultName_AndProvenance()
    {
        var p = new MapGenParams(Biome.GlassCoast, MapSize.Large, 3, 9);
        (MapData map, MapMeta meta) = MapGenerator.GenerateWithMeta(p);
        Assert.Equal("glass_coast_l_3p_9", map.Name);
        Assert.Equal("glasscoast", map.Theater);
        Assert.Equal(128, map.Width);
        Assert.Equal("glass_coast", meta.Biome);
        Assert.Equal(MapSymmetry.Rot4, meta.Symmetry);
        Assert.Equal(3, meta.MaxPlayers);
        Assert.Equal(p, meta.Generator);
        Assert.Equal("custom", MapGenerator.Generate(p with { Name = "custom" }).Name);
        Assert.Throws<ArgumentOutOfRangeException>(() => MapGenerator.Generate(p with { Players = 1 }));
        Assert.Throws<ArgumentOutOfRangeException>(() => MapGenerator.Generate(p with { Players = 5 }));
    }

    [Fact]
    public void BiomeFile_LoadsAllFourBiomes_WithDesignDefaults()
    {
        IReadOnlyList<BiomeParams> all = BiomeCatalog.Default;
        Assert.Equal(4, all.Count);
        Assert.Equal(MapFile.BiomeKeys, all.Select(b => b.Key));
        Assert.Equal(new[] { "rustbelt", "glasscoast", "mountains", "ashplains" }, all.Select(b => b.Theater));
        Assert.Equal(new[] { MapSymmetry.Rot2, MapSymmetry.Rot2, MapSymmetry.MirrorX, MapSymmetry.MirrorY }, all.Select(b => b.Symmetry2p));
        Assert.All(all, b => Assert.Equal(MapSymmetry.Rot4, b.Symmetry4p));
        Assert.True(BiomeCatalog.For(Biome.GlassCoast).OceanWidth > 0);
        Assert.True(BiomeCatalog.For(Biome.RustBelt).RoadGrid > 0);
        Assert.True(BiomeCatalog.For(Biome.Mountains).CliffPlateaus > 0);
        Assert.True(BiomeCatalog.For(Biome.AshPlains).Rivers > 0);
    }

    [Fact]
    public void BiomeFile_RejectsUnknownKeys_AndBadValues()
    {
        string text = File.ReadAllText(BiomeCatalog.FindFile(AppContext.BaseDirectory)!);
        var e1 = Assert.Throws<RulesException>(() => BiomeCatalog.Parse(text.Replace("road_grid = 16", "road_grid = 16\nroad_gird = 3"), "b.toml"));
        Assert.Contains(e1.Problems, p => p.Key == "biome.rust_belt.road_gird");
        var e2 = Assert.Throws<RulesException>(() => BiomeCatalog.Parse(text.Replace("symmetry_2p = \"mirror_x\"", "symmetry_2p = \"spiral\""), "b.toml"));
        Assert.Contains(e2.Problems, p => p.Key == "biome.mountains.symmetry_2p");
        Assert.Throws<RulesException>(() => BiomeCatalog.Parse("[biome.rust_belt]\ntheater = \"x\"\n", "b.toml"));
    }

    [Fact]
    public void ExplicitBiomeParams_ChangeTheMap()
    {
        var p = new MapGenParams(Biome.RustBelt, MapSize.Small, 2, 3);
        BiomeParams b = BiomeCatalog.For(Biome.RustBelt);
        ulong a = MapHasher.Compute(MapGenerator.GenerateWithMeta(p, b).Map);
        Assert.Equal(a, MapHasher.Compute(MapGenerator.Generate(p)));
        Assert.NotEqual(a, MapHasher.Compute(MapGenerator.GenerateWithMeta(p, b with { RoadGrid = 0 }).Map));
    }

    [Theory]
    [InlineData(Biome.RustBelt)]
    [InlineData(Biome.GlassCoast)]
    [InlineData(Biome.Mountains)]
    [InlineData(Biome.AshPlains)]
    public void LargeMap_GeneratesUnderTwoSeconds(Biome biome)
    {
        MapGenerator.Generate(new MapGenParams(biome, MapSize.Small, 2, 1));   // JIT warm-up
        long t0 = Stopwatch.GetTimestamp();
        MapGenerator.Generate(new MapGenParams(biome, MapSize.Large, 4, 2026));
        TimeSpan took = Stopwatch.GetElapsedTime(t0);
        Assert.True(took < TimeSpan.FromSeconds(2), $"{biome} Large took {took.TotalMilliseconds} ms");
    }
}
