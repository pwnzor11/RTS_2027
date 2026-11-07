// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Game.Sandbox;
using Rts.Game.Screens;
using Rts.Game.Tests.Saves;

namespace Rts.Game.Tests.M8_05;

/// <summary>S12: the objectives panel leaves hidden objectives out, shows locale text for keyed labels, and shows running timers as "label m:ss".</summary>
public sealed class MissionOverlayLinesTests
{
    [Theory]
    [InlineData(1500, false, "Second raid 1:15")]
    [InlineData(1, false, "Second raid 0:01")]
    [InlineData(1200, true, "Second raid 1:00 (paused)")]
    public void A_timer_line_rounds_up_to_whole_seconds(int ticksLeft, bool paused, string expected) =>
        Assert.Equal(expected, MissionObjectiveLines.TimerLine("Second raid", ticksLeft, paused));

    [Fact]
    public void Hidden_objectives_stay_off_the_panel()
    {
        using var t = new SaveTestSupport();
        MissionGame game = MissionGame.Create(t.RulesDir, "auth_01");
        game.Sim.Step(Array.Empty<Rts.Sim.Core.Command>());
        List<string> lines = MissionObjectiveLines.Build(game.Host, game.Sim.Tick);

        Assert.Contains(game.Host.Objectives, o => o.Hidden);
        Assert.Equal(1 + game.Host.VisibleObjectives.Count(), lines.Count);
        Assert.DoesNotContain(lines, l => l.Contains("surveyors", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(lines, l => l.Contains("Escort the scraper convoy", StringComparison.Ordinal));   // locale key shown as its text
        Assert.DoesNotContain(lines, l => l.Contains("a1.obj.", StringComparison.Ordinal));
    }
}
