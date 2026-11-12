// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Data;
using Rts.Data.MapGen;
using Rts.Sim.Map;
using Rts.Sim.Rules;
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

    /// <summary>Q-68 (v6): Ash Plains 2-player Medium and Large maps are half-turn symmetric like the other biomes (the bridge + broken causeway pair sits on both decks).</summary>
    [Theory]
    [InlineData(MapSize.Medium)]
    [InlineData(MapSize.Large)]
    public void Layout2_AshPlains_TwoPlayers_IsHalfTurnWithThePairOnBothDecks(MapSize size)
    {
        for (ulong seed = 1; seed <= 6; seed++)
        {
            (MapData map, MapMeta meta) = MapGenerator.GenerateWithMeta(P(size, 2, seed, Biome.AshPlains));
            Assert.Equal(MapSymmetry.Rot2, meta.Symmetry);
            int n = map.Width, shallowRoad = 0;
            for (int i = 0; i < n * n; i++)
            {
                int j = n * n - 1 - i;   // the half turn of cell i
                Assert.Equal(map.Land[i], map.Land[j]);
                Assert.Equal(map.Overlay[i], map.Overlay[j]);
                if (map.Land[i] == LandType.Road) shallowRoad++;
            }
            Assert.True(shallowRoad > 0);
        }
    }

    /// <summary>v6 (Q-69): the harvest travel distance (path cells from the start to the nearest field cell, and to the 60 nearest) stays inside a band per biome on
    /// Medium 2-player maps. v5 measured 4.2 / 7.9; layout 1 measured 5.5-6.5 / 20-67 (20 seeds); v6 about 9 / 13.</summary>
    [Theory]
    [InlineData(Biome.RustBelt)]
    [InlineData(Biome.Mountains)]
    [InlineData(Biome.AshPlains)]
    [InlineData(Biome.GlassCoast)]
    public void Layout2_HarvestDistance_StaysInItsBand(Biome biome)
    {
        for (ulong seed = 1; seed <= 6; seed++)
        {
            (MapData map, MapMeta meta) = MapGenerator.GenerateWithMeta(P(MapSize.Medium, 2, seed, biome));
            (int near, int mean) = MapReport.HarvestDistance(map, meta);
            Assert.InRange(near, 70, 140);
            Assert.InRange(mean, 100, 240);
        }
    }

    /// <summary>v6 Mountains skew study (Directorate 30/16, Left Behind 29/13 by start index in the 64-game mirrors): over 50 seeds the two starts of a Mountains
    /// 2-player map are exact half-turn images in land, height, overlay, density and ruin features, so the skew is not in the map data.</summary>
    [Fact]
    public void Layout2_Mountains_TwoPlayers_StartsAreExactImages()
    {
        for (ulong seed = 1; seed <= 50; seed++)
        {
            (MapData map, MapMeta meta) = MapGenerator.GenerateWithMeta(P(MapSize.Medium, 2, seed, Biome.Mountains));
            int n = map.Width, t = n * n - 1;
            for (int i = 0; i < n * n; i++)
            {
                Assert.Equal(map.Land[i], map.Land[t - i]);
                Assert.Equal(map.HeightLevel[i], map.HeightLevel[t - i]);
                Assert.Equal(map.Overlay[i], map.Overlay[t - i]);
                Assert.Equal(map.Density[i], map.Density[t - i]);
            }
            Assert.Equal(map.StartPositions[0].X, n - 1 - map.StartPositions[1].X);
            Assert.Equal(map.StartPositions[0].Y, n - 1 - map.StartPositions[1].Y);
            foreach (MapFeature f in meta.Features)
                Assert.Contains(meta.Features, g => g.Type == f.Type && Math.Abs(g.Cell.X - (n - 1 - f.Cell.X)) <= 3 && Math.Abs(g.Cell.Y - (n - 1 - f.Cell.Y)) <= 3);   // a ruin's cell is its corner, so its image is within its footprint
        }
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

    private static long Money(MapData m)
    {
        long t = 0;
        for (int i = 0; i < m.Overlay.Count; i++)
            if (OverlayKinds.IsResource(m.Overlay[i])) t += (m.Overlay[i] == OverlayKind.Pitchblende ? 2 : 1) * m.Density[i];
        return t;
    }

    /// <summary>Q-70 (D-205): layout 2 carries less money on Glass Coast (-20 %) and Rust Belt (-10 %); Mountains and Ash Plains are unchanged.</summary>
    [Theory]
    [InlineData(Biome.GlassCoast)]
    [InlineData(Biome.RustBelt)]
    [InlineData(Biome.Mountains)]
    [InlineData(Biome.AshPlains)]
    public void Layout2_Money_PerBiome(Biome biome)
    {
        long m = Money(MapGenerator.Generate(P(MapSize.Medium, 2, 1UL, biome)));
        _out.WriteLine($"L2MONEY {biome} {m}");
        Assert.Equal(L2Money(biome), m);
    }

    /// <summary>Before Q-70: 470 for Rust, Mountains and Ash, 352 for Glass (integer cell densities round the -10 % / -20 % money target to -10 % / -11 % on this seed).</summary>
    private static long L2Money(Biome b) => b == Biome.GlassCoast ? 312 : b == Biome.RustBelt ? 424 : 470;

    /// <summary>Q-70: layout 1 maps do not move (pinned from the build before the Q-70 change).</summary>
    [Theory]
    [InlineData(Biome.RustBelt)]
    [InlineData(Biome.GlassCoast)]
    [InlineData(Biome.Mountains)]
    [InlineData(Biome.AshPlains)]
    public void Layout1_MapHash_IsUnchangedByQ70(Biome biome)
    {
        ulong h = MapHasher.Compute(MapGenerator.Generate(new MapGenParams(biome, MapSize.Medium, 2, 1UL)));
        _out.WriteLine($"L1 {biome} {h:x16}");
        Assert.Equal(L1Hash(biome), h);
    }

    private static ulong L1Hash(Biome b) => b switch
    {
        Biome.RustBelt => 0x09378d3102f55d9dUL,
        Biome.AshPlains => 0xc7d912b969e88e28UL,
        Biome.Mountains => 0x3750ac7cfc2dc9c6UL,
        _ => 0xf1241fd4cdaa5a6bUL,
    };
}
