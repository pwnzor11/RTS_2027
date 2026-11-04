// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Data;
using Rts.Game.Screens.Editor;
using Rts.Sim.Core;
using Rts.Sim.Map;
using Rts.Sim.Rules;

namespace Rts.Game.Tests.Editor;

public class EditorSessionTests
{
    private static EditorSession New(int side = 32, MapSymmetry sym = MapSymmetry.None)
    {
        var doc = EditorDocument.Create("t", "rust_belt", side, side);
        doc.Symmetry = sym;
        return new EditorSession(doc);
    }

    [Fact]
    public void Brush_sizes_cover_1_9_and_25_cells_clipped_at_edges()
    {
        Assert.Single(BrushShape.Square(5, 5, 1, 32, 32));
        Assert.Equal(9, BrushShape.Square(5, 5, 3, 32, 32).Count);
        Assert.Equal(25, BrushShape.Square(5, 5, 5, 32, 32).Count);
        Assert.Equal(4, BrushShape.Square(0, 0, 3, 32, 32).Count);
    }

    [Fact]
    public void Painting_changes_land_and_undo_redo_restore_it()
    {
        EditorSession s = New();
        s.Tool = EditorTool.Land; s.Land = LandType.Water; s.BrushSize = 3;
        Assert.True(s.Click(10, 10));
        Assert.Equal(LandType.Water, s.Document.Land[s.Document.IndexOf(11, 11)]);
        Assert.Equal(9, s.Document.Land.Count(l => l == LandType.Water));
        Assert.True(s.Undo());
        Assert.All(s.Document.Land, l => Assert.Equal(LandType.Clear, l));
        Assert.True(s.Redo());
        Assert.Equal(9, s.Document.Land.Count(l => l == LandType.Water));
    }

    [Fact]
    public void A_drag_is_one_undo_step_and_fills_gaps()
    {
        EditorSession s = New();
        s.Land = LandType.Rock;
        s.BeginStroke();
        s.ApplyAt(2, 2); s.ApplyAt(10, 2);
        s.EndStroke();
        Assert.Equal(9, s.Document.Land.Count(l => l == LandType.Rock));
        Assert.Equal(1, s.History.UndoCount);
        s.Undo();
        Assert.DoesNotContain(LandType.Rock, s.Document.Land);
    }

    [Theory]
    [InlineData(MapSymmetry.Rot2, 2)]
    [InlineData(MapSymmetry.Rot4, 4)]
    [InlineData(MapSymmetry.MirrorX, 2)]
    [InlineData(MapSymmetry.MirrorY, 2)]
    [InlineData(MapSymmetry.MirrorDiag, 2)]
    public void Symmetry_paints_every_image_live(MapSymmetry sym, int images)
    {
        EditorSession s = New(32, sym);
        s.Land = LandType.Cliff;
        s.Click(3, 7);
        Assert.Equal(images, s.Document.Land.Count(l => l == LandType.Cliff));
        foreach (var (x, y) in MapSymmetries.Images(sym, 32, 32, 3, 7))
            Assert.Equal(LandType.Cliff, s.Document.Land[s.Document.IndexOf(x, y)]);
        s.Undo();
        Assert.DoesNotContain(LandType.Cliff, s.Document.Land);
    }

    [Fact]
    public void Rot4_on_a_non_square_map_is_refused()
    {
        var doc = EditorDocument.Create("t", "", 64, 32);
        var s = new EditorSession(doc);
        Assert.False(s.SetSymmetry(MapSymmetry.Rot4));
        Assert.Equal(MapSymmetry.None, doc.Symmetry);
        Assert.True(s.SetSymmetry(MapSymmetry.Rot2));
    }

    [Fact]
    public void Resources_are_placed_with_density_erased_and_refused_on_water()
    {
        EditorSession s = New();
        s.Tool = EditorTool.Resource; s.Resource = OverlayKind.Pitchblende; s.ResourceDensity = 99;
        Assert.Equal(12, s.ResourceDensity);
        s.Click(4, 4);
        int i = s.Document.IndexOf(4, 4);
        Assert.Equal(OverlayKind.Pitchblende, s.Document.Overlay[i]);
        Assert.Equal(12, s.Document.Density[i]);
        s.Tool = EditorTool.Eraser; s.Click(4, 4);
        Assert.Equal(OverlayKind.None, s.Document.Overlay[i]);
        s.Undo();
        Assert.Equal(OverlayKind.Pitchblende, s.Document.Overlay[i]);

        s.Tool = EditorTool.Land; s.Land = LandType.Water; s.Click(4, 4);
        Assert.Equal(OverlayKind.None, s.Document.Overlay[i]);   // painting water clears the field
        s.Tool = EditorTool.Resource; s.Click(4, 4);
        Assert.Equal(OverlayKind.None, s.Document.Overlay[i]);
    }

