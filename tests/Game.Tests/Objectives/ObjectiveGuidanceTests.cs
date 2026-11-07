// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Microsoft.Xna.Framework;
using Rts.Game.Ui.Objectives;
using Rts.Scripting;
using Rts.Sim.Core;

namespace Rts.Game.Tests.Objectives;

/// <summary>M11-05: arrow placement, distance text, the camera pan, cycling, banner and the setting.</summary>
public sealed class ObjectiveGuidanceTests
{
    private static readonly Rectangle View = new(0, 0, 1000, 600);

    [Fact]
    public void A_target_inside_the_view_is_on_screen()
    {
        ArrowPlacement p = GuidanceMath.Place(new Vector2(500, 300), View, 36);
        Assert.True(p.OnScreen);
        Assert.Equal(new Vector2(500, 300), p.Edge);
    }

    [Theory]
    [InlineData(2000f, 300f, 964f, 300f)]    // east: right edge inset by the margin
    [InlineData(-900f, 300f, 36f, 300f)]     // west
    [InlineData(500f, -900f, 500f, 36f)]     // north
    [InlineData(500f, 5000f, 500f, 564f)]    // south
    public void An_off_screen_target_puts_the_arrow_on_the_matching_edge(float tx, float ty, float ex, float ey)
    {
        ArrowPlacement p = GuidanceMath.Place(new Vector2(tx, ty), View, 36);
        Assert.False(p.OnScreen);
        Assert.Equal(ex, p.Edge.X, 1);
        Assert.Equal(ey, p.Edge.Y, 1);
        Assert.Equal(1f, p.Direction.Length(), 3);
    }

    [Fact]
    public void A_diagonal_target_lands_on_the_corner_side_it_points_at_and_stays_inside_the_inset_view()
    {
        ArrowPlacement p = GuidanceMath.Place(new Vector2(3000, 2000), View, 36);
        Assert.False(p.OnScreen);
        Assert.InRange(p.Edge.X, 36f, 964f);
        Assert.InRange(p.Edge.Y, 36f, 564f);
        Assert.True(p.Edge.X > 500 && p.Edge.Y > 300);
    }

    [Theory]
    [InlineData(10, 10, 44, 10, "34 cells east")]
    [InlineData(10, 10, 10, 11, "1 cell south")]
    [InlineData(10, 10, 0, 10, "10 cells west")]
    [InlineData(10, 10, 10, 2, "8 cells north")]
    [InlineData(10, 10, 20, 20, "10 cells south-east")]
    [InlineData(10, 10, 4, 4, "6 cells north-west")]
    [InlineData(10, 10, 30, 14, "20 cells east")]   // mostly east: no diagonal word
    [InlineData(10, 10, 10, 10, "here")]
    public void Distance_text_names_cells_and_one_of_eight_directions(int fx, int fy, int tx, int ty, string expected) =>
        Assert.Equal(expected, GuidanceMath.DistanceText(new CellCoord(fx, fy), new CellCoord(tx, ty)));

    [Fact]
    public void The_camera_pan_runs_about_six_tenths_of_a_second_with_an_ease_and_ends_on_the_target()
    {
        var pan = new CameraPan();
        Assert.Null(pan.Step(0.1f));
        pan.Start(new Vector2(0, 0), new Vector2(100, 200));
        Vector2 first = pan.Step(0.1f)!.Value;
        Assert.True(first.X > 0 && first.X < 100f / 6f * 1.0f + 1);   // eased start: slower than linear
        Vector2 last = Vector2.Zero;
        for (int i = 0; i < 10; i++) last = pan.Step(0.1f) ?? last;
        Assert.Equal(new Vector2(100, 200), last);
        Assert.False(pan.Active);
        pan.Start(Vector2.Zero, Vector2.One * 10);
        pan.Cancel();
        Assert.Null(pan.Step(0.1f));
    }

    private static MissionObjective Obj(string id, ObjectiveStatus st = ObjectiveStatus.Active, bool primary = true) =>
        new(id, "Text " + id, primary, st);

