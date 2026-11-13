// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Data;
using Rts.Game.Screens.Editor;
using Rts.Sim.Core;
using Rts.Sim.Map;
using Rts.Sim.Rules;

namespace Rts.Game.Tests.Editor;

public class EditorPropTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "rts_editor_props_" + Guid.NewGuid().ToString("N"));

    public void Dispose() { try { Directory.Delete(_dir, true); } catch (IOException) { } }

    private static EditorSession New()
    {
        var doc = EditorDocument.Create("props", "rust_belt", 48, 48);
        return new EditorSession(doc);
    }

    [Fact]
    public void Props_paint_undo_and_redo()
    {
        EditorSession s = New();
        s.Tool = EditorTool.Props; s.Prop = OverlayKind.Wreck; s.PropVariant = 2; s.BrushSize = 3;
        Assert.True(s.Click(10, 10));
        int i = s.Document.IndexOf(11, 11);
        Assert.Equal(OverlayKind.Wreck, s.Document.Overlay[i]);
        Assert.Equal(2, s.Document.Density[i]);
        Assert.Equal(9, s.Document.Overlay.Count(o => o == OverlayKind.Wreck));
        Assert.True(s.Undo());
        Assert.All(s.Document.Overlay, o => Assert.Equal(OverlayKind.None, o));
        Assert.True(s.Redo());
        Assert.Equal(9, s.Document.Overlay.Count(o => o == OverlayKind.Wreck));
    }

    [Fact]
    public void Decals_paint_undo_redo_and_zero_erases()
    {
        EditorSession s = New();
        s.Tool = EditorTool.Decal; s.DecalKind = 3; s.BrushSize = 3;
        Assert.True(s.Click(20, 20));
        Assert.Equal(9, s.Document.Decor.Count(d => d == 3));
        Assert.True(s.Undo());
        Assert.All(s.Document.Decor, d => Assert.Equal(0, d));
        Assert.True(s.Redo());
        s.DecalKind = 0; s.BrushSize = 1;
        Assert.True(s.Click(20, 20));
        Assert.Equal(8, s.Document.Decor.Count(d => d == 3));
        Assert.True(s.Undo());
        Assert.Equal(9, s.Document.Decor.Count(d => d == 3));
    }

    [Fact]
    public void Eraser_clears_props_and_decals_in_one_undoable_step()
    {
        EditorSession s = New();
        s.Tool = EditorTool.Props; s.Click(5, 5);
        s.Tool = EditorTool.Decal; s.DecalKind = 2; s.Click(5, 5);
        s.Tool = EditorTool.Eraser;
        Assert.True(s.Click(5, 5));
        int i = s.Document.IndexOf(5, 5);
        Assert.Equal(OverlayKind.None, s.Document.Overlay[i]);
        Assert.Equal(0, s.Document.Decor[i]);
        Assert.True(s.Undo());
        Assert.Equal(OverlayKind.Tree, s.Document.Overlay[i]);
        Assert.Equal(2, s.Document.Decor[i]);
    }

    [Fact]
    public void Props_are_refused_on_water_cliff_starts_and_resources()
    {
        EditorSession s = New();
        s.Tool = EditorTool.Land; s.Land = LandType.Water; s.Click(10, 10);
        s.Land = LandType.Cliff; s.Click(12, 10);
        s.Tool = EditorTool.Start; s.Click(30, 30);
        s.Tool = EditorTool.Resource; s.Resource = OverlayKind.Ashfall; s.Click(20, 20);
        s.Tool = EditorTool.Props;
        foreach (var (x, y) in new[] { (10, 10), (12, 10), (30, 30), (32, 31), (20, 20), (21, 21) })
        {
            Assert.False(s.Click(x, y), $"({x},{y})");
            Assert.False(OverlayKinds.IsProp(s.Document.Overlay[s.Document.IndexOf(x, y)]));
        }
        Assert.True(s.Click(35, 35));
    }

    [Fact]
    public void Painting_water_over_a_prop_removes_it()
    {
        EditorSession s = New();
        s.Tool = EditorTool.Props; s.Click(8, 8);
        s.Tool = EditorTool.Land; s.Land = LandType.Water; s.Click(8, 8);
        Assert.Equal(OverlayKind.None, s.Document.Overlay[s.Document.IndexOf(8, 8)]);
        Assert.True(s.Undo());
        Assert.Equal(OverlayKind.Tree, s.Document.Overlay[s.Document.IndexOf(8, 8)]);
    }

    [Fact]
    public void Save_and_load_keeps_decor_and_props()
    {
        EditorSession s = New();
        s.Tool = EditorTool.Props; s.Prop = OverlayKind.Pylon; s.Click(10, 10);
        s.Tool = EditorTool.Decal; s.DecalKind = 4; s.Click(15, 15);
        string path = MapFolders.PathFor(_dir, "props");
        MapFile.Save(s.Document.ToMapData(), s.Document.ToMeta(), path);
        var (map, meta) = MapFile.LoadWithMeta(path);
        EditorDocument again = EditorDocument.From(map, meta);
        Assert.Equal(OverlayKind.Pylon, again.Overlay[again.IndexOf(10, 10)]);
        Assert.Equal(4, again.Decor[again.IndexOf(15, 15)]);
        Assert.Equal(1, again.Decor.Count(d => d != 0));
    }

    [Fact]
    public void Decals_leave_the_map_hash_alone_but_props_change_it()
    {
        EditorSession s = New();
        ulong plain = MapHasher.Compute(s.Document.ToMapData());
        s.Tool = EditorTool.Decal; s.DecalKind = 2; s.Click(15, 15);
        Assert.Equal(plain, MapHasher.Compute(s.Document.ToMapData()));
        s.Tool = EditorTool.Props; s.Click(10, 10);
        Assert.NotEqual(plain, MapHasher.Compute(s.Document.ToMapData()));
    }

    [Fact]
    public void Validation_reports_props_that_cut_the_path_between_starts()
    {
        EditorSession s = New();
        s.Tool = EditorTool.Start; s.Click(8, 8); s.StartSlot = 2; s.Click(40, 40);
        // a full wall of props across the map between the starts
        s.Tool = EditorTool.Props;
        for (int x = 0; x < 48; x++) s.Click(x, 24);
        ValidationReport r = ValidationReport.Run(s.Document);
        Assert.Contains(r.Rows, row => row.Code == "prop_blocks_path");
        Assert.False(r.CanSave);
    }
}
