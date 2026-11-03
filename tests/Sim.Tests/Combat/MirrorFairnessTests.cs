// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Data;
using Rts.Sim.Core;
using Rts.Sim.Map;
using Rts.Sim.Rules;
using Xunit.Abstractions;

namespace Rts.Sim.Tests.Combat;

/// <summary>Mirror-symmetric duels on a flat map must end symmetric: no advantage from player number or slot order.</summary>
public class MirrorFairnessTests(ITestOutputHelper output)
{
    private static readonly RuleSet Rules = RulesLoader.Load(TestData.RulesDir);

    [Theory]
    [InlineData("test_tank", 3)]
    [InlineData("test_jeep", 4)]
    [InlineData("test_tank", 1)]
    public void MirrorDuel(string type, int count)
    {
        int vt = SimRules.IndexOf(Rules.Sim.Vehicles, type, v => v.Id);
        var sim = new Simulation(new SimSetup(1, MapData.CreateFlat(32, 32), 2, Rules.Sim, Rules.RulesHash));
        var cmds = new List<Command>();
        ushort seq = 0;
        for (int k = 0; k < count; k++)
        {
            EntityId a = sim.SpawnVehicle(0, vt, new CellCoord(8, 12 + 2 * k));
            EntityId b = sim.SpawnVehicle(1, vt, new CellCoord(23, 12 + 2 * k));
            cmds.Add(new Command(0, 0, seq++, CommandKind.AttackMove, a, EntityId.None, new CellCoord(23, 12 + 2 * k), (int)ActorKind.Vehicle));
            cmds.Add(new Command(0, 1, seq++, CommandKind.AttackMove, b, EntityId.None, new CellCoord(8, 12 + 2 * k), (int)ActorKind.Vehicle));
        }
        sim.Step(cmds);
        int t;
        for (t = 1; t < 3000; t++)
        {
            sim.Step(Array.Empty<Command>());
            var s = sim.Snapshot();
            int p0 = s.Units.Count(u => u.Player == 0), p1 = s.Units.Count(u => u.Player == 1);
            if (p0 == 0 || p1 == 0) break;
        }
        var end = sim.Snapshot();
        Assert.Equal(end.Units.Count(u => u.Player == 0), end.Units.Count(u => u.Player == 1));
        Assert.Equal(end.Units.Where(u => u.Player == 0).Sum(u => u.Health), end.Units.Where(u => u.Player == 1).Sum(u => u.Health));
        output.WriteLine($"{type} x{count}: tick {t} p0 {end.Units.Count(u => u.Player == 0)} hp {end.Units.Where(u => u.Player == 0).Sum(u => u.Health)} | p1 {end.Units.Count(u => u.Player == 1)} hp {end.Units.Where(u => u.Player == 1).Sum(u => u.Health)}");
    }
}
