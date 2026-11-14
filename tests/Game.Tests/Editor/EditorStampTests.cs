// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Data;
using Rts.Data.Stamps;
using Rts.Game.Screens.Editor;
using Rts.Sim.Core;
using Rts.Sim.Map;
using Rts.Sim.Rules;

namespace Rts.Game.Tests.Editor;

public class EditorStampTests
{
    private static EditorSession New(MapSymmetry sym = MapSymmetry.None, int side = 64)
    {
        var doc = EditorDocument.Create("stamp", "rust_belt", side, side);
        doc.Symmetry = sym;
        var s = new EditorSession(doc) { Tool = EditorTool.Stamp };
        Assert.NotEmpty(s.AllStamps);
        return s;
    }

    private static string Snapshot(EditorDocument d) =>
        string.Join(",", d.Land.Select(l => (int)l)) + "|" + string.Join(",", d.HeightLevel) + "|" + string.Join(",", d.Overlay.Select(o => (int)o))
        + "|" + string.Join(",", d.Density) + "|" + string.Join(",", d.Decor);

    [Fact]
    public void Stamp_applies_as_one_step_and_undo_redo_restore_every_cell()
    {
        EditorSession s = New();
        s.Stamp = s.StampsForBiome().First();
        string before = Snapshot(s.Document);
        Assert.True(s.Click(20, 20));
        string after = Snapshot(s.Document);
        Assert.NotEqual(before, after);
        Assert.Equal(1, s.History.UndoCount);
        Assert.True(s.Undo());
        Assert.Equal(before, Snapshot(s.Document));
        Assert.True(s.Redo());
        Assert.Equal(after, Snapshot(s.Document));
    }

    [Fact]
    public void Rotation_swaps_the_footprint_and_mirror_keeps_it()
    {
        EditorSession s = New();
        Stamp t = s.AllStamps.First(x => x.Width != x.Height);
        s.Stamp = t;
        Assert.Equal((t.Width, t.Height), s.StampFootprint);
        s.StampRotation = 1;
        Assert.Equal((t.Height, t.Width), s.StampFootprint);
        s.StampRotation = 5;
        Assert.Equal(1, s.StampRotation);
        s.StampMirror = true;
        Assert.Equal((t.Height, t.Width), s.StampFootprint);
        // changed cells stay inside the rotated footprint
        string before = Snapshot(s.Document);
        s.Click(10, 10);
        for (int i = 0; i < s.Document.Land.Length; i++)
        {
            int x = i % 64, y = i / 64;
            if (x >= 10 && x < 10 + t.Height && y >= 10 && y < 10 + t.Width) continue;
            Assert.Equal(LandType.Clear, s.Document.Land[i]);
        }
        Assert.NotEqual(before, Snapshot(s.Document));
    }

    [Fact]
    public void Cells_near_a_start_are_refused_and_reported()
    {
        EditorSession s = New();
        s.Tool = EditorTool.Start; s.Click(12, 12);
        s.Tool = EditorTool.Stamp;
        Stamp t = s.AllStamps.OrderByDescending(x => x.Width * x.Height).First();
        s.Stamp = t;
        s.Click(10, 10);
        Assert.Contains("refused", s.Status);
        for (int y = 9; y <= 15; y++)
            for (int x = 9; x <= 15; x++)
            {
                int i = s.Document.IndexOf(x, y);
                Assert.Equal(LandType.Clear, s.Document.Land[i]);
                Assert.Equal(OverlayKind.None, s.Document.Overlay[i]);
            }
    }

    [Fact]
    public void Symmetric_images_are_stamped_and_can_be_turned_off()
    {
        EditorSession s = New(MapSymmetry.Rot2);
        s.Stamp = s.StampsForBiome().First();
        s.Click(8, 8);
        EditorDocument d = s.Document;
        int changed = 0;
        for (int i = 0; i < d.Land.Length; i++)
        {
            bool differs = d.Land[i] != LandType.Clear || d.Overlay[i] != OverlayKind.None || d.HeightLevel[i] != 0 || d.Decor[i] != 0;
            if (!differs) continue;
            changed++;
            int mx = 63 - i % 64, my = 63 - i / 64, j = my * 64 + mx;
            Assert.Equal(d.Land[i], d.Land[j]); Assert.Equal(d.Overlay[i], d.Overlay[j]); Assert.Equal(d.Decor[i], d.Decor[j]);
        }
        Assert.True(changed > 0);
        Assert.True(s.Undo());
        s.StampSymmetric = false;
        s.Click(8, 8);
        Assert.Equal(LandType.Clear, d.Land[d.IndexOf(55, 55)]);
        Assert.Equal(OverlayKind.None, d.Overlay[d.IndexOf(55, 55)]);
    }

    [Fact]
    public void Picker_lists_only_stamps_for_the_biome()
    {
        EditorSession s = New();
        Assert.All(s.StampsForBiome(), t => Assert.True(t.Biomes.Count == 0 || t.Biomes.Contains("rust_belt")));
    }
}
