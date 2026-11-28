// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Data;
using Rts.Scripting.Tests.M8_15;
using Rts.Sim;
using Rts.Sim.Core;

namespace Rts.Scripting.Tests.S48;

/// <summary>
/// S48 (C-12): auth_06 "The Mill Town" loads and runs at tiers 1, 3 and 5 without script errors or locale warnings, its objectives and
/// hidden bonus are set up as designed, the lose conditions fire, the allow-list holds, and its scripted bot walk wins.
/// </summary>
public sealed class S48Auth06Tests
{
    private const string Mission = "auth_06";

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
        Assert.Equal(new[] { "hatches", "vents", "yard", "spur" }, m.Host.VisibleObjectives.Select(o => o.Id));   // the elevator bonus stays hidden until it is seen
        Assert.Single(m.Host.Timers);
    }

    [Fact]
    public void The_mission_is_wired_into_the_campaign_data()
    {
        MissionSession m = MissionSession.Create(TestData.DataDir, Mission);
        Assert.Equal("authority", m.Def.Faction);
        Assert.Equal(1, m.Def.Act);
        Assert.Equal("auth_02", m.Def.Next[0]);
        Assert.Equal("rustbelt_mill.map.toml", m.Def.MapFile);
        Assert.Equal(new[] { "auth_tank", "auth_scraper", "auth_silo", "auth_ration_hall", "auth_seismograph" }, m.Def.UnlocksOnWin);
        // ASSUMED: auth_01's end state plus auth_05's unlocks (wp/s48-auth05); the lead fixes it at merge.
        Assert.Equal(new[] { "auth_power", "auth_refinery", "auth_barracks", "auth_rifleman", "auth_jeep", "auth_wall", "auth_turret", "auth_factory", "auth_engineer", "auth_rocketeer",
                "auth_grouting", "auth_up_composite_armour", "auth_up_convoy_drill", "auth_up_flak_vests", "auth_up_signals_net", "auth_up_sodium_lamps", "auth_up_tungsten_shot" },
            m.Def.StartsWith);
        var bot = new MissionBot(m);
        Assert.Equal(1, bot.Count("auth_conyard"));
        Assert.Equal(0, bot.Count("auth_factory"));   // the factory is the first thing the player raises
        Assert.Equal(2, bot.Count("auth_scraper"));
    }

    [Fact]
    public void The_allow_list_is_the_carry_over_plus_the_mission_kit()
    {
        MissionDef d = MissionLoader.Load(MissionLoader.FolderOf(TestData.DataDir, Mission));
        var allow = new HashSet<string>(d.StartsWith);
        foreach (string t in d.Players.Single(p => p.Role == "human").Tech ?? Array.Empty<string>()) allow.Add(t);
        foreach (string id in d.UnlocksOnWin) Assert.Contains(id, allow);   // the kit it unlocks is buildable in it
        Assert.DoesNotContain("auth_engineering", allow);                    // auth_02's unlocks stay out
        Assert.DoesNotContain("auth_decon_truck", allow);
        Assert.All(d.StartsWith, id => Assert.DoesNotContain(id, d.UnlocksOnWin));
        // auth_02 starts with exactly what this mission ends with
        MissionDef next = MissionLoader.Load(MissionLoader.FolderOf(TestData.DataDir, "auth_02"));
        static bool Upgrade(string id) => id.StartsWith("auth_up_", StringComparison.Ordinal) || id == "auth_grouting";   // Q-94: upgrade ids ride along
        Assert.Equal(next.StartsWith.Where(x => !Upgrade(x)).OrderBy(x => x, StringComparer.Ordinal),
            d.StartsWith.Concat(d.UnlocksOnWin).Where(x => !Upgrade(x)).OrderBy(x => x, StringComparer.Ordinal));
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
    public void Idling_loses_when_the_grain_train_turns_back()
    {
        MissionSession m = MissionSession.Create(TestData.DataDir, Mission);
        var bot = new MissionBot(m);
        bot.RunUntil(() => m.Host.Outcome != MissionOutcome.None, 15200, every: 20);
        Assert.Empty(m.Host.Errors);
        Assert.Equal(MissionOutcome.Lost, m.Host.Outcome);
    }

    [Fact]
    public void Losing_the_yard_loses_the_mission()
    {
        MissionSession m = MissionSession.Create(TestData.DataDir, Mission);
        var bot = new MissionBot(m);
        bot.Idle(10);
        // the yard is the pre-placed conyard; knock it down the way a raid would
        Simulation s = m.Sim;
        int yard = SimRulesIndex(s);
        for (int i = 1; i < s.Buildings.Capacity; i++)
            if (s.Buildings.Slots.IsIndexAlive(i) && s.Buildings.Player[i] == 0 && s.Buildings.TypeIndex[i] == yard) s.Buildings.Health[i] = 0;
        bot.Idle(5);
        Assert.Equal(MissionOutcome.Lost, m.Host.Outcome);
        Assert.Equal(ObjectiveStatus.Failed, m.Host.Objectives.Single(o => o.Id == "yard").Status);
    }

    private static int SimRulesIndex(Simulation s) => Rts.Sim.Rules.SimRules.IndexOf(s.Setup.Rules.Buildings, "auth_conyard", b => b.Id);

    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    // Tier 5 is measured over 12 seeds (SeedSweepTests); its fixed seed may lose, so it only has to run cleanly (below).
    public void A_bot_builds_a_base_and_breaks_the_shift_and_wins(int tier)
    {
        WalkResult r = CampaignWalks.Play(Mission, tier, MissionSession.Create(TestData.DataDir, Mission));
        Assert.Null(r.Failure);
        Assert.Empty(r.Errors);
        Assert.Equal(0, r.LocaleWarnings);
        Assert.True(r.Won, r.Diagnostics);
        Assert.True(r.Tick < CampaignWalks.TickLimit(Mission), r.Diagnostics);
    }

    [Fact]
    public void The_walk_pays_the_spur_and_the_elevator_bonus()
    {
        MissionSession m = MissionSession.Create(TestData.DataDir, Mission);
        WalkResult r = CampaignWalks.Play(Mission, 3, m);
        Assert.True(r.Won, r.Diagnostics);
        Assert.Equal(ObjectiveStatus.Completed, m.Host.Objectives.Single(o => o.Id == "spur").Status);
        Assert.Equal(ObjectiveStatus.Completed, m.Host.Objectives.Single(o => o.Id == "elevator").Status);
        Assert.Contains(m.Host.Dialogue, d => d.Key == "a6.radio.win");
        Assert.Contains(m.Host.Dialogue, d => d.Key == "a6.radio.tank");
        Assert.Contains(m.Host.Dialogue, d => d.Key == "a6.radio.hall");
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
