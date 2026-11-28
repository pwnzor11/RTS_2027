// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Scripting.Tests.M8_15;
using Rts.Sim;
using Rts.Sim.Core;
using Rts.Sim.Rules;

namespace Rts.Scripting.Tests.S48;

/// <summary>
/// S48 C-12: dir_06 "The Assembly Bore" loads and runs at tiers 1, 3 and 5 without script errors or locale warnings, grants the bore, cistern and
/// gallery only once the smelter vent stands (and the rocket truck once the bore does), loses when the shift is left to idle, has no audio table,
/// and its scripted bot walk wins.
/// </summary>
public sealed class S48Dir06Tests
{
    private const string Mission = "dir_06";

    private static bool CanBuild(Simulation s, string id)
    {
        SimRules r = s.Setup.Rules;
        int b = SimRules.IndexOf(r.Buildings, id, x => x.Id);
        if (b >= 0) return s.IsUnlocked(0, ProductionCategory.Structure, b) || s.IsUnlocked(0, ProductionCategory.Defence, b);
        int v = SimRules.IndexOf(r.Vehicles, id, x => x.Id);
        return v >= 0 && s.IsUnlocked(0, ProductionCategory.Vehicle, v);
    }

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
        Assert.Equal(new[] { "smelter", "bore", "camp" }, m.Host.VisibleObjectives.Select(o => o.Id));   // the stakes bonus is hidden until the vent stands
        Assert.Equal(4, m.Host.Objectives.Count);
        Assert.Contains(m.Host.Timers, t => t.Id == "survey");
        Assert.Contains(m.Host.Dialogue, d => d.Key == "d6.radio.lund");   // Lund speaks for the first time
    }

    [Fact]
    public void The_mission_is_wired_into_the_campaign_data()
    {
        MissionSession m = MissionSession.Create(TestData.DataDir, Mission);
        Assert.Equal("directorate", m.Def.Faction);
        Assert.Equal(1, m.Def.Act);
        Assert.Equal("dir_02", m.Def.Next[0]);
        Assert.Equal("rustbelt_mill.map.toml", m.Def.MapFile);
        Assert.Equal(new[] { "dir_assembly_bore", "dir_rocket_truck", "dir_cistern", "dir_bunk_gallery" }, m.Def.UnlocksOnWin);
        // the rewards are not in the start kit (the script grants them), and dir_05's unlocks are
        Assert.Empty(m.Def.StartsWith.Intersect(m.Def.UnlocksOnWin));
        Assert.Contains("dir_smelter_vent", m.Def.StartsWith);
        Assert.Contains("dir_scraper", m.Def.StartsWith);
        Assert.Contains("dir_sapper", m.Def.StartsWith);
        Assert.Equal(1, new MissionBot(m).Count("dir_shaft"));
        Assert.Equal(0, new MissionBot(m).Count("dir_smelter_vent"));
        Assert.Equal(0, new MissionBot(m).Count("dir_assembly_bore"));
    }

    [Fact]
    public void Voice_lines_are_text_only_and_do_not_error()
    {
        MissionSession m = MissionSession.Create(TestData.DataDir, Mission);
        new MissionBot(m).Idle(500);
        Assert.NotEmpty(m.Host.Dialogue);
        Assert.All(m.Host.Dialogue, d => Assert.True(string.IsNullOrEmpty(d.Voice), d.Key));
        Assert.All(m.Host.Dialogue, d => Assert.DoesNotContain("[", d.Text));   // every key resolves to text
        Assert.Empty(m.Host.Errors);
    }

    [Fact]
    public void The_bore_cistern_gallery_and_rocket_truck_are_earned_not_given()
    {
        MissionSession m = MissionSession.Create(TestData.DataDir, Mission);
        var bot = new MissionBot(m);
        Assert.True(CanBuild(m.Sim, "dir_smelter_vent"));
        foreach (string id in new[] { "dir_assembly_bore", "dir_cistern", "dir_bunk_gallery", "dir_rocket_truck" }) Assert.False(CanBuild(m.Sim, id), id);

        var pad = new List<CellCoord>();
        for (int y = 24; y <= 29; y++) for (int x = 19; x <= 27; x++) pad.Add(new CellCoord(x, y));
        Assert.True(bot.BuildAndPlace("dir_smelter_vent", pad, 2500), bot.Diagnostics());
        Assert.True(bot.RunUntilObjective("smelter", bot.Sim.Tick + 200), bot.Diagnostics());
        foreach (string id in new[] { "dir_assembly_bore", "dir_cistern", "dir_bunk_gallery" }) Assert.True(CanBuild(m.Sim, id), id);
        Assert.False(CanBuild(m.Sim, "dir_rocket_truck"));
        Assert.Contains(m.Host.VisibleObjectives, o => o.Id == "stakes");
        Assert.Contains(m.Host.Dialogue, d => d.Key == "d6.radio.smelter");

        Assert.True(bot.BuildAndPlace("dir_assembly_bore", pad, 2500), bot.Diagnostics());
        Assert.True(bot.BuildAndPlace("dir_cistern", pad, 1500), bot.Diagnostics());
        Assert.True(bot.BuildAndPlace("dir_bunk_gallery", pad, 1500), bot.Diagnostics());
        Assert.True(bot.RunUntilObjective("bore", bot.Sim.Tick + 200), bot.Diagnostics());
        Assert.True(CanBuild(m.Sim, "dir_rocket_truck"));
        Assert.Empty(m.Host.Errors);
    }

    [Fact]
    public void Idling_loses_when_the_shaft_falls()
    {
        MissionSession m = MissionSession.Create(TestData.DataDir, Mission);
        m.Host.SetDifficulty(5);
        new MissionBot(m).RunUntil(() => m.Host.Outcome != MissionOutcome.None, 16000, every: 20);
        Assert.Empty(m.Host.Errors);
        Assert.Equal(MissionOutcome.Lost, m.Host.Outcome);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    // Tier 5 is measured over 12 seeds (SeedSweepTests); its fixed seed may lose, so it only has to run cleanly (below).
    public void A_bot_builds_the_bore_clears_the_camp_and_wins(int tier)
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
