// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Microsoft.Xna.Framework;
using Rts.Game.Screens;
using Rts.Game.Ui;
using Rts.Game.Ui.Objectives;
using Rts.Game.Ui.Sidebar;
using Rts.Sim.Core;
using Rts.Sim.View;
using Xunit;

namespace Rts.Game.Tests.Ui2;

public sealed class PolishTests
{
    [Fact]
    public void Heading_loses_its_hashes_and_is_flagged()
    {
        MdLine l = MarkdownLines.Parse("# Basic Training");
        Assert.True(l.Heading);
        Assert.Equal("Basic Training", l.Plain);
        Assert.False(MarkdownLines.Parse("#hashtag").Heading);
    }

    [Fact]
    public void Bold_runs_are_split_and_markers_removed()
    {
        MdLine l = MarkdownLines.Parse("**Authority:** the Board *may* act");
        Assert.Equal("Authority: the Board may act", l.Plain);
        Assert.Equal(new MdSpan("Authority:", true), l.Spans[0]);
        Assert.False(l.Spans[1].Bold);
        Assert.DoesNotContain("*", l.Plain);
    }

    [Fact]
    public void Wrap_keeps_weights_and_width()
    {
        MdLine l = MarkdownLines.Parse("**one two three** four five six");
        var lines = MarkdownLines.Wrap(l, 12);
        Assert.All(lines, x => Assert.True(x.Length <= 12));
        Assert.Equal("one two three four five six", string.Join(" ", lines.Select(x => x.Plain)));
        Assert.True(lines[0].Spans[0].Bold);
        Assert.Single(MarkdownLines.Wrap(MdLine.Blank, 10));
    }

    [Fact]
    public void Label_wraps_to_two_lines_of_at_most_40()
    {
        var one = GuidanceMath.WrapLabel("Destroy the enemy radar", 40, 2);
        Assert.Equal(new[] { "Destroy the enemy radar" }, one);
        var two = GuidanceMath.WrapLabel("Escort the supply convoy to the northern ridge outpost", 40, 2);
        Assert.Equal(2, two.Count);
        Assert.All(two, x => Assert.True(x.Length <= 40));
        Assert.DoesNotContain("...", string.Concat(two));
        var cut = GuidanceMath.WrapLabel(new string('a', 30) + " " + new string('b', 30) + " " + new string('c', 30), 40, 2);
        Assert.Equal(2, cut.Count);
        Assert.EndsWith("...", cut[1]);
    }

    private static TierRow Head(int t) => new(true, t, false, "", Array.Empty<TierEntry>());

    private static TierRow Cells(int t, int n) =>
        new(false, t, false, "", Enumerable.Range(0, n).Select(_ => new TierEntry(default, t, false, "")).ToList());

    [Fact]
    public void Heading_rows_are_shorter_than_cameo_rows()
    {
        TierRow[] rows = { Head(1), Cells(1, 1), Head(2), Cells(2, 1), Head(3), Cells(3, 1) };
        var placed = TierGrouping.Place(rows, 0, 1000, 60, 20, 4);
        Assert.Equal(6, placed.Count);
        Assert.Equal(20, placed[0].Height);
        Assert.Equal(60, placed[1].Height);
        Assert.Equal(24, placed[1].Y);
        Assert.Equal(88, placed[2].Y);   // no empty cameo-sized gap under the heading
    }

    [Fact]
    public void Rows_that_do_not_fit_whole_are_not_placed_and_scroll_limit_shows_the_last_row()
    {
        TierRow[] rows = { Head(1), Cells(1, 1), Head(2), Cells(2, 1) };
        Assert.Equal(2, TierGrouping.Place(rows, 0, 100, 60, 20, 4).Count);
        int max = TierGrouping.MaxFirstRow(rows, 100, 60, 20, 4);
        Assert.Equal(2, max);
        Assert.Equal(2, TierGrouping.Place(rows, max, 100, 60, 20, 4).Count);
        Assert.Equal(0, TierGrouping.MaxFirstRow(rows, 1000, 60, 20, 4));
    }

    [Fact]
    public void Controls_thumb_stays_inside_track()
    {
        var track = new Rectangle(0, 10, 6, 200);
        Rectangle top = ControlsScreen.ThumbRect(track, 100, 20, 0), bottom = ControlsScreen.ThumbRect(track, 100, 20, 80);
        Assert.Equal(track.Y, top.Y);
        Assert.Equal(track.Bottom, bottom.Bottom);
        Assert.Equal(track, ControlsScreen.ThumbRect(track, 10, 20, 0));
    }
}
