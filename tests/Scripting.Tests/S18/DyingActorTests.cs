// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Sim;
using Rts.Sim.Rules;

namespace Rts.Scripting.Tests.S18;

/// <summary>
/// S18 MIS-03: inside an actor's own OnDestroyed callback (same tick, health 0, not yet removed by the sim's cleanup) Actor.Alive,
/// Actor.Cell and the objective marker treat it as gone.
/// </summary>
public sealed class DyingActorTests
{
    private static List<int> VehiclesOf(Simulation sim, byte player, string type)
    {
        int t = SimRules.IndexOf(sim.Setup.Rules.Vehicles, type, v => v.Id);
        var list = new List<int>();
        for (int i = 1; i < sim.Units.Capacity; i++)
            if (sim.Units.Slots.IsIndexAlive(i) && sim.Units.Player[i] == player && sim.Units.TypeIndex[i] == t) list.Add(i);
        return list;
    }

    [Fact]
    public void Auth01_both_scrapers_dying_together_in_the_convoy_phase_loses_the_mission()
    {
        MissionSession m = MissionSession.Create(TestData.DataDir, "auth_01");
        var bot = new MissionBot(m);
        bot.Idle(5);
        Assert.Equal("convoy", TestAccess.StateValue(m.Host, "phase"));
        List<int> scrapers = VehiclesOf(m.Sim, 0, "auth_scraper");
        Assert.Equal(2, scrapers.Count);
        foreach (int i in scrapers) m.Sim.Units.Health[i] = 0;   // test-only direct kill
        bot.Idle(3);
        Assert.Equal(MissionOutcome.Lost, m.Host.Outcome);
        Assert.Equal(ObjectiveStatus.Failed, m.Host.Objectives.Single(o => o.Id == "scrapers").Status);
        Assert.Empty(m.Host.Errors);
    }

    [Fact]
    public void Auth01_scrapers_dying_one_after_the_other_loses_the_mission_and_the_marker_follows_the_survivor()
    {
        MissionSession m = MissionSession.Create(TestData.DataDir, "auth_01");
        var bot = new MissionBot(m);
        bot.Idle(5);
        List<int> scrapers = VehiclesOf(m.Sim, 0, "auth_scraper");
        MissionObjective obj = m.Host.Objectives.Single(o => o.Id == "scrapers");
        Assert.Equal("scraper1", obj.Target.Name);
        // Which slot is scraper1: the objective cell points at it.
        Assert.True(m.Host.TryObjectiveCell(obj, out var cell1));
        int first = scrapers.Single(i => m.Sim.Units.Position[i].ToCell() == cell1);
        int second = scrapers.Single(i => i != first);

        m.Sim.Units.Health[first] = 0;
        bot.Idle(1);
        Assert.Equal("scraper2", m.Host.Objectives.Single(o => o.Id == "scrapers").Target.Name);
        Assert.Equal(MissionOutcome.None, m.Host.Outcome);

        bot.Idle(10);
        m.Sim.Units.Health[second] = 0;
        bot.Idle(3);
        Assert.Equal(MissionOutcome.Lost, m.Host.Outcome);
        Assert.Empty(m.Host.Errors);
    }

    [Fact]
    public void Auth03_losing_both_carriers_loses_the_mission_at_once()
    {
        MissionSession m = MissionSession.Create(TestData.DataDir, "auth_03");
        var bot = new MissionBot(m);
        bot.Idle(5);
        List<int> carriers = VehiclesOf(m.Sim, 0, "auth_carrier");
        Assert.Equal(2, carriers.Count);
        foreach (int i in carriers) m.Sim.Units.Health[i] = 0;
        bot.Idle(3);
        Assert.Equal(MissionOutcome.Lost, m.Host.Outcome);
        Assert.Equal(ObjectiveStatus.Failed, m.Host.Objectives.Single(o => o.Id == "escort").Status);
        Assert.Empty(m.Host.Errors);
    }

    [Fact]
    public void Alive_and_Cell_are_false_and_nil_inside_the_actors_own_OnDestroyed()
    {
        var (sim, host) = TestData.Tiny(
            "function OnInit() Actor.Create('auth_rifleman', 0, {x=20, y=20}, 'r'); Trigger.OnDestroyed('r', Gone) end\n"
            + "function Gone() state.alive = Actor.Alive('r'); state.cell = (Actor.Cell('r') == nil) end");
        TestData.Run(sim, 2);
        int idx = 0;
        for (int i = 1; i < sim.Infantry.Capacity; i++) if (sim.Infantry.Slots.IsIndexAlive(i)) idx = i;
        sim.Infantry.Health[idx] = 0;
        TestData.Run(sim, 1);
        Assert.Equal("false", TestAccess.StateValue(host, "alive"));
        Assert.Equal("true", TestAccess.StateValue(host, "cell"));
        Assert.Empty(host.Errors);
    }
}
