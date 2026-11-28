// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Scripting.Tests.M8_15;
using Rts.Sim;

namespace Rts.Scripting.Tests.S48;

/// <summary>
/// S48 C-12 lb_06 "Salvage Rights": loads and runs at tiers 1, 3 and 5 without script errors or locale warnings, has no base, loses when
/// the recovery crews arrive or the column dies, is text only, and its scripted bot walk (shoot a car, strip the wreck) wins.
/// </summary>
public sealed class S48Lb06Tests
{
    private const string Mission = "lb_06";

    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    [InlineData(5)]
    public void The_script_runs_for_a_thousand_ticks_at_every_tier_without_errors(int tier)
    {
        MissionSession m = MissionSession.Create(TestData.DataDir, Mission);
        m.Host.SetDifficulty(tier);
        new MissionBot(m).Idle(1000);
        Assert.Empty(m.Host.Errors);
        Assert.Empty(m.Def.Locale.Warnings);
        Assert.Equal(MissionOutcome.None, m.Host.Outcome);
        Assert.Equal(new[] { "open", "bank" }, m.Host.VisibleObjectives.Select(o => o.Id));   // the strongbox bonus is hidden
        Assert.Single(m.Host.Timers);
        Assert.True(m.Sim.Players.KeepAlive[1]);
    }

    [Fact]
    public void The_mission_is_wired_into_the_campaign_data()
    {
        MissionSession m = MissionSession.Create(TestData.DataDir, Mission);
        Assert.Equal("left_behind", m.Def.Faction);
        Assert.Equal(1, m.Def.Act);
        Assert.Equal("lb_02", m.Def.Next[0]);
        Assert.Equal(0, m.Sim.Players.Cash[0]);
        Assert.Equal(new[] { "lb_hauler", "lb_lookout" }, m.Def.UnlocksOnWin);
        Assert.Contains("lb_buggy", m.Def.StartsWith);    // lb_05's unlock (wp/s48-lb05)
        Assert.Contains("lb_gleaner", m.Def.StartsWith);
        Assert.DoesNotContain("lb_hauler", m.Def.StartsWith);
        var bot = new MissionBot(m);
        Assert.Equal(0, bot.Count("lb_mother_rig"));   // the rig stays packed
        Assert.Equal(1, bot.Count("lb_mother_rig_packed"));
        Assert.Equal(3, bot.Count("lb_hauler"));
        Assert.Equal(1, bot.Count("lb_yard"));
    }

    [Fact]
    public void Voice_lines_are_text_only_and_do_not_error()
    {
        MissionSession m = MissionSession.Create(TestData.DataDir, Mission);
        new MissionBot(m).Idle(5);
        Assert.NotEmpty(m.Host.Dialogue);
        Assert.All(m.Host.Dialogue, d => Assert.True(string.IsNullOrEmpty(d.Voice), d.Key));
        Assert.All(m.Host.Dialogue, d => Assert.DoesNotContain("[", d.Text));
        Assert.Empty(m.Host.Errors);
    }

    [Fact]
    public void Idling_loses_when_the_recovery_crews_arrive()
    {
        MissionSession m = MissionSession.Create(TestData.DataDir, Mission);
        new MissionBot(m).RunUntil(() => m.Host.Outcome != MissionOutcome.None, 6000, every: 20);
        Assert.Empty(m.Host.Errors);
        Assert.Equal(MissionOutcome.Lost, m.Host.Outcome);
        Assert.Equal(ObjectiveStatus.Failed, m.Host.Objectives.Single(o => o.Id == "open").Status);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    // Tier 5 is measured over 12 seeds (SeedSweepTests); its fixed seed may lose, so it only has to run cleanly (below).
    public void A_bot_strips_the_train_and_wins(int tier)
    {
        WalkResult r = CampaignWalks.Play(Mission, tier, MissionSession.Create(TestData.DataDir, Mission));
        Assert.Null(r.Failure);
        Assert.Empty(r.Errors);
        Assert.Equal(0, r.LocaleWarnings);
        Assert.True(r.Won, r.Diagnostics);
        Assert.True(r.Tick < CampaignWalks.TickLimit(Mission), r.Diagnostics);
    }

    [Fact]
    public void Tier_5_runs_cleanly()
    {
        WalkResult r = CampaignWalks.Play(Mission, 5, MissionSession.Create(TestData.DataDir, Mission));
        Assert.Null(r.Failure);
        Assert.Empty(r.Errors);
        Assert.Equal(0, r.LocaleWarnings);
    }
}
