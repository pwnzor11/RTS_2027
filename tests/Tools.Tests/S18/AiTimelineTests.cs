// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using System.Text.RegularExpressions;
using Rts.Data;
using Rts.Sim.Core;
using Rts.Sim.Map;
using Rts.Sim.Rules;
using Rts.Tools;

namespace Rts.Tools.Tests.S18;

/// <summary>S18 DATA-TOOLS-02 / DATA-TOOLS-04: `ai timeline` builds the map `ai match` plays on (sea and landing markers for an Expedition
/// side), and the start-share label that the PowerShell summaries parse is pinned.</summary>
[Collection("ToolsConsole")]
public sealed class AiTimelineTests
{
    private static int Water(MapData map) => map.Land.Count(l => l is LandType.Water or LandType.Shallow);

    [Theory]
    [InlineData(64)]
    [InlineData(96)]
    public void An_Expedition_game_gets_sea_and_landing_markers_and_other_factions_do_not(int size)
    {
        (MapData sea, IReadOnlyList<CellCoord>? landing) = AiCommands.BuildMatchMap(null, Faction.Expedition, Faction.Authority, size, 1);
        Assert.NotNull(landing);
        Assert.NotEmpty(landing!);
        Assert.True(Water(sea) > 0, "an Expedition map needs sea");

        (MapData land, IReadOnlyList<CellCoord>? none) = AiCommands.BuildMatchMap(null, Faction.Authority, Faction.Directorate, size, 1);
        Assert.Null(none);
        Assert.True(Water(sea) > Water(land), "the Expedition map should have more sea than the plain one");
    }

    [Fact]
    public void A_chosen_layout_gets_the_same_treatment_as_in_a_match()
    {
        var choice = new AiCommands.MapChoice(Biome.GlassCoast, 1);
        (MapData map, IReadOnlyList<CellCoord>? landing) = AiCommands.BuildMatchMap(choice, Faction.Expedition, Faction.Expedition, 96, 2);
        Assert.NotNull(landing);
        Assert.NotEmpty(landing!);
        var gen = MapGenerator.GenerateWithMeta(choice.Params(96, 2, 2, OceanEdges.North | OceanEdges.South));
        Assert.Equal(gen.Map.Land.ToArray(), map.Land.ToArray());
        Assert.Equal(gen.Meta.Landing, landing);
    }

    [Theory]
    [InlineData("expedition")]
    [InlineData("left_behind")]
    [InlineData("directorate")]
    public void Timeline_accepts_every_faction_and_runs(string faction)
    {
        int code = AiCommands.Run(["timeline", "--a", "tiered:3", "--b", "tiered:3", "--faction-a", faction, "--faction-b", faction,
            "--seed", "1", "--ticks", "120", "--size", "96"]);
        Assert.Equal(0, code);
    }

    [Fact]
    public void Timeline_still_refuses_an_unknown_faction()
    {
        Assert.Equal(2, AiCommands.Run(["timeline", "--a", "tiered:3", "--b", "tiered:3", "--faction-a", "martians", "--ticks", "10"]));
    }

    // The label is hard-coded text that tools/measure/run_s15*.ps1, run_s16*.ps1 and run_s17*.ps1 take apart with this very pattern
    // ("start 0 \(\d+,\d+\) (\d+), start 1 \(mirror\) (\d+)"). Changing the label means changing those scripts in the same commit.
    [Fact]
    public void The_start_share_label_matches_what_the_PowerShell_summaries_parse()
    {
        string file = Path.Combine(Path.GetTempPath(), "rts_s18_label_" + Guid.NewGuid().ToString("N") + ".md");
        try
        {
            int code = AiCommands.Run(["match", "--a", "tiered:3", "--b", "tiered:3", "--seeds", "1", "--ticks", "60", "--size", "64", "--out", file]);
            Assert.Equal(0, code);
            string text = File.ReadAllText(file);
            Match m = Regex.Match(text, @"start 0 \(\d+,\d+\) (\d+), start 1 \(mirror\) (\d+)");
            Assert.True(m.Success, "the start-share line no longer matches the pattern in tools/measure/*.ps1:\n" + text);
            Assert.Contains("Wins by start corner: start 0 (4,4) ", text);
            Assert.Contains(", start 1 (mirror) ", text);
        }
        finally
        {
            if (File.Exists(file)) File.Delete(file);
        }
    }

    [Fact]
    public void The_PowerShell_scripts_still_use_that_pattern()
    {
        string? dir = AppContext.BaseDirectory;
        while (dir is not null && !Directory.Exists(Path.Combine(dir, "tools", "measure"))) dir = Path.GetDirectoryName(dir);
        Assert.NotNull(dir);
        string[] scripts = Directory.GetFiles(Path.Combine(dir!, "tools", "measure"), "*.ps1");
        Assert.NotEmpty(scripts);
        foreach (string script in scripts.Where(s => File.ReadAllText(s).Contains("start 1 \\(mirror\\)", StringComparison.Ordinal)))
            Assert.Contains(@"start 0 \(\d+,\d+\) (\d+), start 1 \(mirror\) (\d+)", File.ReadAllText(script));
    }
}
