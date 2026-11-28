// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Scripting.Tests.M8_15;
using Rts.Sim;
using Rts.Sim.Core;

namespace Rts.Scripting.Tests.S48;

/// <summary>
/// S48 C-12 wave 1: exp_05 "The South Coast" loads and runs at tiers 1, 3 and 5 without script errors or locale warnings, starts without a base,
/// loses explicitly when the tide timer runs out or the whole party dies, keeps its allow-list, has no audio table (text only), and its scripted
/// bot walk wins.
/// </summary>
public sealed class S48Exp05Tests
{
    private const string Mission = "exp_05";

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
        Assert.Equal(new[] { "wreck", "log", "out" }, m.Host.VisibleObjectives.Select(o => o.Id));   // the platform bonus is hidden until the wreck is found
        Assert.Contains(m.Host.Timers, t => t.Id == "tide");
        Assert.True(m.Sim.Players.KeepAlive[1]);
    }

    [Fact]
    public void The_mission_is_wired_into_the_campaign_data()
    {
        MissionSession m = MissionSession.Create(TestData.DataDir, Mission);
        Assert.Equal("expedition", m.Def.Faction);
        Assert.Equal(1, m.Def.Act);
        Assert.Equal("exp_06", m.Def.Next[0]);
        Assert.Equal("tern_point.map.toml", m.Def.MapFile);
        Assert.Equal(0, m.Sim.Players.Cash[0]);
        Assert.Equal(new[] { "exp_dredge_works", "exp_skimmer", "exp_tank_farm" }, m.Def.UnlocksOnWin);
        var bot = new MissionBot(m);
        Assert.Equal(0, bot.Count("exp_landing_depot") + bot.Count("exp_command_ship"));   // no base, no ship
        Assert.Equal(1, bot.Count("exp_survey_hover"));
        Assert.Equal(8, bot.Count("exp_marine"));
        Assert.Equal(2, bot.Count("exp_pathfinder"));
    }

    [Fact]
    public void The_allow_list_is_the_exp_01_kit_and_the_dredge_kit_is_the_reward()
    {
        MissionSession m = MissionSession.Create(TestData.DataDir, Mission);
        HashSet<string> allow = m.Def.StartsWith.ToHashSet();
        foreach (string id in new[] { "exp_pile_house", "exp_billet", "exp_marine", "exp_pillbox", "exp_beacon", "exp_anchorage" }) Assert.Contains(id, allow);
        foreach (string id in new[] { "exp_dredge_works", "exp_skimmer", "exp_tank_farm", "exp_hover_yard", "exp_glide_tank" }) Assert.DoesNotContain(id, allow);
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
    public void Idling_loses_when_the_tide_turns()
    {
        MissionSession m = MissionSession.Create(TestData.DataDir, Mission);
        new MissionBot(m).RunUntil(() => m.Host.Outcome != MissionOutcome.None, 6000, every: 20);
        Assert.Empty(m.Host.Errors);
        Assert.Equal(MissionOutcome.Lost, m.Host.Outcome);
        Assert.Equal(ObjectiveStatus.Failed, m.Host.Objectives.Single(o => o.Id == "log").Status);
        Assert.Contains(m.Host.Messages, x => x.Text.Contains("tide has turned"));
    }

    [Fact]
    public void Losing_the_whole_party_loses_explicitly()
    {
        MissionSession m = MissionSession.Create(TestData.DataDir, Mission);
        var bot = new MissionBot(m);
        bot.Idle(10);
        Simulation s = m.Sim;
        for (int i = 1; i < s.Infantry.Capacity; i++)
            if (s.Infantry.Slots.IsIndexAlive(i) && s.Infantry.Player[i] == 0) s.Infantry.Health[i] = 0;
        for (int i = 1; i < s.Units.Capacity; i++)
            if (s.Units.Slots.IsIndexAlive(i) && s.Units.Player[i] == 0) s.Units.Health[i] = 0;
        Assert.True(bot.RunUntil(() => m.Host.Outcome != MissionOutcome.None, 400, every: 5), bot.Diagnostics());
        Assert.Equal(MissionOutcome.Lost, m.Host.Outcome);
        Assert.Empty(m.Host.Errors);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    // Tier 5 is measured over 12 seeds (SeedSweepTests); its fixed seed may lose, so it only has to run cleanly (below).
    public void A_bot_clears_the_wreck_and_runs_the_log_out_to_sea(int tier)
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