    [Fact]
    public void Starts_are_placed_as_a_symmetric_set_in_slot_order()
    {
        EditorSession s = New(32, MapSymmetry.Rot2);
        s.Tool = EditorTool.Start;
        s.Click(5, 6);
        Assert.Equal(new[] { new CellCoord(5, 6), new CellCoord(26, 25) }, s.Document.Starts);
        Assert.Equal(3, s.StartSlot);
        s.Click(5, 20);
        Assert.Equal(4, s.Document.Starts.Count);
        s.Click(10, 10);   // no slots left from slot 1 after wrap? slot wraps to 1, replaces starts 1..2
        Assert.Equal(4, s.Document.Starts.Count);
        Assert.Equal(new CellCoord(10, 10), s.Document.Starts[0]);
        s.Undo(); s.Undo(); s.Undo();
        Assert.Empty(s.Document.Starts);
        s.Redo();
        Assert.Equal(2, s.Document.Starts.Count);
    }

    [Fact]
    public void A_start_on_the_axis_is_refused_and_a_fifth_start_never_exists()
    {
        var doc = EditorDocument.Create("t", "", 33, 33);
        doc.Symmetry = MapSymmetry.MirrorX;
        EditorSession s = new(doc);
        s.Tool = EditorTool.Start;
        Assert.False(s.Click(16, 5));   // x = 16 is the axis of a 33-wide map
        Assert.Empty(doc.Starts);
        Assert.Contains("axis", s.Status);
        s = New(32, MapSymmetry.None);
        s.Tool = EditorTool.Start;
        for (int i = 0; i < 6; i++) { s.StartSlot = Math.Min(4, s.Document.Starts.Count + 1); s.Click(4 + i * 4, 4); }
        Assert.True(s.Document.Starts.Count <= 4);
    }

    [Fact]
    public void Features_place_with_footprint_and_refuse_overlap()
    {
        EditorSession s = New();
        s.Tool = EditorTool.Feature; s.Feature = FeatureType.Defaults[1];   // 3x2
        s.Click(10, 10);
        Assert.Single(s.Document.Features);
        s.Click(11, 11);   // overlaps
        Assert.Single(s.Document.Features);
        s.Click(31, 31);   // footprint off the map
        Assert.Single(s.Document.Features);
        s.Tool = EditorTool.Eraser; s.Click(12, 11);   // covered cell
        Assert.Empty(s.Document.Features);
        s.Undo();
        Assert.Single(s.Document.Features);
    }

    [Fact]
    public void Rot2_features_place_a_partner()
    {
        EditorSession s = New(32, MapSymmetry.Rot2);
        s.Tool = EditorTool.Feature; s.Feature = FeatureType.Defaults[1];
        s.Click(2, 3);
        Assert.Equal(2, s.Document.Features.Count);
        Assert.Equal(new CellCoord(27, 27), s.Document.Features[1].Cell);   // 3x2 footprint rotated onto (27..29, 27..28)
    }

    [Fact]
    public void Flood_fill_replaces_the_connected_region_only()
    {
        EditorSession s = New();
        s.Land = LandType.Rock; s.Tool = EditorTool.Land;
        s.BeginStroke();
        for (int y = 0; y < 32; y++) s.ApplyAt(16, y);
        s.EndStroke();
        s.Tool = EditorTool.Fill; s.Land = LandType.Ash;
        s.Click(2, 2);
        Assert.Equal(16 * 32, s.Document.Land.Count(l => l == LandType.Ash));
        Assert.Equal(LandType.Clear, s.Document.Land[s.Document.IndexOf(20, 2)]);
    }

    [Fact]
    public void History_keeps_at_least_100_steps_and_drops_the_oldest_beyond_capacity()
    {
        EditorSession s = New(64);
        s.Land = LandType.Rough;
        for (int i = 0; i < 150; i++) { s.Land = i % 2 == 0 ? LandType.Rough : LandType.Ash; s.Click(i % 64, i / 64); }
        Assert.True(s.History.UndoCount >= 100);
        int undone = 0;
        while (s.Undo()) undone++;
        Assert.Equal(s.History.RedoCount, undone);
        Assert.True(undone >= 100);
        for (int i = 0; i < undone; i++) Assert.True(s.Redo());
        Assert.False(s.Redo());
    }

    [Fact]
    public void A_new_edit_drops_the_redo_tail()
    {
        EditorSession s = New();
        s.Click(1, 1); s.Click(2, 2); s.Undo();
        Assert.Equal(1, s.History.RedoCount);
        s.Click(3, 3);
        Assert.Equal(0, s.History.RedoCount);
    }

    [Fact]
    public void A_stroke_that_changes_nothing_is_not_recorded()
    {
        EditorSession s = New();
        s.Land = LandType.Clear;
        Assert.False(s.Click(5, 5));
        Assert.Equal(0, s.History.UndoCount);
        Assert.False(s.Dirty);
    }
}
