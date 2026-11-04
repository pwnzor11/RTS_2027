// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Data;
using Rts.Game.Screens.Editor;
using Rts.Sim.Core;

namespace Rts.Game.Tests.Editor;

public class ValidationReportTests
{
    private static readonly string[] Codes =
        ["starts_count", "start_space", "start_distance", "connectivity_foot", "connectivity_tracked", "feature_cell", "few_fields", "symmetry"];

    [Fact]
    public void Every_issue_code_shows_with_its_cell_errors_first()
    {
        var issues = new List<MapIssue>();
        for (int i = 0; i < Codes.Length; i++)
        {
            bool warning = Codes[i] is "few_fields" or "symmetry";
            issues.Add(new MapIssue(warning ? MapIssueSeverity.Warning : MapIssueSeverity.Error, Codes[i], i % 2 == 0 ? new CellCoord(i, i + 1) : null, "msg " + i));
        }
        ValidationReport r = ValidationReport.From(issues);
        Assert.Equal(6, r.ErrorCount);
        Assert.Equal(2, r.WarningCount);
        Assert.False(r.CanSave);
        Assert.All(r.Rows.Take(6), row => Assert.Equal(MapIssueSeverity.Error, row.Severity));
        foreach (string code in Codes) Assert.Contains(r.Rows, row => row.Code == code && row.Text.Contains(code));
        Assert.Equal(new CellCoord(0, 1), r.Rows.First(x => x.Code == "starts_count").Cell);
        Assert.Contains("(0, 1)", r.Rows.First(x => x.Code == "starts_count").Text);
        Assert.Null(r.Rows.First(x => x.Code == "start_space").Cell);
        Assert.Equal("6 error(s), 2 warning(s)", r.Summary);
    }

    [Fact]
    public void Warnings_alone_allow_saving_and_no_issues_says_so()
    {
        var r = ValidationReport.From(new[] { new MapIssue(MapIssueSeverity.Warning, "symmetry", new CellCoord(3, 3), "differs") });
        Assert.True(r.CanSave);
        Assert.True(ValidationReport.From(Array.Empty<MapIssue>()).CanSave);
        Assert.Equal("No issues", ValidationReport.From(Array.Empty<MapIssue>()).Summary);
    }

    [Fact]
    public void Run_passes_the_documents_map_and_meta_to_the_validator()
    {
        var s = new EditorSession(EditorDocument.Create("v", "ash_plains", 32, 32));
        s.SetSymmetry(MapSymmetry.MirrorY);
        Rts.Sim.Map.MapData? seenMap = null; MapMeta? seenMeta = null;
        var r = ValidationReport.Run(s.Document, null, (m, meta, _) =>
        {
            seenMap = m; seenMeta = meta;
            return new[] { new MapIssue(MapIssueSeverity.Error, "starts_count", null, "need 2..4 starts") };
        });
        Assert.Equal(32, seenMap!.Width);
        Assert.Equal(MapSymmetry.MirrorY, seenMeta!.Symmetry);
        Assert.Equal(1, r.ErrorCount);
    }

    [Fact]
    public void The_real_validator_runs_on_a_crafted_bad_map_without_throwing()
    {
        // M4-02 fills the validator; until then it returns nothing. Once it lands a map with no starts must report starts_count.
        var s = new EditorSession(EditorDocument.Create("bad", "", 32, 32));
        ValidationReport r = ValidationReport.Run(s.Document);
        if (r.Rows.Count > 0) Assert.Contains(r.Rows, row => row.Code == "starts_count");
    }

    [Fact]
    public void Layout_maps_screen_points_to_cells_and_centres_a_cell()
    {
        var layout = EditorLayout.Compute(1280, 720, 2);
        Assert.True(layout.MapView.Width > 0 && layout.MapView.Height > 0);
        Assert.False(layout.MapView.Intersects(layout.Issues));
        Assert.False(layout.MapView.Intersects(layout.Tools));
        var origin = EditorLayout.OriginToCentre(layout.MapView, 10, 20, 8);
        var centre = new Microsoft.Xna.Framework.Point(layout.MapView.Center.X, layout.MapView.Center.Y);
        Assert.Equal((10, 20), EditorLayout.CellAt(centre, origin, 8));
        Assert.Equal((-1, -1), EditorLayout.CellAt(new Microsoft.Xna.Framework.Point(origin.X - 1, origin.Y - 1), origin, 8));
    }

    [Fact]
    public void Text_prompt_accepts_printable_ascii_up_to_40()
    {
        Assert.Equal("ab", TextPromptScreen.Typed("a", 'b'));
        Assert.Equal("a", TextPromptScreen.Typed("a", '\n'));
        Assert.Equal(new string('x', 40), TextPromptScreen.Typed(new string('x', 40), 'y'));
    }
}
