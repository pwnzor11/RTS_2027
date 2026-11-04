// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Data;
using Rts.Game.Screens.Editor;
using Rts.Sim.Core;
using Rts.Sim.Map;
using Rts.Sim.Rules;

namespace Rts.Game.Tests.Editor;

public class EditorFileTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "rts_editor_" + Guid.NewGuid().ToString("N"));

    public void Dispose() { try { Directory.Delete(_dir, true); } catch (IOException) { } }

    private static EditorSession Painted()
    {
        var s = new EditorSession(NewMapScreen.Build(2, 64));
        s.SetSymmetry(MapSymmetry.Rot2);
        s.BrushSize = 3; s.Land = LandType.Water; s.Click(12, 12);
        s.Tool = EditorTool.Resource; s.Resource = OverlayKind.Ashfall; s.ResourceDensity = 5; s.Click(20, 8);
        s.Tool = EditorTool.Start; s.Click(8, 8);
        s.Tool = EditorTool.Feature; s.Feature = FeatureType.Defaults[0]; s.Click(30, 30);
        s.Document.Name = "Round Trip";
        return s;
    }

    [Fact]
    public void Save_then_load_gives_the_same_map_hash_and_metadata()
    {
        EditorSession s = Painted();
        string path = MapFolders.PathFor(_dir, s.Document.Name);
        MapData before = s.Document.ToMapData();
        MapFile.Save(before, s.Document.ToMeta(), path);
        var (map, meta) = MapFile.LoadWithMeta(path);
        Assert.Equal(MapHasher.Compute(before), MapHasher.Compute(map));
        Assert.Equal(MapSymmetry.Rot2, meta.Symmetry);
        Assert.Equal("mountains", meta.Biome);
        Assert.Equal(s.Document.Features, meta.Features);
        EditorDocument again = EditorDocument.From(map, meta);
        Assert.Equal(MapHasher.Compute(before), MapHasher.Compute(again.ToMapData()));
        Assert.Equal(s.Document.Starts, again.Starts);
    }

    [Fact]
    public void Folder_listing_names_user_maps_first_and_ignores_other_files()
    {
        string user = Path.Combine(_dir, "user"), data = Path.Combine(_dir, "data");
        Directory.CreateDirectory(user); Directory.CreateDirectory(data);
        File.WriteAllText(Path.Combine(user, "b.map.toml"), ""); File.WriteAllText(Path.Combine(user, "a.map.toml"), "");
        File.WriteAllText(Path.Combine(user, "notes.txt"), ""); File.WriteAllText(Path.Combine(data, "d.map.toml"), "");
        var list = MapFolders.List(user, data);
        Assert.Equal(new[] { "a", "b", "d" }, list.Select(e => e.Name));
        Assert.Equal(new[] { true, true, false }, list.Select(e => e.IsUser));
        Assert.Empty(MapFolders.List(Path.Combine(_dir, "none"), Path.Combine(_dir, "none2")));
    }

    [Theory]
    [InlineData("My Map!", "my_map")]
    [InlineData("../evil", "evil")]
    [InlineData("???", "map")]
    public void File_stems_are_safe(string name, string stem) => Assert.Equal(stem, MapFolders.FileStem(name));

    [Fact]
    public void Document_size_is_checked_and_sides_follow_the_generator_sizes()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => EditorDocument.Create("x", "", 31, 64));
        Assert.Throws<ArgumentOutOfRangeException>(() => EditorDocument.Create("x", "", 64, 129));
        Assert.Equal(96, EditorDocument.SideFor(MapSize.Medium));
    }

    [Fact]
    public void Map_data_is_a_copy_not_a_live_view()
    {
        var d = EditorDocument.Create("x", "", 32, 32);
        MapData m = d.ToMapData();
        d.Land[0] = LandType.Rock;
        Assert.Equal(LandType.Clear, m.Land[0]);
    }
}
