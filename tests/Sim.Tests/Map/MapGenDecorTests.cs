// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Data;
using Rts.Data.MapGen;
using Rts.Sim.Core;
using Rts.Sim.Map;
using Rts.Sim.Rules;

namespace Rts.Sim.Tests.Map;

/// <summary>M12-05 (design 16 s2.5, D-180): the generator's blocking-prop pass, its data file, symmetry, keep-outs, connectivity guard and the
/// generator version (<see cref="MapGenParams.Layout"/>).</summary>
public sealed class MapGenDecorTests
{
    private static readonly Biome[] AllBiomes = Enum.GetValues<Biome>();

    public static IEnumerable<object[]> Matrix()
    {
        foreach (Biome b in AllBiomes)
            foreach (MapSize s in new[] { MapSize.Small, MapSize.Medium })
                foreach (int players in new[] { 2, 3, 4 })
                    foreach (ulong seed in new[] { 1UL, 2UL })
                        yield return new object[] { b, s, players, seed };
    }

    [Theory]
    [MemberData(nameof(Matrix))]
    public void Generated_maps_have_props_that_pass_every_check(Biome biome, MapSize size, int players, ulong seed)
    {
        (MapData map, MapMeta meta) = MapGenerator.GenerateWithMeta(new MapGenParams(biome, size, players, seed));
        Assert.Empty(MapValidator.Validate(map, meta, null));   // includes symmetry, connectivity, prop_blocks_path, prop_on_resource, prop_in_start_square

        MapReport r = MapReport.Measure(map, meta);
        Assert.InRange(r.PropsPer100, 40, 400);                      // study range 0.4 to 3.6 per 100 (a little slack for yards)
        Assert.True(r.PropClusterShare >= 500, $"{r.PropClusterShare} per mille of props in clusters of 4+");
        Assert.True(r.DeadPermille <= 80, $"dead ground {r.DeadPermille} per mille");

        // Props keep out of the start surroundings (the 8-cell reserve) and nearly out of the 12-cell ring (only beside a cliff, like the study).
        bool cramped = size == MapSize.Small && (biome == Biome.Mountains || players > 2);   // plateau cliffs sit beside the starts on a small map (the study allows props at a cliff edge)
        Assert.True(cramped || r.PropNearStartPermille <= (size == MapSize.Small ? 350 : 80), $"{r.PropNearStartPermille} per mille of props within 12 cells of a start");
        for (int i = 0; i < map.Overlay.Count; i++)
        {
            if (!OverlayKinds.IsProp(map.Overlay[i])) continue;
            foreach (CellCoord s in map.StartPositions)
                Assert.True(Math.Max(Math.Abs(i % map.Width - s.X), Math.Abs(i / map.Width - s.Y)) > 8, $"prop at {i % map.Width},{i / map.Width} near start {s}");
        }
    }

    [Fact]
    public void Props_are_symmetric_and_use_only_the_five_prop_kinds_with_a_variant()
    {
        (MapData map, MapMeta meta) = MapGenerator.GenerateWithMeta(new MapGenParams(Biome.RustBelt, MapSize.Medium, 2, 5));
        Assert.Equal(MapSymmetry.Rot2, meta.Symmetry);
        int n = map.Width * map.Height, props = 0;
        for (int i = 0; i < n; i++)
        {
            int m = n - 1 - i;
            Assert.Equal(map.Overlay[i], map.Overlay[m]);
            Assert.Equal(map.Density[i], map.Density[m]);
            if (!OverlayKinds.IsProp(map.Overlay[i])) continue;
            props++;
            Assert.InRange(map.Density[i], 0, 2);
        }
        Assert.True(props > 100);
    }

    [Fact]
    public void Each_biome_places_only_the_kinds_its_data_names()
    {
        foreach (Biome b in AllBiomes)
        {
            BiomeParams bp = BiomeCatalog.For(b);
            var allowed = bp.Decor.Kinds.Concat(bp.Decor.YardKinds).Select(k => k.Kind).ToHashSet();
            MapData map = MapGenerator.Generate(new MapGenParams(b, MapSize.Medium, 2, 3));
            var found = map.Overlay.Where(OverlayKinds.IsProp).Distinct().ToList();
            Assert.NotEmpty(found);
            Assert.All(found, k => Assert.Contains(k, allowed));
        }
    }

