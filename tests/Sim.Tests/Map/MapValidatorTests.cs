// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Data;
using Rts.Sim.Core;
using Rts.Sim.Map;
using Rts.Sim.Rules;

namespace Rts.Sim.Tests.Map;

/// <summary>M4-02 (design 08 §5/§6): each <see cref="MapValidator"/> issue code on a crafted map, and the file round trip that
/// <c>map gen</c> / <c>map validate</c> perform.</summary>
public sealed class MapValidatorTests
{
    private const int N = 40;

    /// <summary>Flat 40×40 map, starts at (6,6) and (33,33), two Ashfall fields next to each start: no issues.</summary>
    private sealed class Builder
    {
        public LandType[] Land = new LandType[N * N];
        public byte[] Height = new byte[N * N];
        public OverlayKind[] Overlay = new OverlayKind[N * N];
        public byte[] Density = new byte[N * N];
        public List<CellCoord> Starts = new() { new CellCoord(6, 6), new CellCoord(N - 7, N - 7) };

        public Builder()
        {
            foreach (CellCoord s in Starts.ToArray())
            {
                int sx = s.X < N / 2 ? 1 : -1;
                Field(s.X + 5 * sx, s.Y); Field(s.X, s.Y + 5 * sx);
            }
        }

        public void Field(int x, int y) { Overlay[y * N + x] = OverlayKind.Ashfall; Density[y * N + x] = 6; Land[y * N + x] = LandType.Ash; }
        public void Set(int x, int y, LandType l) => Land[y * N + x] = l;
        public MapData Map() => new("crafted", "rustbelt", N, N, new ushort[N * N], Land, Height, Overlay, Density, Starts.ToArray());
    }

    private static string[] Codes(IReadOnlyList<MapIssue> issues) => issues.Select(i => i.Code).ToArray();

    [Fact]
    public void CleanMap_HasNoIssues()
    {
        Assert.Empty(MapValidator.Validate(new Builder().Map(), null, null));
        Assert.Empty(MapValidator.Validate(new Builder().Map(), null, RulesLoader.Load(TestData.RulesDir).Sim.Terrain));
    }

    [Fact]
    public void StartsCount()
    {
        var b = new Builder();
        b.Starts.RemoveAt(1);
        var issues = MapValidator.Validate(b.Map(), null, null);
        MapIssue i = Assert.Single(issues, x => x.Code == "starts_count");
        Assert.Equal(MapIssueSeverity.Error, i.Severity);
        b.Starts.AddRange(new[] { new CellCoord(33, 6), new CellCoord(6, 33), new CellCoord(33, 33), new CellCoord(20, 20) });
        Assert.Contains("starts_count", Codes(MapValidator.Validate(b.Map(), null, null)));
    }

    [Theory]
    [InlineData(LandType.Rock)]
    [InlineData(LandType.Rough)]
    [InlineData(LandType.Shallow)]
    public void StartSpace_UnbuildableLand(LandType land)
    {
        var b = new Builder();
        b.Set(8, 5, land);
        MapIssue i = Assert.Single(MapValidator.Validate(b.Map(), null, null), x => x.Code == "start_space");
        Assert.Equal(new CellCoord(6, 6), i.Cell);
        Assert.Equal(MapIssueSeverity.Error, i.Severity);
    }

    [Fact]
    public void StartSpace_ResourcesHeightAndEdge()
    {
        var b = new Builder();
        b.Field(5, 7);
        Assert.Contains("start_space", Codes(MapValidator.Validate(b.Map(), null, null)));
        b = new Builder();
        b.Height[4 * N + 4] = 1;
        Assert.Contains("start_space", Codes(MapValidator.Validate(b.Map(), null, null)));
        b = new Builder();
        b.Starts[0] = new CellCoord(1, 6);
        Assert.Contains("start_space", Codes(MapValidator.Validate(b.Map(), null, null)));
    }

