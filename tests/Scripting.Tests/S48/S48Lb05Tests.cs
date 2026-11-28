// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using System.Text.RegularExpressions;
using Rts.Scripting.Tests.M8_15;
using Rts.Sim;
using Rts.Sim.Core;
using Rts.Sim.Rules;

namespace Rts.Scripting.Tests.S48;

/// <summary>
/// S48 C-12: lb_05 "Under One Banner" loads and runs at tiers 1, 3 and 5 without script errors or locale warnings, is wired into the
/// Left Behind campaign between lb_01 and lb_02, keeps its tech inside lb_01's end set, loses when the Gathering leaves, wins by its bot
/// walk, and pays the hidden Mother Rig bonus when a Tinker takes the well's rig.
/// </summary>
public sealed class S48Lb05Tests
{
    private const string Mission = "lb_05";

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
        Assert.Equal(new[] { "outpost", "well" }, m.Host.VisibleObjectives.Select(o => o.Id));   // the Mother Rig bonus is hidden
        Assert.Equal(3, m.Host.Objectives.Count);
        Assert.Equal(2, m.Host.Timers.Count);
        Assert.True(m.Sim.Players.KeepAlive[1]);
    }

    [Fact]
    public void The_mission_is_wired_into_the_Left_Behind_campaign_between_lb_01_and_lb_02()
    {
        MissionSession m = MissionSession.Create(TestData.DataDir, Mission);
        Assert.Equal("left_behind", m.Def.Faction);
        Assert.Equal(1, m.Def.Act);
        Assert.Equal("lb_06", m.Def.Next[0]);
        Assert.Equal(360, m.Def.ParSeconds);   // S48 sweep sw48b (D-289)
        Assert.Equal(new[] { "lb_buggy", "lb_gleaner" }, m.Def.UnlocksOnWin);

        string toml = File.ReadAllText(Path.Combine(TestData.DataDir, "campaigns", "left_behind.toml"));
        var ids = Regex.Matches(toml, "(?m)^id = \"([a-z0-9_]+)\"").Select(x => x.Groups[1].Value).ToList();
        Assert.Equal(new[] { "lb_01", "lb_05", "lb_06", "lb_02", "lb_03", "lb_04" }, ids);   // S48: lb_06 (play position 3) follows
        Assert.Matches("(?s)id = \"lb_05\"\\s*requires = \\[\"lb_01\"\\]", toml);
        Assert.Matches("(?s)id = \"lb_06\"[^\\[]*requires = \\[\"lb_05\"\\]", toml);
        Assert.Contains("open_after = [\"auth_02\", \"dir_02\"]", toml);   // D-341 Q8: the faction still opens after the same missions
    }

    [Fact]
    public void The_allow_list_is_a_subset_of_what_lb_01_ends_with_and_holds_no_vehicle_building()
    {
        MissionDef five = MissionLoader.Load(MissionLoader.FolderOf(TestData.DataDir, Mission));
        MissionDef one = MissionLoader.Load(MissionLoader.FolderOf(TestData.DataDir, "lb_01"));
        var ends = new HashSet<string>(one.StartsWith.Concat(one.UnlocksOnWin));
        // Q-94: the faction's upgrade ids are listed in every mission (S48UpgradeGateTests); lb_01 has no researching building so it lists none.
        Assert.All(five.StartsWith.Where(t => !t.Contains("_up_")), t => Assert.Contains(t, ends));
        var human = five.Players.Single(p => p.Role == "human").Tech!;
        Assert.All(human, t => Assert.Contains(t, five.StartsWith));
        Assert.DoesNotContain("lb_garage_rig", five.StartsWith);   // buggy and gleaner are this mission's reward: a garage would have nothing to build
        Assert.DoesNotContain("lb_buggy", five.StartsWith);
        Assert.DoesNotContain("lb_gleaner", five.StartsWith);
    }

    [Fact]
    public void Lb_02_still_starts_with_everything_lb_05_unlocks()
    {
        MissionDef five = MissionLoader.Load(MissionLoader.FolderOf(TestData.DataDir, Mission));
        MissionDef two = MissionLoader.Load(MissionLoader.FolderOf(TestData.DataDir, "lb_02"));
        Assert.All(five.UnlocksOnWin, t => Assert.Contains(t, two.StartsWith));
    }

    [Fact]
    public void The_camp_and_both_holds_are_placed_for_the_right_players()
    {
        MissionSession m = MissionSession.Create(TestData.DataDir, Mission);
        var bot = new MissionBot(m);
        bot.Idle(5);   // the script's OnInit (the starting column) runs on the first step
        Assert.Equal(2200, m.Sim.Players.Cash[0]);
        Assert.Equal(1, bot.Count("lb_mother_rig"));
        Assert.Equal(1, bot.Count("lb_yard"));
        Assert.Equal(1, bot.Count("lb_tinker"));
        Assert.Equal(8, bot.Count("lb_slinger"));
        Assert.Equal(1, Buildings(m.Sim, "lb_mother_rig", 1));
        Assert.Equal(1, Buildings(m.Sim, "lb_dynamo_rig", 1));
        Assert.Equal(1, Buildings(m.Sim, "lb_muster_rig", 1));
        Assert.Equal(1, Buildings(m.Sim, "lb_dynamo_rig", 2));
        Assert.Equal(1, Buildings(m.Sim, "lb_muster_rig", 2));
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
    public void Idling_loses_before_the_Gathering_moves_on_and_no_script_error_is_raised()
    {
        MissionSession m = MissionSession.Create(TestData.DataDir, Mission);
        new MissionBot(m).RunUntil(() => m.Host.Outcome != MissionOutcome.None, 22000, every: 20);
        Assert.Empty(m.Host.Errors);
        Assert.Equal(MissionOutcome.Lost, m.Host.Outcome);
        Assert.Equal(ObjectiveStatus.Failed, m.Host.Objectives.Single(o => o.Id == "outpost").Status);
    }

    [Fact]
    public void Losing_the_Mother_Rig_loses_the_mission()
    {
        MissionSession m = MissionSession.Create(TestData.DataDir, Mission);
        var bot = new MissionBot(m);
        bot.Idle(5);
        Simulation s = m.Sim;
        for (int i = 1; i < s.Buildings.Capacity; i++)
            if (s.Buildings.Slots.IsIndexAlive(i) && s.Buildings.Player[i] == 0 && IsType(s, i, "lb_mother_rig")) s.Buildings.Health[i] = 0;   // test-only direct kill
        Assert.True(bot.RunUntil(() => m.Host.Outcome != MissionOutcome.None, 400, every: 10));
        Assert.Equal(MissionOutcome.Lost, m.Host.Outcome);
        Assert.Contains(m.Host.Messages, x => x.Text.Contains("Mother Rig is lost", StringComparison.Ordinal));
        Assert.Empty(m.Host.Errors);
    }

    [Fact]
    public void Breaking_the_hold_completes_the_first_objective_cancels_the_muster_and_sends_the_answer()
    {
        MissionSession m = MissionSession.Create(TestData.DataDir, Mission);
        var bot = new MissionBot(m);
        bot.Idle(5);
        KillBuildings(m.Sim, 2, "lb_dynamo_rig", "lb_muster_rig");
        Assert.True(bot.RunUntilObjective("outpost", 200), bot.Diagnostics());
        Assert.DoesNotContain(m.Host.Timers, t => t.Id == "muster");
        Assert.True(bot.RunUntil(() => m.Host.Messages.Any(x => x.Text.Contains("has sent its answer", StringComparison.Ordinal)), 1200, every: 10), bot.Diagnostics());
        Assert.Equal(ObjectiveStatus.Active, m.Host.Objectives.Single(o => o.Id == "well").Status);
        Assert.Empty(m.Host.Errors);
    }

    [Fact]
    public void Idling_lets_the_hold_march_on_the_well()
    {
        MissionSession m = MissionSession.Create(TestData.DataDir, Mission);
        var bot = new MissionBot(m);
        Assert.True(bot.RunUntil(() => m.Host.Messages.Any(x => x.Text.Contains("marching on the well", StringComparison.Ordinal)), 7400, every: 20), bot.Diagnostics());
        Assert.Empty(m.Host.Errors);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    // Tier 5 is measured over 12 seeds (SeedSweepTests); its fixed seed may lose, so it only has to run cleanly (below).
    public void A_bot_breaks_the_hold_meets_the_answer_and_takes_the_well(int tier)
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
    public void A_Tinker_takes_the_hurt_Mother_Rig_and_pays_the_hidden_bonus()
    {
        MissionSession m = MissionSession.Create(TestData.DataDir, Mission);
        var bot = new MissionBot(m);
        bot.Idle(5);
        Simulation s = m.Sim;
        // test-only: clear the well of its guards and nests, then hurt (not kill) its Mother Rig, as the slingers would
        for (int i = 1; i < s.Units.Capacity; i++) if (s.Units.Slots.IsIndexAlive(i) && s.Units.Player[i] == 1) s.Units.Health[i] = 0;
        for (int i = 1; i < s.Infantry.Capacity; i++) if (s.Infantry.Slots.IsIndexAlive(i) && s.Infantry.Player[i] == 1) s.Infantry.Health[i] = 0;
        KillBuildings(s, 1, "lb_spike_nest");
        for (int i = 1; i < s.Buildings.Capacity; i++)
            if (s.Buildings.Slots.IsIndexAlive(i) && s.Buildings.Player[i] == 1 && IsType(s, i, "lb_mother_rig")) s.Buildings.Health[i] = 400;
        EntityId rig = default, tinker = default;
        for (int i = 1; i < s.Buildings.Capacity; i++)
            if (s.Buildings.Slots.IsIndexAlive(i) && s.Buildings.Player[i] == 1 && IsType(s, i, "lb_mother_rig")) rig = s.Buildings.Slots.IdOf(i);
        int tinkerType = SimRules.IndexOf(s.Setup.Rules.Infantry, "lb_tinker", v => v.Id);
        for (int i = 1; i < s.Infantry.Capacity; i++)
            if (s.Infantry.Slots.IsIndexAlive(i) && s.Infantry.Player[i] == 0 && s.Infantry.TypeIndex[i] == tinkerType) tinker = s.Infantry.Slots.IdOf(i);
        Assert.False(rig.IsNone);
        Assert.False(tinker.IsNone);
        bot.Step(new[] { new Command(s.Tick, 0, bot.NextSeq(), CommandKind.Enter, tinker, rig, default, (int)ActorKind.Infantry | ((int)ActorKind.Building << 8)) });
        Assert.True(bot.RunUntilObjective("mother", 6000), bot.Diagnostics());
        Assert.Equal(2, Buildings(s, "lb_mother_rig", 0));   // the camp's own rig plus the taken one
        Assert.Contains(m.Host.VisibleObjectives, o => o.Id == "mother");
        Assert.Contains(m.Host.Dialogue, d => d.Key == "l5.radio.mother");
        Assert.Empty(m.Host.Errors);
    }

    private static bool IsType(Simulation s, int building, string typeId) =>
        s.Buildings.TypeIndex[building] == SimRules.IndexOf(s.Setup.Rules.Buildings, typeId, b => b.Id);

    private static int Buildings(Simulation s, string typeId, byte player)
    {
        int n = 0;
        for (int i = 1; i < s.Buildings.Capacity; i++)
            if (s.Buildings.Slots.IsIndexAlive(i) && s.Buildings.Player[i] == player && IsType(s, i, typeId)) n++;
        return n;
    }

    private static void KillBuildings(Simulation s, byte player, params string[] typeIds)
    {
        for (int i = 1; i < s.Buildings.Capacity; i++)
            if (s.Buildings.Slots.IsIndexAlive(i) && s.Buildings.Player[i] == player && typeIds.Any(t => IsType(s, i, t))) s.Buildings.Health[i] = 0;   // test-only direct kill
    }
}
