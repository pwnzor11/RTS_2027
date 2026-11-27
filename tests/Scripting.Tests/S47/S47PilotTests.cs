// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Scripting.Tests.M8_15;
using Rts.Sim;
using Rts.Sim.Core;

namespace Rts.Scripting.Tests.S47;

/// <summary>
/// S47 pilot (D-333, D-337): auth_07 "The Defector" loads and runs at tiers 1, 3 and 5 without script errors or locale warnings, ends in a
/// loss when nobody goes to the rendezvous, has no audio table (text only), and its scripted bot walk wins.
/// </summary>
public sealed class S47PilotTests
{
    private const string Mission = "auth_07";

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
        Assert.Equal(2, m.Host.Objectives.Count(o => o.Id is "reach" or "log"));
        Assert.Equal(new[] { "reach" }, m.Host.VisibleObjectives.Select(o => o.Id));   // the log bonus and "bring" appear only once Dray is found
        Assert.Single(m.Host.Timers);
        Assert.True(m.Sim.Players.KeepAlive[1]);
    }

    [Fact]
    public void The_mission_is_wired_into_the_campaign_data()
    {
        MissionSession m = MissionSession.Create(TestData.DataDir, Mission);
        Assert.Equal("authority", m.Def.Faction);
        Assert.Equal(2, m.Def.Act);
        Assert.Equal("auth_08", m.Def.Next[0]);
        Assert.Equal(0, m.Sim.Players.Cash[0]);
        Assert.Equal(600, m.Def.ParSeconds);   // placeholder until the sweep has measured it (D-289)
        Assert.Equal(new[] { "auth_seismic_mortar" }, m.Def.UnlocksOnWin);
        var bot = new MissionBot(m);
        Assert.Equal(0, bot.Count("auth_conyard"));   // no base
        int column = 0;
        foreach (string id in new[] { "auth_carrier", "auth_decon_truck", "auth_jeep", "auth_rifleman", "auth_marksman", "auth_rocketeer" }) column += bot.Count(id);
        Assert.Equal(1 + 1 + 2 + 6 + 2 + 2, column);
        Assert.Equal(0, bot.Count("auth_engineer"));   // Dray is not on the map at the start
    }

    [Fact]
    public void Voice_lines_are_text_only_and_do_not_error()
    {
        MissionSession m = MissionSession.Create(TestData.DataDir, Mission);
        new MissionBot(m).Idle(5);
        Assert.NotEmpty(m.Host.Dialogue);
        Assert.All(m.Host.Dialogue, d => Assert.True(string.IsNullOrEmpty(d.Voice), d.Key));
        Assert.All(m.Host.Dialogue, d => Assert.DoesNotContain("[", d.Text));   // every key resolves to text
        Assert.Empty(m.Host.Errors);
    }

    [Fact]
    public void Idling_loses_when_the_rendezvous_timer_runs_out()
    {
        MissionSession m = MissionSession.Create(TestData.DataDir, Mission);
        new MissionBot(m).RunUntil(() => m.Host.Outcome != MissionOutcome.None, 6000, every: 20);
        Assert.Empty(m.Host.Errors);
        Assert.Equal(MissionOutcome.Lost, m.Host.Outcome);
        Assert.Equal(ObjectiveStatus.Failed, m.Host.Objectives.Single(o => o.Id == "reach").Status);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    // Tier 5 is measured over 12 seeds (SeedSweepTests); its fixed seed may lose, so it only has to run cleanly (below).
    public void A_bot_brings_Dray_home_by_the_long_road_and_wins(int tier)
    {
        WalkResult r = CampaignWalks.Play(Mission, tier, MissionSession.Create(TestData.DataDir, Mission));
        Assert.Null(r.Failure);
        Assert.Empty(r.Errors);
        Assert.Equal(0, r.LocaleWarnings);
        Assert.True(r.Won, r.Diagnostics);
        Assert.True(r.Tick < CampaignWalks.TickLimit(Mission), r.Diagnostics);
    }

    [Fact]
    public void The_walk_collects_the_hidden_log_and_Dray_arrives_alive()
    {
        MissionSession m = MissionSession.Create(TestData.DataDir, Mission);
        WalkResult r = CampaignWalks.Play(Mission, 3, m);
        Assert.True(r.Won, r.Diagnostics);
        Assert.Equal(ObjectiveStatus.Completed, m.Host.Objectives.Single(o => o.Id == "log").Status);
        Assert.Equal(ObjectiveStatus.Completed, m.Host.Objectives.Single(o => o.Id == "bring").Status);
        Assert.Contains(m.Host.Dialogue, d => d.Key == "a7.radio.win");
    }

    [Fact]
    public void Tier_5_runs_cleanly()
    {
        WalkResult r = CampaignWalks.Play(Mission, 5, MissionSession.Create(TestData.DataDir, Mission));
        Assert.Null(r.Failure);
        Assert.Empty(r.Errors);
        Assert.Equal(0, r.LocaleWarnings);
    }

    [Fact]
    public void Reaching_the_spoil_heaps_cancels_the_rendezvous_and_starts_the_alert_clock()
    {
        MissionSession m = MissionSession.Create(TestData.DataDir, Mission);
        var b = new MissionBot(m);
        b.OrderAll(CommandKind.Move, new CellCoord(41, 31));   // straight at the spoil heaps
        Assert.True(b.RunUntil(() => m.Host.Objectives.Any(o => o.Id == "bring"), 4800, every: 10), b.Diagnostics());
        Assert.Contains(m.Host.Timers, t => t.Id == "alert");
        Assert.DoesNotContain(m.Host.Timers, t => t.Id == "rendezvous");
        Assert.Empty(m.Host.Errors);
    }
}
