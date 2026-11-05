// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Data;
using Rts.Sim.Core;
using Rts.Sim.Map;
using Rts.Sim.Rules;

namespace Rts.Sim.Tests.M7;

/// <summary>M7-02 map file format 3: Coast cells and the <c>convoy_edge</c> and <c>landing</c> markers; versions 1 and 2 still load.</summary>
public sealed class MapFormat3Tests
{
    private static string Temp(string name)
    {
        string dir = Path.Combine(Path.GetTempPath(), "rts_map3_tests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        return Path.Combine(dir, name);
    }

    private static MapData CoastMap()
    {
        MapData flat = MapData.CreateFlat(8, 8, "coast");
        var land = new LandType[64];
        for (int y = 0; y < 8; y++)
        {
            land[y * 8 + 6] = LandType.Shallow;
            land[y * 8 + 7] = LandType.Water;
            land[y * 8 + 5] = LandType.Coast;
        }
        return new MapData("coast", "glass_coast", 8, 8, new ushort[64], land, new byte[64], new OverlayKind[64], new byte[64], flat.StartPositions);
    }

    private static MapMeta Meta(MapData map) =>
        new("glass_coast", MapSymmetry.None, 2, null, Array.Empty<MapFeature>())
        {
            ConvoyEdge = new[] { new CellCoord(7, 0), new CellCoord(7, 7) },
            Landing = new[] { new CellCoord(5, 3) },
        };

    [Fact]
    public void Version_IsThree_AndOlderStillRead()
    {
        Assert.Equal(3, MapFile.FormatVersion);
        Assert.Equal(1, MapFile.OldestReadableVersion);
    }

    [Fact]
    public void Format3_RoundTrips_CoastCellsAndMarkers()
    {
        MapData map = CoastMap();
        string path = Temp("c.map.toml");
        MapFile.Save(map, Meta(map), path);
        Assert.Contains("format_version = 3", File.ReadAllText(path));
        var (back, meta) = MapFile.LoadWithMeta(path);
        Assert.Equal(MapHasher.Compute(map), MapHasher.Compute(back));
        Assert.Equal(LandType.Coast, back.Land[3 * 8 + 5]);
        Assert.Equal(new[] { new CellCoord(7, 0), new CellCoord(7, 7) }, meta.ConvoyEdge);
        Assert.Equal(new[] { new CellCoord(5, 3) }, meta.Landing);
        Assert.Equal("glass_coast", meta.Biome);
    }

    [Fact]
    public void Format3_WithoutMarkers_HasNoMarkerKeys()
    {
        MapData map = CoastMap();
        string path = Temp("n.map.toml");
        MapFile.Save(map, MapMeta.DefaultFor(map), path);
        string text = File.ReadAllText(path);
        Assert.DoesNotContain("convoy_edge", text);
        Assert.DoesNotContain("landing", text);
        var (_, meta) = MapFile.LoadWithMeta(path);
        Assert.Empty(meta.ConvoyEdge);
        Assert.Empty(meta.Landing);
    }

    [Fact]
    public void Format2File_Loads_AndIgnoresMarkers()
    {
        MapData map = MapGenerator.Generate(32, 5, "two");
        string path = Temp("two.map.toml");
        MapFile.Save(map, new MapMeta("rust_belt", MapSymmetry.Rot2, 2, null, Array.Empty<MapFeature>()), path);
        File.WriteAllText(path, File.ReadAllText(path).Replace("format_version = 3", "format_version = 2"));
        var (back, meta) = MapFile.LoadWithMeta(path);
        Assert.Equal(MapHasher.Compute(map), MapHasher.Compute(back));
        Assert.Equal((MapSymmetry.Rot2, 2), (meta.Symmetry, meta.MaxPlayers));
        Assert.Empty(meta.ConvoyEdge);
        Assert.Empty(meta.Landing);
        // a version-2 file carrying the new keys is read as version 2: the keys are ignored
        string withKeys = File.ReadAllText(path).Replace("max_players = 2\n", "max_players = 2\nlanding = [[1, 1]]\n").Replace("max_players = 2\r\n", "max_players = 2\r\nlanding = [[1, 1]]\r\n");
        File.WriteAllText(path, withKeys);
        Assert.Empty(MapFile.LoadWithMeta(path).Meta.Landing);
    }

    [Fact]
    public void Format1File_StillLoads()
    {
        MapData map = MapGenerator.Generate(32, 7, "one");
        string path = Temp("one.map.toml");
        MapFile.Save(map, path);
        File.WriteAllText(path, File.ReadAllText(path).Replace("format_version = 3", "format_version = 1"));
        var (back, meta) = MapFile.LoadWithMeta(path);
        Assert.Equal(MapHasher.Compute(map), MapHasher.Compute(back));
        Assert.Equal(MapMeta.DefaultFor(back).MaxPlayers, meta.MaxPlayers);
        Assert.Empty(meta.Landing);
    }

    [Fact]
    public void FutureVersion_IsRejected()
    {
        MapData map = CoastMap();
        string path = Temp("f.map.toml");
        MapFile.Save(map, path);
        File.WriteAllText(path, File.ReadAllText(path).Replace("format_version = 3", "format_version = 4"));
        Assert.Throws<MapFileException>(() => MapFile.Load(path));
    }

    [Theory]
    [InlineData("landing = [[99, 1]]")]
    [InlineData("landing = [[1]]")]
    [InlineData("landing = 3")]
    [InlineData("convoy_edge = [[0, -1]]")]
    [InlineData("convoy_edge = [[\"a\", 1]]")]
    public void BadMarkers_AreRejected(string line)
    {
        MapData map = CoastMap();
        string path = Temp("b.map.toml");
        MapFile.Save(map, MapMeta.DefaultFor(map), path);
        string text = File.ReadAllText(path);
        int at = text.IndexOf("max_players", StringComparison.Ordinal);
        File.WriteAllText(path, text.Insert(at, line + "\n"));
        Assert.Throws<MapFileException>(() => MapFile.Load(path));
    }

    [Fact]
    public void LandTypeNine_IsStillInvalid()
    {
        var d = MapData.CreateFlat(4, 4);
        var land = new LandType[16];
        land[3] = (LandType)RuleCounts.LandTypes;
        string path = Temp("l.map.toml");
        MapFile.Save(new MapData("e", "rust_belt", 4, 4, new ushort[16], land, new byte[16], new OverlayKind[16], new byte[16], d.StartPositions), path);
        Assert.Throws<MapFileException>(() => MapFile.Load(path));
    }
}
