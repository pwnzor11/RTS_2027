// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Data;
using Rts.Data.MapGen;
using Rts.Sim.Core;
using Rts.Sim.Map;
using Rts.Sim.Rules;

namespace Rts.Sim.Tests.Map;

/// <summary>M7-13: the opt-in ocean edge of the map generator, its convoy and landing markers, and the naval map validator checks.</summary>
public sealed class MapGenOceanTests
{
    /// <summary>Hashes captured on integ-s10 6d816c0, before the ocean option existed. M12-05 added the blocking-prop pass to every generated map
    /// (D-180: the generator is replaced), so the check now runs with the prop budget set to zero: the land, resources and ruins underneath are
    /// still byte-identical to the pre-M7 generator, and a map without props draws no random number the old generator did not.</summary>
    [Theory]
    [InlineData(Biome.RustBelt, MapSize.Small, 3, 1UL, 0x75be99624c8dc623UL)]
    [InlineData(Biome.RustBelt, MapSize.Small, 4, 77UL, 0xdad937e3ef7f422cUL)]
    [InlineData(Biome.RustBelt, MapSize.Medium, 3, 1UL, 0xd839e6ba7cdfcdeaUL)]
    [InlineData(Biome.RustBelt, MapSize.Medium, 4, 77UL, 0xbf69464c447730e1UL)]
    [InlineData(Biome.RustBelt, MapSize.Large, 3, 1UL, 0x66e3b597b42ee90bUL)]
    [InlineData(Biome.RustBelt, MapSize.Large, 4, 77UL, 0x600d026637573a37UL)]
    [InlineData(Biome.GlassCoast, MapSize.Small, 3, 1UL, 0xb4f82f44291b0f22UL)]
    [InlineData(Biome.GlassCoast, MapSize.Small, 4, 77UL, 0x9bd967e4373378baUL)]
    [InlineData(Biome.GlassCoast, MapSize.Medium, 3, 1UL, 0xf147f2caeaa02961UL)]
    [InlineData(Biome.GlassCoast, MapSize.Medium, 4, 77UL, 0xa18a79607236b784UL)]
    [InlineData(Biome.GlassCoast, MapSize.Large, 3, 1UL, 0xc50a40de475d7c82UL)]
    [InlineData(Biome.GlassCoast, MapSize.Large, 4, 77UL, 0x346be54ce38159abUL)]
    [InlineData(Biome.Mountains, MapSize.Small, 3, 1UL, 0x940cbba006dba045UL)]
    [InlineData(Biome.Mountains, MapSize.Small, 4, 77UL, 0xe6b8bb18cdc19e06UL)]
    [InlineData(Biome.Mountains, MapSize.Medium, 3, 1UL, 0x9933b7e9efa200d3UL)]
    [InlineData(Biome.Mountains, MapSize.Medium, 4, 77UL, 0x0cc0cf546e9eef56UL)]
    [InlineData(Biome.Mountains, MapSize.Large, 3, 1UL, 0x3057c79ebe39b0a1UL)]
    [InlineData(Biome.Mountains, MapSize.Large, 4, 77UL, 0x23c7306a460489aeUL)]
    [InlineData(Biome.AshPlains, MapSize.Small, 3, 1UL, 0x3abf1e01f1e5c7f2UL)]
    [InlineData(Biome.AshPlains, MapSize.Small, 4, 77UL, 0x4172c50524f5fbffUL)]
    [InlineData(Biome.AshPlains, MapSize.Medium, 3, 1UL, 0x324ee154b4fcd30eUL)]
    [InlineData(Biome.AshPlains, MapSize.Medium, 4, 77UL, 0xc05814f2563c1d74UL)]
    [InlineData(Biome.AshPlains, MapSize.Large, 3, 1UL, 0x207a4ac42b5b84ccUL)]
    [InlineData(Biome.AshPlains, MapSize.Large, 4, 77UL, 0x22f742d0bc692f94UL)]
    public void OptionOff_MapsAreByteIdenticalToPreM7(Biome biome, MapSize size, int players, ulong seed, ulong expected)
    {
        string text = File.ReadAllText(BiomeCatalog.FindFile(AppContext.BaseDirectory)!).Replace("\r\n", "\n");
        BiomeParams bare = BiomeCatalog.Parse(System.Text.RegularExpressions.Regex.Replace(text, @"prop_permille = \d+", "prop_permille = 0"), "b.toml")[(int)biome];
        var p = new MapGenParams(biome, size, players, seed);
        var (map, meta) = MapGenerator.GenerateWithMeta(p, bare);
        Assert.Equal(expected, MapHasher.Compute(map));
        Assert.Empty(meta.ConvoyEdge);
        Assert.Empty(meta.Landing);
        Assert.DoesNotContain(map.Land, l => l == LandType.Coast);
        Assert.DoesNotContain(map.Overlay, OverlayKinds.IsProp);
        Assert.Equal(expected, MapHasher.Compute(MapGenerator.Generate(new MapGenParams(biome, size, players, seed, null, OceanEdges.None), bare)));
    }

