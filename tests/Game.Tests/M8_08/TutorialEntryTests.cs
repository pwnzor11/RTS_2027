// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Game.Sandbox;
using Rts.Game.Screens;
using Rts.Game.Tests.Saves;

namespace Rts.Game.Tests.M8_08;

/// <summary>M8-09: the tutorial the Campaign screen opens (CampaignFlow.TutorialId) loads and runs, and is not part of any faction campaign.</summary>
public sealed class TutorialEntryTests
{
    [Fact]
    public void The_tutorial_loads_and_its_first_objective_shows()
    {
        using var t = new SaveTestSupport();
        MissionGame game = MissionGame.Create(t.RulesDir, "tut_01", 1);
        game.Sim.Step(Array.Empty<Rts.Sim.Core.Command>());

        List<string> lines = MissionObjectiveLines.Build(game.Host, game.Sim.Tick);
        Assert.Equal(2, lines.Count);   // header + the first lesson; later lessons are hidden until revealed
        Assert.Contains("move them to the marked ground", lines[1], StringComparison.Ordinal);
    }

    [Fact]
    public void No_campaign_lists_the_tutorial()
    {
        using var t = new SaveTestSupport();
        string dir = Path.Combine(t.RulesDir, "..", "campaigns");
        Assert.True(Directory.Exists(dir));
        foreach (string f in Directory.GetFiles(dir, "*.toml"))
            Assert.DoesNotContain('"' + "tut_01" + '"', File.ReadAllText(f), StringComparison.Ordinal);
    }
}
