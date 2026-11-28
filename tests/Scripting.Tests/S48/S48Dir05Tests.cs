// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Scripting.Tests.M8_15;
using Rts.Sim;
using Rts.Sim.Core;

namespace Rts.Scripting.Tests.S48;

/// <summary>
/// S48 (C-12 wave 1): dir_05 "Night Shift" loads and runs at tiers 1, 3 and 5 without script errors or locale warnings, loses explicitly
/// (dawn timer, whole shift dead), holds its allow-list, has no audio table, and its scripted bot walk wins.
/// </summary>
public sealed class S48Dir05Tests
{
    private const string Mission = "dir_05";

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
        Assert.Equal(new[] { "power", "home" }, m.Host.VisibleObjectives.Select(o => o.Id));   // the sapper bonus is hidden until the plants are dark
        Assert.Single(m.Host.Timers);
        Assert.True(m.Sim.Players.KeepAlive[1]);
    }

    [Fact]
    public void The_mission_is_wired_into_the_campaign_data()
    {
        MissionSession m = MissionSession.Create(TestData.DataDir, Mission);
        Assert.Equal("directorate", m.Def.Faction);
        Assert.Equal(1, m.Def.Act);
        Assert.Equal("dir_06", m.Def.Next[0]);   // S48: dir_06 is play position 3
        Assert.Equal(0, m.Sim.Players.Cash[0]);
        Assert.Equal(new[] { "dir_smelter_vent", "dir_scraper" }, m.Def.UnlocksOnWin);
        var bot = new MissionBot(m);
        Assert.Equal(8, bot.Count("dir_conscript"));
        Assert.Equal(2, bot.Count("dir_sapper"));
        Assert.Equal(0, bot.Count("dir_shaft"));   // no base
        Assert.Equal(4, new MissionBot(m, 1).Count("auth_power"));   // the three plants and the base's own
    }

    [Fact]
    public void The_allow_list_is_a_subset_of_what_dir_01_ends_with_and_adds_nothing_else()
    {
        MissionDef first = MissionLoader.Load(MissionLoader.FolderOf(TestData.DataDir, "dir_01"));
        MissionDef me = MissionLoader.Load(MissionLoader.FolderOf(TestData.DataDir, Mission));
        var allowed = first.StartsWith.Concat(first.UnlocksOnWin).ToHashSet();
        Assert.All(me.StartsWith, t => Assert.Contains(t, allowed));
        Assert.All(me.Players[0].Tech!, t => Assert.Contains(t, me.StartsWith));
        var dir02 = MissionLoader.Load(MissionLoader.FolderOf(TestData.DataDir, "dir_02"));
        Assert.All(me.UnlocksOnWin, t => Assert.Contains(t, dir02.StartsWith));   // what dir_02 starts with
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
    public void Idling_loses_when_dawn_comes()
    {
        MissionSession m = MissionSession.Create(TestData.DataDir, Mission);
        new MissionBot(m).RunUntil(() => m.Host.Outcome != MissionOutcome.None, 6000, every: 20);
        Assert.Empty(m.Host.Errors);
        Assert.Equal(MissionOutcome.Lost, m.Host.Outcome);
        Assert.Equal(ObjectiveStatus.Failed, m.Host.Objectives.Single(o => o.Id == "home").Status);
    }

    [Fact]
    public void Losing_the_whole_shift_loses_at_once()
    {
        MissionSession m = MissionSession.Create(TestData.DataDir, Mission);
        var b = new MissionBot(m);
        b.Idle(5);
        Simulation s = b.Sim;
        for (int i = 1; i < s.Infantry.Capacity; i++)
            if (s.Infantry.Slots.IsIndexAlive(i) && s.Infantry.Player[i] == 0) s.Infantry.Health[i] = 0;
        Assert.True(b.RunUntil(() => m.Host.Outcome != MissionOutcome.None, 200, every: 1), b.Diagnostics());
        Assert.Equal(MissionOutcome.Lost, m.Host.Outcome);
        Assert.True(s.Tick < 1000, "lost long before dawn");
    }

    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    // Tier 5 is measured over 12 seeds (SeedSweepTests); its fixed seed may lose, so it only has to run cleanly (below).
    public void A_bot_cuts_the_three_plants_and_brings_the_shift_home(int tier)
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
