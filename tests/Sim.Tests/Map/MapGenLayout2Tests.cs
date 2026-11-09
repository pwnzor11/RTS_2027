// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Data;
using Rts.Data.MapGen;
using Rts.Sim.Map;
using Xunit.Abstractions;

namespace Rts.Sim.Tests.Map;

/// <summary>M12-07 / M12-08 / M12-09..11: generator layout 2 (zone skeleton first) for all four biomes, and its fairness checks.</summary>
public sealed class MapGenLayout2Tests
{
    private readonly ITestOutputHelper _out;

    /// <summary>Creates the test class.</summary>
    public MapGenLayout2Tests(ITestOutputHelper output) => _out = output;

    private static MapGenParams P(MapSize size, int players, ulong seed, Biome biome = Biome.RustBelt) => new(biome, size, players, seed) { Layout = MapGenerator.ZonedLayout };

    [Theory]
    [InlineData(MapSize.Medium, 2, 1UL)]
    [InlineData(MapSize.Small, 2, 7UL)]
    [InlineData(MapSize.Medium, 4, 3UL)]
    public void Layout2_SameParams_SameBytes(MapSize size, int players, ulong seed)
    {
        MapData a = MapGenerator.Generate(P(size, players, seed));
        MapData b = MapGenerator.Generate(P(size, players, seed));
        Assert.Equal(MapHasher.Compute(a), MapHasher.Compute(b));
        Assert.Equal(a.Land, b.Land);
        Assert.Equal(a.Overlay, b.Overlay);
        Assert.Equal(a.Density, b.Density);
    }

    [Fact]
    public void CurrentLayout_StaysOne_AndDefaultIsUnchanged()
    {
        Assert.Equal(1, MapGenerator.CurrentLayout);
        Assert.Equal(MapGenerator.CurrentLayout, new MapGenParams(Biome.RustBelt, MapSize.Medium, 2, 1UL).Layout);
        Assert.NotEqual(
            MapHasher.Compute(MapGenerator.Generate(new MapGenParams(Biome.RustBelt, MapSize.Medium, 2, 1UL))),
            MapHasher.Compute(MapGenerator.Generate(P(MapSize.Medium, 2, 1UL))));
    }

    [Theory]
    [InlineData(Biome.AshPlains)]
    [InlineData(Biome.GlassCoast)]
    [InlineData(Biome.Mountains)]
    public void Layout2_OtherBiomes_AreDeterministicAndKeepTheirTheater(Biome biome)
    {
        MapData a = MapGenerator.Generate(P(MapSize.Medium, 2, 1UL, biome));
        MapData b = MapGenerator.Generate(P(MapSize.Medium, 2, 1UL, biome));
        Assert.Equal(MapHasher.Compute(a), MapHasher.Compute(b));
        Assert.NotEqual(MapHasher.Compute(a), MapHasher.Compute(MapGenerator.Generate(P(MapSize.Medium, 2, 1UL))));
        Assert.Equal(BiomeCatalog.For(biome).Theater, a.Theater);
    }

    [Theory]
    [InlineData(MapSize.Medium, 2)]
    [InlineData(MapSize.Medium, 4)]
    public void Layout2_Map_IsSymmetricAndFair(MapSize size, int players)
    {
        (MapData map, MapMeta meta) = MapGenerator.GenerateWithMeta(P(size, players, 5UL));
        Assert.DoesNotContain(MapValidator.Validate(map, meta, null), i => i.Severity == MapIssueSeverity.Error);
        IReadOnlyList<LayoutCheck> checks = LayoutChecks.Evaluate(map, meta);
        Assert.Contains(checks, c => c.Name == "lane count");
        Assert.Contains(checks, c => c.Name == "prop safety");
        Assert.True(checks.Single(c => c.Name == "resource parity").Pass);
    }

    /// <summary>50 seeds x Small/Medium/Large x 2/3/4 players for each biome; filter with <c>--filter Category!=Slow</c>. Every map must generate
    /// with a clean validator; the pass and retry rates are printed, and at least 42 of 50 seeds must pass every layout check.</summary>
    [Trait("Category", "Slow")]
    [Theory]
    [InlineData(Biome.RustBelt)]
    [InlineData(Biome.Mountains)]
    [InlineData(Biome.AshPlains)]
    [InlineData(Biome.GlassCoast)]
    public void Layout2_Sweep_50Seeds_AllSizes_AllPlayerCounts(Biome biome)
    {
        foreach (MapSize size in new[] { MapSize.Small, MapSize.Medium, MapSize.Large })
            foreach (int players in new[] { 2, 3, 4 })
            {
                int total = 50;
                int clean = 0, retries = 0;
                for (ulong seed = 1; seed <= (ulong)total; seed++)
                {
                    (MapData map, MapMeta meta) = MapGenerator.GenerateWithMeta(P(size, players, seed, biome), BiomeCatalog.For(biome), out int attempts);
                    retries += attempts - 1;
                    if (MapGenerator.FailedChecks == 0) clean++;
                    Assert.DoesNotContain(MapValidator.Validate(map, meta, null), i => i.Severity == MapIssueSeverity.Error);
                }
                _out.WriteLine($"{biome} {size} {players}p: {clean}/{total} seeds pass every layout check, {retries / (double)total:0.0} retries per seed (cap {MapGenerator.MaxZonedAttempts - 1})");
                Assert.True(clean >= total * 42 / 50, $"{biome} {size} {players}p: only {clean}/{total} seeds pass the layout checks");
            }
    }

    /// <summary>D-124: an Expedition player is guaranteed a coast. With an ocean requested, layout 2 puts sea behind the mains in every biome and writes
    /// convoy cells and one landing per start; the finished (turned) map passes the validator, naval checks included.</summary>
    [Theory]
    [InlineData(Biome.RustBelt)]
    [InlineData(Biome.Mountains)]
    [InlineData(Biome.AshPlains)]
    [InlineData(Biome.GlassCoast)]
    public void Layout2_WithOcean_HasSeaConvoyAndLandings(Biome biome)
    {
        foreach ((MapSize size, int players) in new[] { (MapSize.Medium, 2), (MapSize.Small, 2), (MapSize.Large, 2), (MapSize.Medium, 3), (MapSize.Medium, 4), (MapSize.Small, 4) })
            for (ulong seed = 1; seed <= 6; seed++)
            {
                (MapData map, MapMeta meta) = MapGenerator.GenerateWithMeta(P(size, players, seed, biome) with { OceanEdges = OceanEdges.North | OceanEdges.South });
                Assert.True(meta.ConvoyEdge.Count > 0, $"{biome} {size} {players}p seed {seed}: no convoy cells");
                Assert.True(meta.Landing.Count >= 1, $"{biome} {size} {players}p seed {seed}: no landing");
                Assert.DoesNotContain(MapValidator.Validate(map, meta, null), i => i.Severity == MapIssueSeverity.Error);
            }
    }
}