    [Theory]
    [InlineData(Biome.RustBelt, MapSize.Small, 2, 1UL, OceanEdges.North)]
    [InlineData(Biome.RustBelt, MapSize.Medium, 4, 5UL, OceanEdges.East)]
    [InlineData(Biome.Mountains, MapSize.Medium, 2, 9UL, OceanEdges.West | OceanEdges.East)]
    [InlineData(Biome.AshPlains, MapSize.Large, 3, 12UL, OceanEdges.South)]
    [InlineData(Biome.GlassCoast, MapSize.Small, 2, 3UL, OceanEdges.North | OceanEdges.South)]
    public void OptionOn_SeaCoastAndMarkers(Biome biome, MapSize size, int players, ulong seed, OceanEdges edges)
    {
        var p = new MapGenParams(biome, size, players, seed, null, edges);
        var (map, meta) = MapGenerator.GenerateWithMeta(p);

        Assert.Equal(edges, meta.Generator!.OceanEdges);
        Assert.Contains(map.Land, l => l == LandType.Coast);
        Assert.NotEmpty(meta.ConvoyEdge);
        Assert.NotEmpty(meta.Landing);
        foreach (CellCoord c in meta.ConvoyEdge)
        {
            Assert.True(c.X == 0 || c.Y == 0 || c.X == map.Width - 1 || c.Y == map.Height - 1, $"convoy cell {c} is not on the edge");
            Assert.Equal(LandType.Water, map.Land[map.IndexOf(c)]);
        }
        foreach (CellCoord c in meta.Landing) Assert.Equal(LandType.Coast, map.Land[map.IndexOf(c)]);

        // Every start's square is untouched by the sea and the generated map passes the full validator (naval checks included).
        Assert.Empty(MapValidator.Validate(map, meta, null));
        Assert.All(map.StartPositions, s => Assert.NotEqual(LandType.Coast, map.Land[map.IndexOf(s)]));

        // Deterministic.
        Assert.Equal(MapHasher.Compute(map), MapHasher.Compute(MapGenerator.Generate(p)));
        Assert.NotEqual(MapHasher.Compute(map), MapHasher.Compute(MapGenerator.Generate(p with { OceanEdges = OceanEdges.None })));
    }

    [Fact]
    public void OptionOn_ManySeeds_AlwaysProducesAValidNavalMap()
    {
        foreach (Biome biome in new[] { Biome.RustBelt, Biome.Mountains, Biome.AshPlains, Biome.GlassCoast })
            for (ulong seed = 1; seed <= 6; seed++)
                foreach (OceanEdges edges in new[] { OceanEdges.North, OceanEdges.East | OceanEdges.West })
                {
                    var (map, meta) = MapGenerator.GenerateWithMeta(new MapGenParams(biome, MapSize.Small, 2 + (int)(seed % 3), seed, null, edges));
                    Assert.Empty(MapValidator.Validate(map, meta, null));
                }
    }

