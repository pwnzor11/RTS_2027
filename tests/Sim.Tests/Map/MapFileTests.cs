using Rts.Data;
using Rts.Sim.Core;
using Rts.Sim.Map;
using Rts.Sim.Rules;

namespace Rts.Sim.Tests.Map;

public class MapFileTests
{
    private static string Temp(string name)
    {
        string dir = Path.Combine(Path.GetTempPath(), "rts_map_tests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        return Path.Combine(dir, name);
    }

    [Fact]
    public void SaveLoadRoundTripKeepsHash()
    {
        MapData map = MapGenerator.Generate(32, 5, "round \"trip\"");
        string path = Temp("a.map.toml");
        MapFile.Save(map, path);
        MapData back = MapFile.Load(path);
        Assert.Equal(MapHasher.Compute(map), MapHasher.Compute(back));
        Assert.Equal(map.Theater, back.Theater);
        Assert.Equal(map.StartPositions, back.StartPositions);
    }

    [Fact]
    public void LoadRejectsTruncatedLayer()
    {
        string path = Temp("bad.map.toml");
        MapFile.Save(MapGenerator.Generate(32, 2), path);
        string text = File.ReadAllText(path);
        int at = text.IndexOf("land = \"", StringComparison.Ordinal) + 8;
        File.WriteAllText(path, text.Remove(at, 40)); // drops 30 bytes of the land layer
        var ex = Assert.Throws<MapFileException>(() => MapFile.Load(path));
        Assert.Contains("layers.land", ex.Message);
    }

    [Fact]
    public void LoadRejectsOutOfRangeEnum()
    {
        string path = Temp("enum.map.toml");
        var d = MapData.CreateFlat(4, 4);
        var land = new LandType[16];
        land[3] = (LandType)99;
        MapFile.Save(new MapData("e", "rust_belt", 4, 4, new ushort[16], land, new byte[16], new OverlayKind[16], new byte[16], d.StartPositions), path);
        Assert.Throws<MapFileException>(() => MapFile.Load(path));
    }

    [Fact]
    public void LoadRejectsMissingFileAndBadToml()
    {
        Assert.Throws<MapFileException>(() => MapFile.Load(Temp("nope.map.toml")));
        string path = Temp("junk.map.toml");
        File.WriteAllText(path, "[map\nbroken");
        Assert.Throws<MapFileException>(() => MapFile.Load(path));
    }

    [Fact]
    public void CommittedMapsLoad()
    {
        string dir = AppContext.BaseDirectory;
        while (dir != null && !File.Exists(Path.Combine(dir, "RTS_2027.slnx"))) dir = Path.GetDirectoryName(dir)!;
        Assert.NotNull(dir);
        Assert.Equal(32, MapFile.Load(Path.Combine(dir, "data", "maps", "test_32.map.toml")).Width);
        Assert.Equal(64, MapFile.Load(Path.Combine(dir, "data", "maps", "test_64.map.toml")).Width);
    }
}