    [Fact]
    public void Ruins_get_a_yard_of_props()
    {
        (MapData map, MapMeta meta) = MapGenerator.GenerateWithMeta(new MapGenParams(Biome.RustBelt, MapSize.Medium, 2, 11));
        Assert.NotEmpty(meta.Features);
        foreach (MapFeature f in meta.Features.Take(2))
        {
            (int fw, int fh) = MapAnalysis.FootprintOf(f.Type);
            int near = 0;
            for (int y = f.Cell.Y - 5; y < f.Cell.Y + fh + 5; y++)
                for (int x = f.Cell.X - 5; x < f.Cell.X + fw + 5; x++)
                    if (map.Contains(new CellCoord(x, y)) && OverlayKinds.IsProp(map.Overlay[y * map.Width + x])) near++;
            Assert.True(near >= 2, $"ruin at {f.Cell} has {near} props around it");
        }
    }

    [Fact]
    public void The_pass_is_deterministic_and_a_zero_budget_biome_adds_nothing()
    {
        var p = new MapGenParams(Biome.AshPlains, MapSize.Small, 2, 8);
        Assert.Equal(MapHasher.Compute(MapGenerator.Generate(p)), MapHasher.Compute(MapGenerator.Generate(p)));
        string text = File.ReadAllText(BiomeCatalog.FindFile(AppContext.BaseDirectory)!).Replace("\r\n", "\n");
        IReadOnlyList<BiomeParams> none = BiomeCatalog.Parse(System.Text.RegularExpressions.Regex.Replace(text, @"prop_permille = \d+", "prop_permille = 0"), "b.toml");
        MapData bare = MapGenerator.GenerateWithMeta(p, none[(int)Biome.AshPlains]).Map;
        Assert.DoesNotContain(bare.Overlay, OverlayKinds.IsProp);
    }

    [Fact]
    public void Decor_rules_are_validated()
    {
        string text = File.ReadAllText(BiomeCatalog.FindFile(AppContext.BaseDirectory)!).Replace("\r\n", "\n");
        Assert.Throws<RulesException>(() => BiomeCatalog.Parse(text.Replace("kinds = [\"tree:3\"", "kinds = [\"bush:3\""), "b.toml"));
        Assert.Throws<RulesException>(() => BiomeCatalog.Parse(text.Replace("cluster_min = 3", "cluster_min = 20"), "b.toml"));
        Assert.Throws<RulesException>(() => BiomeCatalog.Parse(text.Replace("yard_min = 4", "yard_mim = 4"), "b.toml"));
    }

    [Fact]
    public void An_older_generator_layout_is_refused_and_the_layout_is_stored_in_the_file()
    {
        var old = new MapGenParams(Biome.RustBelt, MapSize.Small, 2, 4, null, OceanEdges.None, 0);
        var e = Assert.Throws<InvalidOperationException>(() => MapGenerator.Generate(old));
        Assert.Equal("This save was made with an older map generator and can't be loaded.", e.Message);

        (MapData map, MapMeta meta) = MapGenerator.GenerateWithMeta(new MapGenParams(Biome.RustBelt, MapSize.Small, 2, 4));
        Assert.Equal(MapGenerator.CurrentLayout, meta.Generator!.Layout);
        string path = Path.Combine(Path.GetTempPath(), "rts_layout_" + Guid.NewGuid().ToString("N") + ".map.toml");
        MapFile.Save(map, meta, path);
        string text = File.ReadAllText(path);
        Assert.Contains("layout = 1", text);
        Assert.Equal(1, MapFile.LoadWithMeta(path).Meta.Generator!.Layout);
        File.WriteAllText(path, text.Replace("layout = 1\n", "").Replace("layout = 1\r\n", ""));
        Assert.Equal(0, MapFile.LoadWithMeta(path).Meta.Generator!.Layout);   // a file from before the field names generator 0
    }
}