    [Fact]
    public void OptionOn_SurvivesMapFileRoundTrip()
    {
        var p = new MapGenParams(Biome.RustBelt, MapSize.Small, 2, 4, null, OceanEdges.North | OceanEdges.West);
        var (map, meta) = MapGenerator.GenerateWithMeta(p);
        string path = Path.Combine(Path.GetTempPath(), "m7_13_" + Guid.NewGuid().ToString("N") + ".map.toml");
        try
        {
            MapFile.Save(map, meta, path);
            var (map2, meta2) = MapFile.LoadWithMeta(path);
            Assert.Equal(MapHasher.Compute(map), MapHasher.Compute(map2));
            Assert.Equal(meta.ConvoyEdge, meta2.ConvoyEdge);
            Assert.Equal(meta.Landing, meta2.Landing);
            Assert.Equal(OceanEdges.North | OceanEdges.West, meta2.Generator!.OceanEdges);
        }
        finally { File.Delete(path); }
    }

    // ---- validator ----------------------------------------------------------------------------

    /// <summary>A 40x40 map: sea in the top 6 rows, a Coast band in rows 6-7, two starts well inland.</summary>
    private static MapData Naval(Action<LandType[]>? edit = null)
    {
        const int n = 40;
        var land = new LandType[n * n];
        for (int y = 0; y < 6; y++) for (int x = 0; x < n; x++) land[y * n + x] = y < 4 ? LandType.Water : LandType.Shallow;
        for (int y = 6; y < 8; y++) for (int x = 0; x < n; x++) land[y * n + x] = LandType.Coast;
        edit?.Invoke(land);
        return new MapData("naval", "rust_belt", n, n, new ushort[n * n], land, new byte[n * n], new OverlayKind[n * n], new byte[n * n],
            new[] { new CellCoord(8, 25), new CellCoord(30, 25) });
    }

    private static MapMeta Meta(MapData map, CellCoord[] convoy, CellCoord[]? landing = null) =>
        new("", MapSymmetry.None, 2, null, Array.Empty<MapFeature>()) { ConvoyEdge = convoy, Landing = landing ?? Array.Empty<CellCoord>() };

    private static string[] Codes(MapData m, MapMeta meta) => MapValidator.Validate(m, meta, null).Select(i => i.Code).ToArray();

    [Fact]
    public void Validator_GoodNavalMap_NoNavalIssues()
    {
        MapData m = Naval();
        Assert.DoesNotContain(Codes(m, Meta(m, new[] { new CellCoord(5, 0) }, new[] { new CellCoord(5, 6) })),
            c => c is "convoy_lake" or "convoy_no_coast" or "landing_unreachable");
    }

    [Fact]
    public void Validator_IsolatedLakeAsConvoyEdge_IsAnError()
    {
        MapData m = Naval(l => { for (int y = 20; y < 23; y++) for (int x = 20; x < 23; x++) l[y * 40 + x] = LandType.Water; });
        Assert.Contains("convoy_lake", Codes(m, Meta(m, new[] { new CellCoord(21, 21) })));
        Assert.Contains("convoy_lake", Codes(m, Meta(m, new[] { new CellCoord(10, 15) })));   // plain land
    }

    [Fact]
    public void Validator_SeaWithoutCoast_IsAnError()
    {
        MapData m = Naval(l => { for (int i = 0; i < l.Length; i++) if (l[i] == LandType.Coast) l[i] = LandType.Rock; });
        Assert.Contains("convoy_no_coast", Codes(m, Meta(m, new[] { new CellCoord(5, 0) })));
    }

    [Fact]
    public void Validator_UnreachableLanding_IsAnError()
    {
        // A landing marker on plain land, and a coast the start cannot walk to (a rock wall cuts the map below the coast).
        MapData m = Naval();
        Assert.Contains("landing_unreachable", Codes(m, Meta(m, new[] { new CellCoord(5, 0) }, new[] { new CellCoord(5, 20) })));
        MapData walled = Naval(l => { for (int x = 0; x < 40; x++) l[12 * 40 + x] = LandType.Rock; });
        Assert.Contains("landing_unreachable", Codes(walled, Meta(walled, new[] { new CellCoord(5, 0) })));
    }

    [Fact]
    public void Validator_MapsWithoutNavalMarkers_GetNoNavalIssues()
    {
        MapData m = Naval();
        Assert.DoesNotContain(Codes(m, MapMeta.DefaultFor(m)), c => c is "convoy_lake" or "convoy_no_coast" or "landing_unreachable");
    }
}
