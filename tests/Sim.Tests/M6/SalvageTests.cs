// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Sim.Core;
using Rts.Sim.Missions;
using Rts.Sim.Rules;
using static Rts.Sim.Tests.Combat.CombatFixture;
using static Rts.Sim.Tests.Economy.EconomyFixture;

namespace Rts.Sim.Tests.M6;

/// <summary>M6-04: wrecks (spawn, value, decay, table) and the Salvage mission (docs/design/10_m6_plan.md §3.2-3.3).</summary>
public sealed class SalvageTests
{
    /// <summary>A wreck life so long that decay costs at most 1 credit during a test (floor of the linear line).</summary>
    private const int Long = 1_000_000;

    private static readonly CellCoord WreckCell = new(12, 10);

    /// <summary>The test rules with wrecks on (wreck_ticks), a salvaging harvester and rifleman, and an upgrade doubling the salvage rate.</summary>
    private static SimRules Rules(int wreckTicks = 1200, int harvesterRate = 20, int rifleRate = 30, int max = 1000)
    {
        SimRules r = Loaded.Sim;
        var veh = r.Vehicles.ToArray();
        int h = Veh("test_harvester");
        veh[h] = veh[h] with { SalvageRate = harvesterRate };
        var inf = r.Infantry.ToArray();
        int rf = Inf("test_rifleman");
        inf[rf] = inf[rf] with { SalvageRate = rifleRate };
        var ups = r.Upgrades.ToArray();
        ups[0] = ups[0] with { SalvagePermille = 2000 };
        return r with
        {
            Vehicles = veh, Infantry = inf, Upgrades = ups,
            Economy = r.Economy with { WreckTicks = wreckTicks, WreckMaxValue = max },
        };
    }

    private static Simulation NewGame(SimRules rules) => NewSim(PaintMap(OverlayKind.None, 0), rules);

    private static Command Kill(Simulation sim, EntityId id, ActorKind kind) => Debug(sim, 1, DebugOp.Kill, id, (int)kind);

    /// <summary>Spawns enemy vehicles (type, cell) before the first step, then kills them all in one step.</summary>
    private static void KillAll(Simulation sim, params (int Type, CellCoord Cell)[] victims)
    {
        var cmds = new List<Command>();
        foreach (var (type, cell) in victims) cmds.Add(Kill(sim, sim.SpawnVehicle(1, type, cell), ActorKind.Vehicle));
        sim.Step(cmds.ToArray());
    }

    private static void Kill(Simulation sim, int vehicleType, CellCoord cell) => KillAll(sim, (vehicleType, cell));

    private static Command Order(Simulation sim, CommandKind kind, EntityId who, ActorKind actor, CellCoord cell = default) =>
        new(sim.Tick, 0, (ushort)(100 + sim.Tick), kind, who, default, cell, (int)actor);

    // ---- spawn and value ------------------------------------------------------------------------

    [Fact]
    public void Wrecks_OnlyWhenWreckTicksPositive()
    {
        var off = NewGame(Loaded.Sim);
        Assert.Equal(0, off.Setup.Rules.Economy.WreckTicks);
        Kill(off, Veh("test_tank"), WreckCell);
        Assert.Equal(0, off.Salvage.Count);

        var on = NewGame(Rules());
        Kill(on, Veh("test_tank"), WreckCell);
        Assert.Equal(1, on.Salvage.Count);
        Assert.Equal(1200, on.Salvage.TicksLeft[1]);
        Assert.Equal((WreckCell.X, WreckCell.Y), (on.Salvage.CellX[1], on.Salvage.CellY[1]));
    }

    [Fact]
    public void Value_IsCostPercent_WithFloorAndCap()
    {
        var sim = NewGame(Rules(max: 150));
        KillAll(sim, (Veh("test_tank"), new CellCoord(12, 10)),       // 800 x 25% = 200 -> capped 150
            (Veh("test_jeep"), new CellCoord(13, 10)),                // 400 x 25% = 100
            (Veh("test_crawler"), new CellCoord(14, 10)));            // cost 0 -> floor 25
        Assert.Equal(new[] { 150, 100, 25 }, new[] { sim.Salvage.Value[1], sim.Salvage.Value[2], sim.Salvage.Value[3] });
        Assert.Equal(sim.Salvage.Value[1], sim.Salvage.ValueStart[1]);
    }

