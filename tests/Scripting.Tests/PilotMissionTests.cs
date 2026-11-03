// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Sim;
using Rts.Sim.Core;
using Rts.Sim.Rules;

namespace Rts.Scripting.Tests;

/// <summary>The pilot mission can be won by a simple scripted player, and lost by an idle one.</summary>
public sealed class PilotMissionTests
{
    private sealed class Bot
    {
        private readonly MissionSession _m;
        private ushort _seq;
        public Bot(MissionSession m) => _m = m;

        public void Send(CommandKind kind, EntityId subject, CellCoord cell, int arg, int arg2 = 0) =>
            _m.Sim.Step(new[] { new Command(_m.Sim.Tick, 0, _seq++, kind, subject, default, cell, arg, arg2) });

        public void Idle() => _m.Sim.Step(TestData.NoCommands);

        public void OrderAll(CommandKind kind, CellCoord cell)
        {
            Simulation s = _m.Sim;
            var cmds = new List<Command>();
            for (int i = 1; i < s.Units.Capacity; i++)
                if (s.Units.Slots.IsIndexAlive(i) && s.Units.Player[i] == 0) cmds.Add(new Command(s.Tick, 0, _seq++, kind, s.Units.Slots.IdOf(i), default, cell, (int)ActorKind.Vehicle));
            for (int i = 1; i < s.Infantry.Capacity; i++)
                if (s.Infantry.Slots.IsIndexAlive(i) && s.Infantry.Player[i] == 0) cmds.Add(new Command(s.Tick, 0, _seq++, kind, s.Infantry.Slots.IdOf(i), default, cell, (int)ActorKind.Infantry));
            s.Step(cmds);
        }
    }

    private static int OwnBuildings(Simulation sim)
    {
        int n = 0;
        for (int i = 1; i < sim.Buildings.Capacity; i++)
            if (sim.Buildings.Slots.IsIndexAlive(i) && sim.Buildings.Player[i] == 0) n++;
        return n;
    }

    [Fact]
    public void Idle_player_runs_without_script_errors_and_does_not_win()
    {
        MissionSession m = MissionSession.Create(TestData.DataDir, "auth_01");
        TestData.Run(m.Sim, 6000);
        Assert.Empty(m.Host.Errors);
        Assert.NotEqual(MissionOutcome.Won, m.Host.Outcome);
        Assert.Equal(2, m.Host.Objectives.Count);
    }

    [Fact]
    public void Starting_tech_is_restricted_to_the_mission_list()
    {
        MissionSession m = MissionSession.Create(TestData.DataDir, "auth_01");
        Assert.Equal(3500, m.Sim.Players.Cash[0]);
        var rules = m.Sim.Setup.Rules;
        int tank = SimRules.IndexOf(rules.Vehicles, "auth_tank", v => v.Id);
        int turret = SimRules.IndexOf(rules.Buildings, "auth_turret", b => b.Id);
        Assert.False(m.Sim.IsUnlocked(0, ProductionCategory.Vehicle, tank));
        Assert.False(m.Sim.IsUnlocked(0, ProductionCategory.Defence, turret));
    }

    [Fact]
    public void A_simple_player_can_win()
    {
        MissionSession m = MissionSession.Create(TestData.DataDir, "auth_01");
        var bot = new Bot(m);
        Simulation sim = m.Sim;
        var rules = sim.Setup.Rules;
        int power = SimRules.IndexOf(rules.Buildings, "auth_power", b => b.Id);
        int refinery = SimRules.IndexOf(rules.Buildings, "auth_refinery", b => b.Id);
        var site = new CellCoord(56, 6);
        bot.OrderAll(CommandKind.AttackMove, site);
        int queuedPower = -1, queuedRefinery = -1;
        for (int guard = 0; guard < 14000 && !sim.GameOver; guard++)
        {
            bool build = m.Host.Objectives.Any(o => o.Id == "refinery");
            if (build && queuedPower < 0) { bot.Send(CommandKind.Build, default, default, (int)ProductionCategory.Structure, power); queuedPower = sim.Tick; continue; }
            if (build && sim.Tick % 20 == 0 && queuedPower >= 0 && OwnBuildings(sim) < 2)
            {
                bot.Send(CommandKind.Place, default, new CellCoord(27, 25), power);
                continue;
            }

            if (build && OwnBuildings(sim) >= 2 && queuedRefinery < 0) { bot.Send(CommandKind.Build, default, default, (int)ProductionCategory.Structure, refinery); queuedRefinery = sim.Tick; continue; }
            if (build && queuedRefinery >= 0 && sim.Tick % 20 == 0) { bot.Send(CommandKind.Place, default, new CellCoord(23, 29), refinery); continue; }
            if (guard % 100 == 0 && !build) { bot.OrderAll(CommandKind.AttackMove, site); continue; }
            bot.Idle();
        }

        string diag = $"tick {sim.Tick} objectives [{string.Join("; ", m.Host.Objectives.Select(o => o.Id + "=" + o.Status))}] units {sim.Units.Slots.Count} inf {sim.Infantry.Slots.Count} bld {sim.Buildings.Slots.Count} msgs [{string.Join(" | ", m.Host.Messages.Select(x => x.Tick + ":" + x.Text))}] errors [{string.Join("; ", m.Host.Errors)}] cash {sim.Players.Cash[0]}";
        Assert.True(m.Host.Errors.Count == 0, diag);
        Assert.True(m.Host.Outcome == MissionOutcome.Won, diag);
        Assert.True(sim.GameOver);
        Assert.True(sim.Tick < 12000, diag);   // the design target is about ten minutes (12000 ticks)
        Assert.Equal(0, sim.WinningTeam);
        Assert.All(m.Host.Objectives, o => Assert.Equal(ObjectiveStatus.Completed, o.Status));
    }
}
