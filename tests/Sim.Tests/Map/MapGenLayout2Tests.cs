// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Data;
using Rts.Data.MapGen;
using Rts.Sim.Map;
using Xunit.Abstractions;

namespace Rts.Sim.Tests.Map;

/// <summary>M12-07 / M12-08: generator layout 2 (zone skeleton first) for Rust Belt, and its fairness checks.</summary>
public sealed class MapGenLayout2Tests
{
    private readonly ITestOutputHelper _out;

    /// <summary>Creates the test class.</summary>
    public MapGenLayout2Tests(ITestOutputHelper output) => _out = output;

    private static MapGenParams P(MapSize size, int players, ulong seed) => new(Biome.RustBelt, size, players, seed) { Layout = MapGenerator.ZonedLayout };

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
    public void Layout2_OtherBiomes_ThrowNotBuiltYet(Biome biome)
    {
        var ex = Assert.Throws<InvalidOperationException>(() => MapGenerator.Generate(new MapGenParams(biome, MapSize.Medium, 2, 1UL) { Layout = MapGenerator.ZonedLayout }));
        Assert.Contains("not built yet", ex.Message);
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

    /// <summary>50 seeds (2 players) or 12 seeds (3 and 4 players) x Small/Medium/Large; filter with <c>--filter Category!=Slow</c>. Every map must generate with a clean
    /// validator; the pass and retry rates are printed, and at least 10 of 12 (or 42 of 50) seeds must pass every layout check.</summary>
    [Trait("Category", "Slow")]
    [Fact]
    public void Layout2_Sweep_50Seeds_AllSizes_AllPlayerCounts()
    {
        foreach (MapSize size in new[] { MapSize.Small, MapSize.Medium, MapSize.Large })
            foreach (int players in new[] { 2, 3, 4 })
            {
                int total = players == 2 ? 50 : 12;   // 3 and 4 player maps retry far more often, so they get fewer seeds to keep the sweep to minutes
                int clean = 0, retries = 0;
                for (ulong seed = 1; seed <= (ulong)total; seed++)
                {
                    (MapData map, MapMeta meta) = MapGenerator.GenerateWithMeta(P(size, players, seed), BiomeCatalog.For(Biome.RustBelt), out int attempts);
                    retries += attempts - 1;
                    if (MapGenerator.FailedChecks == 0) clean++;
                    Assert.DoesNotContain(MapValidator.Validate(map, meta, null), i => i.Severity == MapIssueSeverity.Error);
                }
                _out.WriteLine($"{size} {players}p: {clean}/{total} seeds pass every layout check, {retries / (double)total:0.0} retries per seed (cap {MapGenerator.MaxZonedAttempts - 1})");
                Assert.True(clean >= total * 10 / 12, $"{size} {players}p: only {clean}/{total} seeds pass the layout checks");
            }
    }
}
