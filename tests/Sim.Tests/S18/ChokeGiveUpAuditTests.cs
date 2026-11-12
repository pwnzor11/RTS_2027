// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Sim.Core;
using Rts.Sim.Missions;
using Rts.Sim.Rules;
using Rts.Sim.Tests.Determinism;
using Rts.Sim.Tests.Movement;
using Xunit.Abstractions;

namespace Rts.Sim.Tests.S18;

/// <summary>S18 audit (SP-03): a Move ends as "arrived" after 13 blocked waits (130 ticks) without entering a new cell. Scene: a rock wall
/// at x = 30 with a one-cell gap at y = 15, a block of own tanks west of it, one group Move to a point east of it.</summary>
public sealed class ChokeGiveUpAuditTests
{
    private readonly ITestOutputHelper _out;

    public ChokeGiveUpAuditTests(ITestOutputHelper output) => _out = output;

    /// <summary>Returns (tanks that reached the east side, tanks standing idle on the west side with no order left, the tick of the first
    /// such give-up, whether the gap cell is empty at the end).</summary>
    private (int East, int GaveUpWest, int FirstGiveUpTick, bool GapFree) Funnel(int tanks, int speed)
    {
        var map = MoveWorld.Grid(60, 30, (land, w) => { for (int y = 0; y < 30; y++) if (y != 15) land[y * w + 30] = LandType.Rock; });
        VehicleType tank = ScriptedGame.Vehicle("tank", speed, 100);
        var rules = new SimRules(new[] { tank }, new[] { ScriptedGame.Infantry("rifle", 20, 30) }, ScriptedGame.Buildings(),
            Array.Empty<WeaponType>(), Array.Empty<WarheadType>(), MoveWorld.Terrain(), Array.Empty<EffectDef>());
        var sim = new Simulation(new SimSetup(1, map, 2, rules, 1) { AllowDebug = true });
        var ids = new List<EntityId>();
        for (int k = 0; k < tanks; k++) ids.Add(sim.SpawnVehicle(0, 0, new CellCoord(27 - k / 10, 10 + k % 10)));   // columns of ten, nearest the wall first
        sim.Step(ids.Select((id, k) => MoveWorld.Move(id, ActorKind.Vehicle, 45, 15, 0, 0, (ushort)k)).ToArray());
        int firstGiveUp = -1;
        for (int t = 0; t < 6000; t++)
        {
            sim.Step(MoveWorld.None);
            if (firstGiveUp < 0 && ids.Any(id => sim.Units.Mission[id.Index] == MissionKind.Guard && MoveWorld.CellOf(sim, id).X < 30)) firstGiveUp = sim.Tick;
            if (ids.All(id => sim.Units.Mission[id.Index] == MissionKind.Guard)) break;
        }
        int east = ids.Count(id => MoveWorld.CellOf(sim, id).X > 30);
        int west = ids.Count(id => MoveWorld.CellOf(sim, id).X < 30 && sim.Units.Mission[id.Index] == MissionKind.Guard);
        _out.WriteLine($"tanks {tanks} speed {speed}: east {east}, gave up west {west}, first give-up at tick {firstGiveUp}, ended at tick {sim.Tick}");
        return (east, west, firstGiveUp, sim.Map.VehicleAt(new CellCoord(30, 15)).IsNone);
    }

    [Fact]
    public void Control_TwentyTanks_AllGetThroughTheGap()
    {
        var (east, west, _, _) = Funnel(20, 40);
        Assert.Equal(20, east);
        Assert.Equal(0, west);
    }

    [Theory]
    [InlineData(40)]
    [InlineData(60)]
    public void BigGroupThroughAOneCellGap_EveryTankArrives(int tanks)
    {
        var (east, west, _, _) = Funnel(tanks, 40);
        Assert.Equal(tanks, east);
        Assert.Equal(0, west);
    }
}
