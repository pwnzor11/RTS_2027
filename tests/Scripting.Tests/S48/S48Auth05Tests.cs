// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Scripting.Tests.M8_15;
using Rts.Sim;
using Rts.Sim.Core;

namespace Rts.Scripting.Tests.S48;

/// <summary>
/// S48 C-12: auth_05 "Hatch Country" loads and runs at tiers 1, 3 and 5 without script errors or locale warnings, loses when the
/// "name read out" timer runs out, has no audio table (text only), keeps its allow-list, and its scripted bot walk wins.
/// </summary>
public sealed class S48Auth05Tests
{
    private const string Mission = "auth_05";

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
        Assert.Equal(new[] { "hatches", "pole" }, m.Host.VisibleObjectives.Select(o => o.Id));   // the logbook bonus is hidden
        Assert.Equal(3, m.Host.Objectives.Count);
        Assert.Single(m.Host.Timers);
        Assert.True(m.Sim.Players.KeepAlive[1]);
    }

    [Fact]
    public void The_mission_is_wired_into_the_campaign_data()
    {
        MissionSession m = MissionSession.Create(TestData.DataDir, Mission);
        Assert.Equal("authority", m.Def.Faction);
        Assert.Equal(1, m.Def.Act);
        Assert.Equal(0, m.Sim.Players.Cash[0]);
        Assert.Equal(new[] { "auth_engineer", "auth_rocketeer" }, m.Def.UnlocksOnWin);
        Assert.DoesNotContain("auth_engineer", m.Def.StartsWith);   // introduced here, not before
        Assert.DoesNotContain("auth_rocketeer", m.Def.StartsWith);
        var bot = new MissionBot(m);
        Assert.Equal(0, bot.Count("auth_conyard"));   // no base
        Assert.Equal(2, bot.Count("auth_jeep"));
        Assert.Equal(6, bot.Count("auth_rifleman"));
        Assert.Equal(2, bot.Count("auth_engineer"));
        Assert.Equal(2, bot.Count("auth_rocketeer"));
        Assert.Equal(3, EnemyBuildings(m, "dir_hatch"));
        Assert.Equal(1, EnemyBuildings(m, "dir_mast"));
    }

    private static int EnemyBuildings(MissionSession m, string typeId)
    {
        Simulation s = m.Sim;
        int type = Rts.Sim.Rules.SimRules.IndexOf(s.Setup.Rules.Buildings, typeId, b => b.Id);
        int n = 0;
        for (int i = 1; i < s.Buildings.Capacity; i++)
            if (s.Buildings.Slots.IsIndexAlive(i) && s.Buildings.Player[i] == 1 && s.Buildings.TypeIndex[i] == type) n++;
        return n;
    }

    [Fact]
    public void The_allow_list_is_the_slot_in_set_plus_the_two_new_soldiers()
    {
        MissionSession m = MissionSession.Create(TestData.DataDir, Mission);
        // Q-94: the faction's upgrade ids are listed in every mission (gate test S48UpgradeGateTests); this test is about units and buildings.
        static bool Upgrade(string id) => id.Contains("_up_") || id.EndsWith("_grouting");
        var allow = new HashSet<string>(m.Def.StartsWith.Where(t => !Upgrade(t)));
        foreach (string t in m.Def.Players.Single(p => p.Role == "human").Tech ?? Array.Empty<string>()) if (!Upgrade(t)) allow.Add(t);
        string[] auth01End = { "auth_power", "auth_refinery", "auth_barracks", "auth_rifleman", "auth_jeep", "auth_wall", "auth_factory", "auth_turret" };
        Assert.All(m.Def.StartsWith.Where(id => !Upgrade(id)), id => Assert.Contains(id, auth01End));   // starts_with is a subset of what auth_01 ends with
        Assert.Equal(auth01End.Concat(new[] { "auth_engineer", "auth_rocketeer" }).Order(), allow.Order());
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
    public void Idling_loses_when_the_name_is_read_out()
    {
        MissionSession m = MissionSession.Create(TestData.DataDir, Mission);
        new MissionBot(m).RunUntil(() => m.Host.Outcome != MissionOutcome.None, 10000, every: 20);
        Assert.Empty(m.Host.Errors);
        Assert.Equal(MissionOutcome.Lost, m.Host.Outcome);
        Assert.Equal(ObjectiveStatus.Failed, m.Host.Objectives.Single(o => o.Id == "hatches").Status);
        Assert.Equal(ObjectiveStatus.Failed, m.Host.Objectives.Single(o => o.Id == "pole").Status);
        Assert.Contains(m.Host.Messages, x => x.Text.Contains("finished reading out"));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    // Tier 5 is measured over 12 seeds (SeedSweepTests); its fixed seed may lose, so it only has to run cleanly (below).
    public void A_bot_closes_the_hatches_and_the_pole_and_wins(int tier)
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

    [Fact]
    public void The_first_closed_hatch_calls_a_patrol_down_from_the_north()
    {
        MissionSession m = MissionSession.Create(TestData.DataDir, Mission);
        WalkResult r = CampaignWalks.Play(Mission, 3, m);
        Assert.True(r.Won, r.Diagnostics);
        Assert.Contains(m.Host.Messages, x => x.Text.Contains("patrol is coming"));
        Assert.Contains(m.Host.Dialogue, d => d.Key == "a5.radio.win");
        Assert.Equal(ObjectiveStatus.Completed, m.Host.Objectives.Single(o => o.Id == "hatches").Status);
        Assert.Equal(ObjectiveStatus.Completed, m.Host.Objectives.Single(o => o.Id == "pole").Status);
    }

    [Fact]
    public void Entering_the_logbook_zone_pays_the_hidden_bonus()
    {
        MissionSession m = MissionSession.Create(TestData.DataDir, Mission);
        var b = new MissionBot(m);
        b.OrderAll(CommandKind.Move, new CellCoord(52, 4));
        Assert.True(b.RunUntil(() => m.Host.Objectives.Single(o => o.Id == "log").Status == ObjectiveStatus.Completed, 4000, every: 10), b.Diagnostics());
        Assert.Empty(m.Host.Errors);
    }
}