    [Fact]
    public void Tab_cycles_active_objectives_that_have_targets_and_wraps()
    {
        var m = new ObjectiveGuidanceModel();
        var list = new List<MissionObjective> { Obj("a"), Obj("b"), Obj("c"), Obj("d", ObjectiveStatus.Completed) };
        bool Has(MissionObjective o) => o.Id is "a" or "c" or "d";   // b has no target; d is done
        Assert.Equal("a", m.Guided(list, Has)!.Id);
        Assert.Equal("c", m.Cycle(list, Has)!.Id);
        Assert.Equal("a", m.Cycle(list, Has)!.Id);
        Assert.Null(m.Cycle(list, _ => false));
    }

    [Fact]
    public void Guidance_falls_back_to_the_first_active_objective_with_a_target()
    {
        var m = new ObjectiveGuidanceModel();
        var list = new List<MissionObjective> { Obj("a"), Obj("b") };
        Assert.Equal("a", m.Current(list)!.Id);
        Assert.Equal("b", m.Guided(list, o => o.Id == "b")!.Id);
        Assert.Null(m.Guided(list, _ => false));
    }

    [Fact]
    public void The_one_line_counts_among_active_objectives_and_marks_optional_ones()
    {
        var m = new ObjectiveGuidanceModel();
        string L(string s) => s;
        var list = new List<MissionObjective> { Obj("a", ObjectiveStatus.Completed), Obj("b"), Obj("c", primary: false) };
        Assert.Equal("Objective 1 of 2: Text b", m.OneLine(list, L));
        Assert.Equal("All objectives resolved", m.OneLine(new List<MissionObjective> { Obj("a", ObjectiveStatus.Completed) }, L));
        Assert.Equal("No objectives", m.OneLine(new List<MissionObjective>(), L));
    }

    [Fact]
    public void A_new_objective_raises_the_banner_for_three_seconds_and_asks_for_one_camera_cue()
    {
        var m = new ObjectiveGuidanceModel();
        string L(string s) => s;
        var list = new List<MissionObjective> { Obj("a") };
        m.Observe(list, 0.016f, announceFirst: false, L);
        Assert.Equal("", m.Banner);                 // a loaded game does not announce what it already had
        Assert.Null(m.TakeArrival());

        list.Add(Obj("b"));
        m.Observe(list, 0.016f, false, L);
        Assert.Equal("NEW OBJECTIVE: Text b", m.Banner);
        Assert.True(m.FlashLeft > 0f);
        Assert.Equal("b", m.TakeArrival());
        Assert.Null(m.TakeArrival());

        m.Observe(list, 2.9f, false, L);
        Assert.NotEqual("", m.Banner);
        m.Observe(list, 0.2f, false, L);
        Assert.Equal("", m.Banner);
    }

    [Fact]
    public void Mission_start_announces_the_first_objective_and_completion_raises_a_banner_without_a_cue()
    {
        var m = new ObjectiveGuidanceModel();
        string L(string s) => s;
        var list = new List<MissionObjective> { Obj("a") };
        m.Observe(list, 0.016f, announceFirst: true, L);
        Assert.StartsWith("NEW OBJECTIVE", m.Banner);
        Assert.Equal("a", m.TakeArrival());
        list[0] = Obj("a", ObjectiveStatus.Completed);
        m.Observe(list, 4f, true, L);
        m.Observe(list, 0.016f, true, L);
        Assert.StartsWith("OBJECTIVE COMPLETE", m.Banner);
        Assert.Null(m.TakeArrival());
    }

    [Fact]
    public void O_toggles_the_full_list()
    {
        var m = new ObjectiveGuidanceModel();
        Assert.False(m.Expanded);
        m.ToggleExpanded();
        Assert.True(m.Expanded);
    }

    [Fact]
    public void Trim_shortens_with_dots()
    {
        Assert.Equal("abc", GuidanceMath.Trim("abc", 5));
        Assert.Equal("ab...", GuidanceMath.Trim("abcdefgh", 5));
    }

    [Fact]
    public void The_camera_cue_setting_defaults_on_round_trips_and_older_files_get_the_default()
    {
        Assert.True(Settings.Default.CameraCueOnNewObjective);
        Settings off = Settings.Default with { CameraCueOnNewObjective = false };
        Assert.False(Settings.Parse(off.ToToml()).CameraCueOnNewObjective);
        Assert.True(Settings.Parse("version = 2\nfullscreen = true\n").CameraCueOnNewObjective);
    }
}
