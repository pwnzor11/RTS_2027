// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Data;
using Rts.Data.MapGen;
using Rts.Sim.Core;
using Rts.Sim.Map;
using Rts.Sim.Rules;

namespace Rts.Sim.Tests.Map;

/// <summary>M4-02 (design 08 §1 checks, §8): every biome × size × player count is connected for Foot and Tracked, fair (path
/// equality), has a 5×5 start square, ≥ 12 cells between starts and ≥ 2 fields per start; symmetry is exact; shores are Shallow.</summary>
public sealed class MapGenFairnessTests
{
    private static readonly Lazy<TerrainTable> FixtureTerrain = new(() => RulesLoader.Load(TestData.RulesDir).Sim.Terrain);

    public static TheoryData<Biome, MapSize, int> AllCombos()
    {
        var d = new TheoryData<Biome, MapSize, int>();
        foreach (Biome b in Enum.GetValues<Biome>())
            foreach (MapSize s in Enum.GetValues<MapSize>())
                for (int p = 2; p <= 4; p++) d.Add(b, s, p);
        return d;
    }

    [Theory]
    [MemberData(nameof(AllCombos))]
    public void EveryCombination_PassesAllChecks(Biome biome, MapSize size, int players)
    {
        foreach (ulong seed in new ulong[] { 1, 424242 })
        {
            (MapData map, MapMeta meta) = MapGenerator.GenerateWithMeta(new MapGenParams(biome, size, players, seed));
            Assert.Equal(players, map.StartPositions.Count);
            Assert.Equal(MapGenerator.CellsFor(size), map.Width);

            // Validator, both with the built-in rule and the fixture terrain table: no errors, no warnings.
            Assert.Empty(MapValidator.Validate(map, meta, null));
            Assert.Empty(MapValidator.Validate(map, meta, FixtureTerrain.Value));

            bool[]? blocked = MapAnalysis.FeatureMask(map, meta.Features);
            int[] fields = MapAnalysis.FieldIds(map, out int fieldCount);
            foreach (CellCoord s in map.StartPositions)
            {
                // Explicit §1 checks (the validator covers them too).
                foreach (Locomotion loco in new[] { Locomotion.Foot, Locomotion.Tracked })
                {
                    int[] d = MapAnalysis.Distances(map, s, loco, FixtureTerrain.Value, blocked);
                    Assert.All(map.StartPositions, t => Assert.NotEqual(MapAnalysis.Unreachable, d[map.IndexOf(t)]));
                }
                int[] dt = MapAnalysis.Distances(map, s, Locomotion.Tracked, null, blocked);
                Assert.True(MapAnalysis.FieldsWithin(dt, fields, fieldCount, 14) >= 2);
                for (int dy = -2; dy <= 2; dy++)
                    for (int dx = -2; dx <= 2; dx++)
                    {
                        int i = map.IndexOf(new CellCoord(s.X + dx, s.Y + dy));
                        Assert.True(FixtureTerrain.Value.Buildable(map.Land[i]));
                        Assert.Equal(OverlayKind.None, map.Overlay[i]);
                        Assert.Equal(0, map.HeightLevel[i]);
                    }
                foreach (CellCoord t in map.StartPositions)
                    if (t != s) Assert.True(Math.Max(Math.Abs(s.X - t.X), Math.Abs(s.Y - t.Y)) >= 12);
            }

            PathSpread spread = MapAnalysis.Spread(map, map.StartPositions, null, blocked);
            Assert.Equal(0, spread.NearestField);
            Assert.Equal(0, spread.Centre);
            if (players != 3) Assert.Equal(0, spread.OtherStarts);   // 3 players: one start is next to both others (Q-M4-a)
            PathSpread real = MapAnalysis.Spread(map, map.StartPositions, FixtureTerrain.Value, blocked);
            Assert.Equal(0, real.NearestField);
            Assert.Equal(0, real.Centre);
            if (players != 3) Assert.Equal(0, real.OtherStarts);

            AssertShoreRings(map);
        }
    }