    [Fact]
    public void Buildings_LeaveAWreckAtTheFootprintCentre_InfantryDoNot()
    {
        var sim = NewGame(Rules());
        EntityId b = sim.PlaceBuilding(1, Bld("test_refinery"), new CellCoord(20, 20));   // 3x3, cost 1000 -> 250
        EntityId f = sim.SpawnInfantry(1, Inf("test_rifleman"), new CellCoord(25, 25));
        sim.Step(new[] { Kill(sim, f, ActorKind.Infantry) });
        Assert.Equal(0, sim.Salvage.Count);
        sim.Step(new[] { Kill(sim, b, ActorKind.Building) });
        Assert.Equal(1, sim.Salvage.Count);
        Assert.Equal((21, 21, 250), (sim.Salvage.CellX[1], sim.Salvage.CellY[1], sim.Salvage.Value[1]));
    }

    // ---- decay and table ------------------------------------------------------------------------

    [Fact]
    public void Wreck_DecaysLinearly_AndExpires()
    {
        var sim = NewGame(Rules(wreckTicks: 100));
        Kill(sim, Veh("test_tank"), WreckCell);   // 200
        Assert.Equal(200, sim.Salvage.Value[1]);
        Run(sim, 50);
        Assert.Equal((50, 100), (sim.Salvage.TicksLeft[1], sim.Salvage.Value[1]));
        Run(sim, 25);
        Assert.Equal(50, sim.Salvage.Value[1]);
        Run(sim, 25);
        Assert.Equal(0, sim.Salvage.Count);
        Assert.Equal(0, sim.Salvage.Value[1]);
    }

    [Fact]
    public void FullTable_ReplacesTheOldest_LowestSlotOnTies()
    {
        var sim = NewGame(Rules());
        var s = sim.Salvage;
        for (int i = 1; i <= s.Capacity; i++) { s.CellX[i] = 1; s.CellY[i] = 1; s.Value[i] = 100000; s.ValueStart[i] = 100000; s.TicksLeft[i] = 500 + i; }
        s.TicksLeft[9] = 3; s.TicksLeft[7] = 3;
        Kill(sim, Veh("test_tank"), WreckCell);
        Assert.Equal((WreckCell.X, 200, 1200), (s.CellX[7], s.Value[7], s.TicksLeft[7]));
        Assert.Equal(2, s.TicksLeft[9]);   // untouched by the replacement (it only ticked once)
        Assert.Equal(s.Capacity, s.Count);
    }

    // ---- the mission ----------------------------------------------------------------------------

    private static (Simulation Sim, EntityId Hauler) Scene(SimRules rules)
    {
        var sim = NewGame(rules);
        sim.PlaceBuilding(0, Bld("test_refinery"), Refinery);
        EntityId h = sim.SpawnVehicle(0, Harvester, new CellCoord(8, 8));
        KillAll(sim, (Veh("test_tank"), WreckCell));
        return (sim, h);
    }

    [Fact]
    public void Salvage_DrainsAndPays()
    {
        var (sim, h) = Scene(Rules(wreckTicks: Long));
        sim.Step(new[] { Order(sim, CommandKind.Harvest, h, ActorKind.Vehicle, WreckCell) });
        sim.Step(None);
        Assert.Equal(MissionKind.Salvage, MissionOf(sim, h));
        int wreckBefore = sim.Salvage.Value[1];
        RunUntil(sim, () => sim.Salvage.Value[1] < wreckBefore);
        RunUntil(sim, () => sim.Players.Stored[0] >= 199, 4000);
        Assert.Equal(0, sim.Salvage.Count);
        Assert.Equal(0, sim.Economy.Cargo[h.Index]);
    }