    [Fact]
    public void StartDistance()
    {
        var b = new Builder();
        b.Starts[1] = new CellCoord(17, 9);   // 11 cells (Chebyshev)
        MapIssue i = Assert.Single(MapValidator.Validate(b.Map(), null, null), x => x.Code == "start_distance");
        Assert.Equal(MapIssueSeverity.Error, i.Severity);
        b.Starts[1] = new CellCoord(18, 9);   // 12: fine
        Assert.DoesNotContain("start_distance", Codes(MapValidator.Validate(b.Map(), null, null)));
    }

    [Fact]
    public void Connectivity_DeepWaterWall_BlocksFootAndTracked()
    {
        var b = new Builder();
        for (int x = 0; x < N; x++) b.Set(x, N / 2, LandType.Water);
        string[] codes = Codes(MapValidator.Validate(b.Map(), null, null));
        Assert.Contains("connectivity_foot", codes);
        Assert.Contains("connectivity_tracked", codes);
    }

    [Fact]
    public void Connectivity_ShallowWall_BlocksOnlyTracked()
    {
        var b = new Builder();
        for (int x = 0; x < N; x++) b.Set(x, N / 2, LandType.Shallow);
        string[] codes = Codes(MapValidator.Validate(b.Map(), null, null));
        Assert.DoesNotContain("connectivity_foot", codes);
        MapIssue i = Assert.Single(MapValidator.Validate(b.Map(), null, null), x => x.Code == "connectivity_tracked");
        Assert.Equal(new CellCoord(N - 7, N - 7), i.Cell);
        // Same with the real terrain table (Shallow: foot 40, tracked 0).
        codes = Codes(MapValidator.Validate(b.Map(), null, RulesLoader.Load(TestData.RulesDir).Sim.Terrain));
        Assert.DoesNotContain("connectivity_foot", codes);
        Assert.Contains("connectivity_tracked", codes);
    }

    [Fact]
    public void Connectivity_TerrainTableIsHonoured()
    {
        var b = new Builder();
        for (int x = 0; x < N; x++) b.Set(x, N / 2, LandType.Rough);
        Assert.Empty(MapValidator.Validate(b.Map(), null, null));
        var speeds = new int[RuleCounts.LandTypes * RuleCounts.Locomotions];
        Array.Fill(speeds, 100);
        speeds[(int)LandType.Rough * RuleCounts.Locomotions + (int)Locomotion.Foot] = 0;
        var table = new TerrainTable(speeds, Enumerable.Repeat(true, RuleCounts.LandTypes).ToArray());
        string[] codes = Codes(MapValidator.Validate(b.Map(), null, table));
        Assert.Contains("connectivity_foot", codes);
        Assert.DoesNotContain("connectivity_tracked", codes);
    }

    [Fact]
    public void FeatureCell_BadLandOverlapNearStartAndOffMap()
    {
        MapData map = new Builder().Map();
        MapMeta Meta(params MapFeature[] f) => new("rust_belt", MapSymmetry.None, 2, null, f);
        Assert.Empty(MapValidator.Validate(map, Meta(new MapFeature("neu_ruin_depot", new CellCoord(20, 10))), null));

        var b = new Builder();
        b.Set(21, 11, LandType.Rock);
        Assert.Contains("feature_cell", Codes(MapValidator.Validate(b.Map(), Meta(new MapFeature("neu_ruin_depot", new CellCoord(20, 10))), null)));

        var overlap = Meta(new MapFeature("neu_ruin_tenement", new CellCoord(20, 10)), new MapFeature("neu_ruin_depot", new CellCoord(22, 11)));
        MapIssue i = Assert.Single(MapValidator.Validate(map, overlap, null), x => x.Code == "feature_cell");
        Assert.Equal(new CellCoord(22, 11), i.Cell);
        Assert.Equal(MapIssueSeverity.Error, i.Severity);

        Assert.Contains("feature_cell", Codes(MapValidator.Validate(map, Meta(new MapFeature("neu_ruin_depot", new CellCoord(10, 10))), null)));
        Assert.Contains("feature_cell", Codes(MapValidator.Validate(map, Meta(new MapFeature("neu_ruin_tenement", new CellCoord(N - 2, 20))), null)));
    }

