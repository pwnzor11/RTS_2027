// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Sim.Core;
using Rts.Sim.Map;
using Xunit.Abstractions;
using static Rts.Sim.Tests.Combat.CombatFixture;

namespace Rts.Sim.Tests.Pathing;

/// <summary>M13-05 (S18 SP-01, D-205): 20 tanks ordered to attack one building across a 128x128 map must not stall path solving for
/// everybody else. Before the fix every attacker's search for the (blocked) footprint cell expanded the whole map and one tick
/// solved them all, so a bystander's Move waited 83 ticks.</summary>
public sealed class PathStallTests
{
    private const int PerTickCap = 4 * 4000;   // PathService: at most 4x the per-tick budget of new work is started in one call
    private readonly ITestOutputHelper _out;

    public PathStallTests(ITestOutputHelper output) => _out = output;

    private (int BystanderDelay, int MaxTickCost, int Total) Run(bool attack)
    {
        var sim = new Simulation(new SimSetup(1, MapData.CreateFlat(128, 128), 2, Loaded.Sim, Loaded.RulesHash) { AllowDebug = true });
        EntityId hq = sim.PlaceBuilding(1, Bld("test_hq"), new CellCoord(110, 110));
        var army = new EntityId[20];
        for (int k = 0; k < army.Length; k++) army[k] = sim.SpawnVehicle(0, Veh("test_tank"), new CellCoord(8 + 2 * (k % 5), 8 + 2 * (k / 5)));
        EntityId bystander = sim.SpawnVehicle(1, Veh("test_jeep"), new CellCoord(60, 20));
        CellCoord start = sim.Units.Position[bystander.Index].ToCell();
        for (int t = 0; t < 3; t++) sim.Step(None);
        int delay = -1, maxCost = 0, total = 0;
        for (int t = 0; t < 400; t++)
        {
            Command[] cmds = None;
            if (t == 0)
                cmds = army.Select(a => attack ? Attack(sim, 0, ActorKind.Vehicle, a, ActorKind.Building, hq) : Move(sim, 0, ActorKind.Vehicle, a, 106, 106)).ToArray();
            else if (t == 5) cmds = new[] { Move(sim, 1, ActorKind.Vehicle, bystander, start.X + 6, start.Y) };
            sim.Step(cmds);
            maxCost = Math.Max(maxCost, sim.Paths.LastProcessCost);
            total += sim.Paths.LastProcessCost;
            if (delay < 0 && t >= 5 && sim.Units.Position[bystander.Index] != LeptonCoord.CenterOf(start)) delay = t - 5;
        }
        _out.WriteLine($"attack={attack}: bystander delay {delay} ticks, largest tick {maxCost} expansions, total {total} over 400 ticks");
        return (delay, maxCost, total);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void TwentyTanksOrderedAcrossTheMap_BystanderMovesAtOnce_AndNoTickOverdraws(bool attack)
    {
        var (delay, maxCost, _) = Run(attack);
        Assert.InRange(delay, 0, 10);              // the control Move measured 7 ticks at S18; the attack 83
        Assert.InRange(maxCost, 0, PerTickCap);    // S18: 327 500 expansions in the order tick
    }
}