    [Fact]
    public void Salvage_DrainRate_IsRatePer10Ticks()
    {
        var (sim, h) = Scene(Rules(wreckTicks: Long));
        sim.Step(new[] { Order(sim, CommandKind.Harvest, h, ActorKind.Vehicle, WreckCell) });
        int i = h.Index;
        RunUntil(sim, () => sim.Economy.CargoValue[i] > 0);      // first drain on arrival: rate 20
        Assert.Equal(20, sim.Economy.CargoValue[i]);
        int t0 = sim.Tick;
        RunUntil(sim, () => sim.Economy.CargoValue[i] > 20);
        Assert.Equal(10, sim.Tick - t0);
        Assert.Equal(40, sim.Economy.CargoValue[i]);
    }

    [Fact]
    public void Upgrade_RaisesTheRate()
    {
        int Finish(bool upgrade)
        {
            var (sim, h) = Scene(Rules(wreckTicks: Long));
            var cmds = new List<Command> { Order(sim, CommandKind.Harvest, h, ActorKind.Vehicle, WreckCell) };
            if (upgrade) cmds.Add(Debug(sim, 0, DebugOp.GrantUpgrade, default, 0));
            sim.Step(cmds.ToArray());
            return RunUntil(sim, () => sim.Salvage.Count == 0, 3000);
        }
        int slow = Finish(false), fast = Finish(true);
        Assert.True(fast < slow, $"{fast} vs {slow}");
    }

    [Fact]
    public void Infantry_IsPaidDirectly()
    {
        var sim = NewGame(Rules(wreckTicks: Long));
        sim.PlaceBuilding(0, Bld("test_silo"), new CellCoord(4, 4));
        EntityId t = sim.SpawnInfantry(0, Inf("test_rifleman"), new CellCoord(10, 10));
        KillAll(sim, (Veh("test_jeep"), WreckCell));   // 100
        sim.Step(new[] { Order(sim, CommandKind.Harvest, t, ActorKind.Infantry, WreckCell) });
        RunUntil(sim, () => sim.Salvage.Count == 0, 2000);
        Assert.InRange(sim.Players.Stored[0], 99, 100);
    }

    [Fact]
    public void HarvestOnANonSalvager_IsUnchanged()
    {
        var (sim, h) = Scene(Rules(wreckTicks: Long, harvesterRate: 0));
        sim.Step(new[] { Order(sim, CommandKind.Harvest, h, ActorKind.Vehicle, WreckCell) });
        sim.Step(None);
        Assert.NotEqual(MissionKind.Salvage, MissionOf(sim, h));
        Run(sim, 200);
        Assert.InRange(sim.Salvage.Value[1], 199, 200);   // only decayed, never drained
        Assert.Equal(0, sim.Players.Stored[0]);
    }

    [Fact]
    public void SalvageHashesMatchAfterSaveLoad_MidSalvage()
    {
        var (sim, h) = Scene(Rules(wreckTicks: Long));
        sim.Step(new[] { Order(sim, CommandKind.Harvest, h, ActorKind.Vehicle, WreckCell) });
        Run(sim, 60);
        Assert.Equal(MissionKind.Salvage, MissionOf(sim, h));
        var ms = new MemoryStream();
        sim.Save(ms);
        var copy = Simulation.Load(new MemoryStream(ms.ToArray()), sim.Setup);
        Assert.Equal(sim.ComputeStateHash(), copy.ComputeStateHash());
        for (int t = 0; t < 400; t++)
        {
            sim.Step(None); copy.Step(None);
            Assert.Equal(sim.ComputeStateHash(), copy.ComputeStateHash());
        }
        Assert.True(sim.Players.Stored[0] > 0);
    }

    [Fact]
    public void SameScript_SameHash()
    {
        ulong Run1()
        {
            var (sim, h) = Scene(Rules(wreckTicks: Long));
            sim.Step(new[] { Order(sim, CommandKind.Harvest, h, ActorKind.Vehicle, WreckCell) });
            Run(sim, 500);
            return sim.ComputeStateHash();
        }
        Assert.Equal(Run1(), Run1());
    }
}
