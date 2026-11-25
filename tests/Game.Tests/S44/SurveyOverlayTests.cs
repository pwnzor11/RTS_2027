// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Data;
using Rts.Game.Render;
using Rts.Sim.Core;
using Rts.Sim.Economy;
using Rts.Sim.Map;
using Rts.Sim.Rules;

namespace Rts.Game.Tests.S44;

/// <summary>M-14: the Survey Hover richness overlay model (pure, no window).</summary>
public sealed class SurveyOverlayTests
{
    private static readonly SimRules Rules = RulesLoader.Load(RulesLocator.FindRulesDir(AppContext.BaseDirectory)!).Sim;

    private static MapState Map() => new(MapData.CreateFlat(20, 20, "survey"));

    private static readonly Func<CellCoord, bool> All = _ => true;

    [Fact]
    public void Shades_split_the_density_range_into_three_bands()
    {
        Assert.Equal(0, SurveyOverlay.ShadeOf(0));
        Assert.Equal(1, SurveyOverlay.ShadeOf(1));
        Assert.Equal(2, SurveyOverlay.ShadeOf(EconomyState.MaxDensity / 2 + 1));
        Assert.Equal(3, SurveyOverlay.ShadeOf(EconomyState.MaxDensity));
        for (int d = 1; d <= EconomyState.MaxDensity; d++) Assert.InRange(SurveyOverlay.ShadeOf(d), 1, 3);
    }

    [Fact]
    public void Only_fields_inside_the_sight_circle_are_marked()
    {
        MapState map = Map();
        map.SetOverlay(new CellCoord(10, 10), OverlayKind.Ashfall, 12);
        map.SetOverlay(new CellCoord(13, 10), OverlayKind.Pitchblende, 3);   // 3 cells away: in sight 4
        map.SetOverlay(new CellCoord(16, 10), OverlayKind.Ashfall, 6);       // 6 cells away: out
        var dest = new List<RichnessCell>();
        SurveyOverlay.Plan(map, new[] { new SurveySource(new CellCoord(10, 10), 4) }, All, dest);
        Assert.Equal(2, dest.Count);
        Assert.Equal(3, dest[0].Shade);
        Assert.Equal(OverlayKind.Pitchblende, dest[1].Kind);
    }

    [Fact]
    public void Cells_the_viewer_cannot_see_now_are_never_marked()
    {
        MapState map = Map();
        map.SetOverlay(new CellCoord(5, 5), OverlayKind.Ashfall, 8);
        var dest = new List<RichnessCell>();
        SurveyOverlay.Plan(map, new[] { new SurveySource(new CellCoord(5, 5), 3) }, _ => false, dest);
        Assert.Empty(dest);
    }

    [Fact]
    public void Walls_trees_and_empty_fields_are_skipped_and_no_survey_means_no_overlay()
    {
        MapState map = Map();
        map.SetOverlay(new CellCoord(5, 5), OverlayKind.Tree, 0);
        map.SetOverlay(new CellCoord(5, 6), OverlayKind.Ashfall, 0);
        var dest = new List<RichnessCell> { new(new CellCoord(0, 0), OverlayKind.Ashfall, 1, 1) };
        SurveyOverlay.Plan(map, new[] { new SurveySource(new CellCoord(5, 5), 5) }, All, dest);
        Assert.Empty(dest);
        map.SetOverlay(new CellCoord(5, 7), OverlayKind.Ashfall, 5);
        SurveyOverlay.Plan(map, Array.Empty<SurveySource>(), All, dest);
        Assert.Empty(dest);
    }

    [Fact]
    public void Two_surveys_do_not_mark_a_cell_twice()
    {
        MapState map = Map();
        map.SetOverlay(new CellCoord(8, 8), OverlayKind.Ashfall, 4);
        var dest = new List<RichnessCell>();
        SurveyOverlay.Plan(map, new[] { new SurveySource(new CellCoord(7, 8), 3), new SurveySource(new CellCoord(9, 8), 3) }, All, dest);
        Assert.Single(dest);
    }

    [Fact]
    public void The_survey_hover_is_recognised_by_its_rules_id_and_the_toml_todo_is_gone()
    {
        VehicleType survey = Rules.Vehicles.First(v => v.Id == "exp_survey_hover");
        Assert.True(SurveyOverlay.IsSurvey(survey));
        Assert.All(Rules.Vehicles.Where(v => v.Id != survey.Id), v => Assert.False(SurveyOverlay.IsSurvey(v)));
        string toml = File.ReadAllText(Path.Combine(RulesLocator.FindRulesDir(AppContext.BaseDirectory)!, "vehicles.toml"));
        Assert.DoesNotContain("TODO M7-14: \"reveals ash richness\"", toml);
    }
}