    [Fact]
    public void FeatureBlocksConnectivity()
    {
        var b = new Builder();
        for (int x = 0; x < N; x++) if (x < 18 || x > 19) b.Set(x, N / 2, LandType.Rock);
        MapData map = b.Map();
        Assert.Empty(MapValidator.Validate(map, null, null));
        var meta = new MapMeta("", MapSymmetry.None, 2, null, new[] { new MapFeature("neu_ruin_depot", new CellCoord(18, N / 2)) });
        Assert.Contains("connectivity_tracked", Codes(MapValidator.Validate(map, meta, null)));
    }

    [Fact]
    public void FewFields_IsAWarning()
    {
        var b = new Builder();
        b.Overlay[6 * N + 11] = OverlayKind.None;
        MapIssue i = Assert.Single(MapValidator.Validate(b.Map(), null, null));
        Assert.Equal("few_fields", i.Code);
        Assert.Equal(MapIssueSeverity.Warning, i.Severity);
        Assert.Equal(new CellCoord(6, 6), i.Cell);
    }

    [Fact]
    public void Symmetry_ReportsFirstDifferingCell()
    {
        var b = new Builder();   // the crafted map is Rot2-symmetric
        var meta = new MapMeta("", MapSymmetry.Rot2, 2, null, Array.Empty<MapFeature>());
        Assert.Empty(MapValidator.Validate(b.Map(), meta, null));
        b.Set(15, 3, LandType.Rough);
        b.Set(30, 2, LandType.Rough);
        MapIssue i = Assert.Single(MapValidator.Validate(b.Map(), meta, null));
        Assert.Equal("symmetry", i.Code);
        Assert.Equal(MapIssueSeverity.Warning, i.Severity);
        Assert.Equal(new CellCoord(30, 2), i.Cell);   // first in cell-index order
        Assert.Contains("symmetry", Codes(MapValidator.Validate(b.Map(), meta with { Symmetry = MapSymmetry.MirrorX }, null)));
    }

    [Fact]
    public void IssueOrder_IsFixed()
    {
        var b = new Builder();
        b.Set(6, 6, LandType.Rock);
        b.Starts[1] = new CellCoord(14, 14);
        for (int x = 0; x < N; x++) b.Set(x, 10, LandType.Water);
        b.Overlay[6 * N + 11] = OverlayKind.None;
        var meta = new MapMeta("", MapSymmetry.Rot2, 2, null, new[] { new MapFeature("neu_ruin_depot", new CellCoord(7, 7)) });
        string[] codes = Codes(MapValidator.Validate(b.Map(), meta, null)).Distinct().ToArray();
        Assert.Equal(new[] { "start_space", "start_distance", "connectivity_foot", "connectivity_tracked", "feature_cell", "few_fields", "symmetry" },
            codes);
    }

    [Theory]
    [InlineData(Biome.RustBelt, 2)]
    [InlineData(Biome.AshPlains, 3)]
    [InlineData(Biome.GlassCoast, 4)]
    public void GeneratedMap_RoundTripsThroughMapFile_AndValidatesClean(Biome biome, int players)
    {
        // What `map gen` then `map validate` do.
        (MapData map, MapMeta meta) = MapGenerator.GenerateWithMeta(new MapGenParams(biome, MapSize.Small, players, 2027));
        string dir = Path.Combine(Path.GetTempPath(), "rts_m402_" + Guid.NewGuid().ToString("N"));
        string path = Path.Combine(dir, map.Name + ".map.toml");
        try
        {
            MapFile.Save(map, meta, path);
            (MapData back, MapMeta backMeta) = MapFile.LoadWithMeta(path);
            Assert.Equal(MapHasher.Compute(map), MapHasher.Compute(back));
            Assert.Equal(meta.Symmetry, backMeta.Symmetry);
            Assert.Equal(meta.Generator, backMeta.Generator);
            Assert.Equal(meta.Features, backMeta.Features);
            Assert.Equal(players, backMeta.MaxPlayers);
            Assert.Empty(MapValidator.Validate(back, backMeta, RulesLoader.Load(TestData.RulesDir).Sim.Terrain));
        }
        finally
        {
            if (Directory.Exists(dir)) Directory.Delete(dir, true);
        }
    }
}