    [Theory]
    [InlineData(Biome.RustBelt, 2, MapSymmetry.Rot2)]
    [InlineData(Biome.GlassCoast, 2, MapSymmetry.Rot2)]
    [InlineData(Biome.Mountains, 2, MapSymmetry.MirrorX)]
    [InlineData(Biome.AshPlains, 2, MapSymmetry.MirrorY)]
    [InlineData(Biome.Mountains, 3, MapSymmetry.Rot4)]
    [InlineData(Biome.AshPlains, 4, MapSymmetry.Rot4)]
    public void Map_IsExactlySymmetric_AndStartsAreImages(Biome biome, int players, MapSymmetry expected)
    {
        (MapData map, MapMeta meta) = MapGenerator.GenerateWithMeta(new MapGenParams(biome, MapSize.Medium, players, 31));
        Assert.Equal(expected, meta.Symmetry);
        int n = map.Width;
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                int i = y * n + x;
                foreach ((int ix, int iy) in MapSymmetries.Images(expected, n, n, x, y))
                {
                    int j = iy * n + ix;
                    Assert.Equal(map.Land[i], map.Land[j]);
                    Assert.Equal(map.HeightLevel[i], map.HeightLevel[j]);
                    Assert.Equal(map.Overlay[i], map.Overlay[j]);
                    Assert.Equal(map.Density[i], map.Density[j]);
                }
            }
        CellCoord s0 = map.StartPositions[0];
        var images = MapSymmetries.Images(expected, n, n, s0.X, s0.Y).Select(c => new CellCoord(c.X, c.Y)).ToArray();
        Assert.All(map.StartPositions, s => Assert.Contains(s, images));
        Assert.Equal(players, map.StartPositions.Distinct().Count());
        Assert.Equal(0, meta.Features.Count % MapSymmetries.Order(expected));
    }

    [Fact]
    public void ThreePlayers_LeaveOneRot4StartEmpty_RotatedBySeed()
    {
        var empties = new List<CellCoord>();
        for (ulong seed = 0; seed < 4; seed++)
        {
            MapData map = MapGenerator.Generate(new MapGenParams(Biome.RustBelt, MapSize.Small, 3, seed));
            CellCoord s0 = map.StartPositions[0];
            var orbit = MapSymmetries.Images(MapSymmetry.Rot4, 64, 64, s0.X, s0.Y).Select(c => new CellCoord(c.X, c.Y)).ToArray();
            CellCoord empty = Assert.Single(orbit, c => !map.StartPositions.Contains(c));
            empties.Add(empty);
            // The empty start's fields are still on the map (a contested expansion).
            int[] d = MapAnalysis.Distances(map, empty, Locomotion.Tracked, null);
            int[] f = MapAnalysis.FieldIds(map, out int fc);
            Assert.True(MapAnalysis.FieldsWithin(d, f, fc, 14) >= 2);
        }
        Assert.Equal(4, empties.Distinct().Count());
    }

    [Theory]
    [InlineData(MapSize.Small)]
    [InlineData(MapSize.Medium)]
    [InlineData(MapSize.Large)]
    public void GlassCoast_EdgeRingIsDeepWater_WithShallowInside(MapSize size)
    {
        foreach (int players in new[] { 2, 4 })
        {
            MapData map = MapGenerator.Generate(new MapGenParams(Biome.GlassCoast, size, players, 8));
            int n = map.Width;
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                    if (Math.Min(Math.Min(x, y), Math.Min(n - 1 - x, n - 1 - y)) < 3)
                        Assert.Equal(LandType.Water, map.Land[y * n + x]);
            // Walking in from the middle of each edge: Water, then at least one Shallow, then land.
            foreach ((int dx, int dy, int x0, int y0) in new[] { (1, 0, 0, n / 2), (-1, 0, n - 1, n / 2), (0, 1, n / 2, 0), (0, -1, n / 2, n - 1) })
            {
                int x = x0, y = y0;
                while (map.Land[y * n + x] == LandType.Water) { x += dx; y += dy; }
                Assert.Equal(LandType.Shallow, map.Land[y * n + x]);
            }
            AssertShoreRings(map);
            foreach (CellCoord s in map.StartPositions)
            {
                // Shore start (design 07 §6.7): deep water within 16 cells of path for Hover.
                int[] d = MapAnalysis.Distances(map, s, Locomotion.Hover, null);
                Assert.Contains(Enumerable.Range(0, n * n), i => map.Land[i] == LandType.Water && d[i] <= 16);
            }
        }
    }

    [Fact]
    public void Biomes_HaveTheirCharacter()
    {
        MapData Gen(Biome b) => MapGenerator.Generate(new MapGenParams(b, MapSize.Medium, 4, 99));
        MapData rust = Gen(Biome.RustBelt), glass = Gen(Biome.GlassCoast), mtn = Gen(Biome.Mountains), ash = Gen(Biome.AshPlains);
        int Count(MapData m, LandType l) => m.Land.Count(x => x == l);
        Assert.True(Count(rust, LandType.Road) > 500, "Rust Belt road grid");
        Assert.True(Count(rust, LandType.Rock) > 0 && Count(rust, LandType.Rough) > 0 && Count(rust, LandType.Water) > 0);
        Assert.True(Count(glass, LandType.Water) > 96 * 4 * 3 && Count(glass, LandType.Shallow) > 0 && Count(glass, LandType.Rock) > 0);
        Assert.True(mtn.HeightLevel.Any(h => h == 2) && Count(mtn, LandType.Cliff) > 100, "Mountains plateaus");
        Assert.True(Count(ash, LandType.Ash) > 96 * 96 / 2 && Count(ash, LandType.Water) > 0 && Count(ash, LandType.Shallow) > 0);
        Assert.True(ash.Overlay.Count(o => o == OverlayKind.Ashfall) > rust.Overlay.Count(o => o == OverlayKind.Ashfall) / 2);
        foreach (MapData m in new[] { rust, glass, mtn, ash })
        {
            Assert.Contains(OverlayKind.Pitchblende, m.Overlay);
            for (int i = 0; i < m.Overlay.Count; i++)
                if (m.Overlay[i] != OverlayKind.None && !OverlayKinds.IsProp(m.Overlay[i])) Assert.InRange(m.Density[i], 1, 12);   // props carry a 0..2 variant
        }
        (_, MapMeta rustMeta) = MapGenerator.GenerateWithMeta(new MapGenParams(Biome.RustBelt, MapSize.Medium, 2, 99));
        Assert.Contains(rustMeta.Features, f => f.Type == "neu_ruin_tenement");
        Assert.All(rustMeta.Features, f => Assert.StartsWith("neu_ruin_", f.Type));
    }

    /// <summary>Design 08 §1: deep water is ringed by Shallow wherever it meets land (bridges and cliffs excepted).</summary>
    private static void AssertShoreRings(MapData map)
    {
        int n = map.Width;
        for (int i = 0; i < n * n; i++)
        {
            if (map.Land[i] != LandType.Water) continue;
            int x = i % n, y = i / n;
            for (int dy = -1; dy <= 1; dy++)
                for (int dx = -1; dx <= 1; dx++)
                {
                    int nx = x + dx, ny = y + dy;
                    if (nx < 0 || ny < 0 || nx >= n || ny >= n) continue;
                    LandType l = map.Land[ny * n + nx];
                    Assert.True(l is LandType.Water or LandType.Shallow or LandType.Road or LandType.Cliff,
                        $"{l} at ({nx},{ny}) touches deep water at ({x},{y})");
                }
        }
    }
}
