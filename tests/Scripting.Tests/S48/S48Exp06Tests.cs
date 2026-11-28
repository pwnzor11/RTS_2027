// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Scripting.Tests.M8_15;
using Rts.Sim;
using Rts.Sim.Core;
using Rts.Sim.Rules;

namespace Rts.Scripting.Tests.S48;

/// <summary>
/// S48 C-12 wave 1: exp_06 "Dredge Rights" loads and runs at tiers 1, 3 and 5 without script errors or locale warnings, keeps its allow-list
/// (the dredge kit and every upgrade id, the hover kit only as a script grant and a reward), loses explicitly when the Command Ship dies,
/// grants the Hover Yard and sends two Glide Tank prototypes once the Dredge Works stands, and its scripted bot walk wins.
/// </summary>
public sealed class S48Exp06Tests
{
    private const string Mission = "exp_06";

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
        Assert.Equal(new[] { "landing", "dredge", "battery" }, m.Host.VisibleObjectives.Select(o => o.Id));   // the wreck bonus is hidden until the works stand
        Assert.True(m.Sim.Players.KeepAlive[1]);
    }

    [Fact]
    public void The_mission_is_wired_into_the_campaign_data()
    {
        MissionSession m = MissionSession.Create(TestData.DataDir, Mission);
        Assert.Equal("expedition", m.Def.Faction);
        Assert.Equal(1, m.Def.Act);
        Assert.Equal("exp_02", m.Def.Next[0]);
        Assert.Equal("tern_point.map.toml", m.Def.MapFile);
        Assert.Equal(new[] { "exp_hover_yard", "exp_glide_tank", "exp_hoverlander", "exp_provision_store", "exp_plotting_room", "exp_gun_battery" }, m.Def.UnlocksOnWin);
        var bot = new MissionBot(m);
        Assert.Equal(1, bot.Count("exp_command_ship"));
        Assert.Equal(0, bot.Count("exp_landing_depot"));
        int howitzer = SimRules.IndexOf(m.Sim.Setup.Rules.Vehicles, "auth_howitzer", v => v.Id);
        int guns = 0;
        for (int i = 1; i < m.Sim.Units.Capacity; i++)
            if (m.Sim.Units.Slots.IsIndexAlive(i) && m.Sim.Units.Player[i] == 1 && m.Sim.Units.TypeIndex[i] == howitzer) guns++;
        Assert.Equal(3, guns);   // the coast battery is part of the map from the first tick
    }

    [Fact]
    public void The_allow_list_is_the_dredge_kit_and_the_hover_kit_comes_from_the_script()
    {
        MissionSession m = MissionSession.Create(TestData.DataDir, Mission);
        HashSet<string> allow = m.Def.StartsWith.ToHashSet();
        foreach (string id in new[] { "exp_pile_house", "exp_billet", "exp_marine", "exp_pillbox", "exp_beacon", "exp_anchorage", "exp_dredge_works", "exp_skimmer", "exp_tank_farm", "exp_up_veteran_drill" })
            Assert.Contains(id, allow);
        foreach (string id in new[] { "exp_hover_yard", "exp_glide_tank", "exp_plotting_room", "exp_gun_battery" }) Assert.DoesNotContain(id, allow);
    }

    private static bool Allowed(Simulation s, string buildingOrVehicle)
    {
        int b = SimRules.IndexOf(s.Setup.Rules.Buildings, buildingOrVehicle, x => x.Id);
        if (b >= 0) return s.UnlockFilter!(0, ProductionCategory.Structure, b);
        int v = SimRules.IndexOf(s.Setup.Rules.Vehicles, buildingOrVehicle, x => x.Id);
        Assert.True(v >= 0, buildingOrVehicle);
        return s.UnlockFilter!(0, ProductionCategory.Vehicle, v);
    }

    [Fact]
    public void Losing_the_Command_Ship_before_the_landing_loses()
    {
        MissionSession m = MissionSession.Create(TestData.DataDir, Mission);
        var bot = new MissionBot(m);
        bot.Idle(10);
        Simulation s = m.Sim;
        for (int i = 1; i < s.Units.Capacity; i++)
            if (s.Units.Slots.IsIndexAlive(i) && s.Units.Player[i] == 0) s.Units.Health[i] = 0;
        Assert.True(bot.RunUntil(() => m.Host.Outcome != MissionOutcome.None || s.GameOver, 400, every: 5), bot.Diagnostics());
        Assert.True(s.GameOver);
        Assert.Equal(1, s.WinningTeam);   // the Authority battery is left standing (the sim or the script ends it, whichever is first)
        Assert.Empty(m.Host.Errors);
    }

    [Fact]
    public void The_Dredge_Works_grants_the_Hover_Yard_and_sends_two_Glide_Tank_prototypes()
    {
        MissionSession m = MissionSession.Create(TestData.DataDir, Mission);
        var bot = new MissionBot(m);
        int ship = SimRules.IndexOf(m.Sim.Setup.Rules.Vehicles, "exp_command_ship", v => v.Id);
        EntityId flagship = default;
        for (int i = 1; i < m.Sim.Units.Capacity; i++)
            if (m.Sim.Units.Slots.IsIndexAlive(i) && m.Sim.Units.Player[i] == 0 && m.Sim.Units.TypeIndex[i] == ship) flagship = m.Sim.Units.Slots.IdOf(i);
        bot.Send(CommandKind.Deploy, flagship, new CellCoord(16, 48), 0);
        Assert.True(bot.RunUntilObjective("landing", 600), bot.Diagnostics());
        Assert.False(Allowed(m.Sim, "exp_hover_yard"));
        CellCoord depot = ExpeditionMissionTestKit.BuildingCell(m.Sim, "exp_landing_depot");
        CellCoord[] spots = ExpeditionMissionTestKit.Spots(m.Sim, depot, 3, 10, c => c.Y <= 46 && c.X <= 28);
        Assert.True(bot.BuildAndPlace("exp_pile_house", spots), bot.Diagnostics());
        Assert.True(bot.BuildAndPlace("exp_dredge_works", ExpeditionMissionTestKit.Spots(m.Sim, new CellCoord(16, 46), 0, 6, c => c.Y >= 43)), bot.Diagnostics());
        bot.Idle(60);
        Assert.True(Allowed(m.Sim, "exp_hover_yard"));
        Assert.False(Allowed(m.Sim, "exp_glide_tank"));   // the Glide Tank needs the Plotting Room (a reward): the prototypes come by sea
        bot.Idle(900);
        Assert.Equal(2, bot.Count("exp_glide_tank"));
        Assert.Equal(ObjectiveStatus.Active, m.Host.Objectives.Single(o => o.Id == "dredge").Status);   // two Skimmers are still needed
        Assert.Contains(m.Host.Dialogue, d => d.Key == "e6.radio.roque");
        Assert.Empty(m.Host.Errors);
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

    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    // Tier 5 is measured over 12 seeds (SeedSweepTests); its fixed seed may lose, so it only has to run cleanly (below).
    public void A_bot_lands_digs_and_breaks_the_battery(int tier)
    {
        MissionSession m = MissionSession.Create(TestData.DataDir, Mission);
        WalkResult r = CampaignWalks.Play(Mission, tier, m);
        Assert.Null(r.Failure);
        Assert.Empty(r.Errors);
        Assert.Equal(0, r.LocaleWarnings);
        Assert.True(r.Won, r.Diagnostics);
        Assert.True(r.Tick < CampaignWalks.TickLimit(Mission), r.Diagnostics);
        Assert.Equal(ObjectiveStatus.Completed, m.Host.Objectives.Single(o => o.Id == "dredge").Status);
        Assert.Equal(ObjectiveStatus.Completed, m.Host.Objectives.Single(o => o.Id == "battery").Status);
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
