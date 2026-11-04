// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Sim.Core;
using Rts.Sim.Rules;
using Rts.Sim.Tests.Combat;
using Rts.Sim.Tests.Economy;
using Rts.Sim.Tests.Production;
using static Rts.Sim.Tests.Combat.CombatFixture;

namespace Rts.Sim.Tests.M4;

/// <summary>M4-06 player statistics counters (design 08 §6 "stats"): built, lost, kills, harvested and spent, and their save round-trip.</summary>
public sealed class StatsTests
{
    private static ushort _seq = 9100;

    private static void Step(Simulation sim, params Command[] cmds) => sim.Step(cmds);

    private static void Until(Simulation sim, Func<bool> done, int max = 4000)
    {
        for (int t = 0; t < max; t++)
        {
            sim.Step(None);
            if (done()) return;
        }
        Assert.Fail($"condition not reached within {max} ticks (tick {sim.Tick})");
    }

    private static Command Kill(Simulation sim, ActorKind kind, EntityId who) =>
        new(sim.Tick, 0, _seq++, CommandKind.Debug, who, default, default, (int)DebugOp.Kill, (int)kind);

    private static Simulation Roundtrip(Simulation sim)
    {
        var ms = new MemoryStream();
        sim.Save(ms);
        ms.Position = 0;
        return Simulation.Load(ms, sim.Setup);
    }

    private static int AliveInfantry(Simulation sim, byte player)
    {
        int n = 0;
        for (int i = 1; i < sim.Infantry.Capacity; i++)
            if (sim.Infantry.Slots.IsIndexAlive(i) && sim.Infantry.Player[i] == player) n++;
        return n;
    }

    private static int AliveVehicles(Simulation sim, byte player)
    {
        int n = 0;
        for (int i = 1; i < sim.Units.Capacity; i++)
            if (sim.Units.Slots.IsIndexAlive(i) && sim.Units.Player[i] == player) n++;
        return n;
    }

    /// <summary>A production sim: player 0 has the usual base, debug placement on, and cash.</summary>
    private static Simulation BaseSim(bool debug)
    {
        var sim = ProductionFixture.NewSim(new[] { ("test_hq", 4, 4), ("test_barracks", 8, 4), ("test_factory", 4, 8),
            ("test_power", 12, 4) }, debug: debug);
        sim.Players.Cash[0] = 10000;
        return sim;
    }

    [Fact]
    public void FreshSim_AllCountersAreZero()
    {
        Simulation sim = NewSim();
        Assert.Equal(default, sim.StatsOf(0));
        Assert.Equal(default, sim.StatsOf(1));
    }

    [Fact]
    public void Fight_KillsAndLossesMatchTheLethalHits()
    {
        Simulation sim = NewSim();
        EntityId g0 = sim.SpawnVehicle(0, Veh("test_tank"), new CellCoord(4, 4));
        EntityId e0 = sim.SpawnVehicle(1, Veh("test_tank"), new CellCoord(6, 4));
        EntityId e1 = sim.SpawnVehicle(1, Veh("test_tank"), new CellCoord(6, 8));
        EntityId g1 = sim.SpawnVehicle(0, Veh("test_tank"), new CellCoord(4, 8));
        sim.Units.Health[e0.Index] = 1;   // one shot is lethal
        sim.Units.Health[g1.Index] = 1;

        // Player 0 kills player 1's tank.
        Step(sim, Attack(sim, 0, ActorKind.Vehicle, g0, ActorKind.Vehicle, e0));
        Until(sim, () => !sim.Units.Slots.IsAlive(e0));
        Assert.Equal(1, sim.StatsOf(0).Kills);
        Assert.Equal(0, sim.StatsOf(0).UnitsLost);
        Assert.Equal(1, sim.StatsOf(1).UnitsLost);
        Assert.Equal(0, sim.StatsOf(1).Kills);

        // Player 1 kills player 0's tank.
        Step(sim, Attack(sim, 1, ActorKind.Vehicle, e1, ActorKind.Vehicle, g1));
        Until(sim, () => !sim.Units.Slots.IsAlive(g1));
        Assert.Equal(1, sim.StatsOf(1).Kills);
        Assert.Equal(1, sim.StatsOf(0).UnitsLost);
        Assert.Equal(1, sim.StatsOf(0).Kills);
        Assert.Equal(0, sim.StatsOf(0).UnitsBuilt);   // debug spawns are not production
    }

    [Fact]
    public void Fight_StructureKillCountsAsKillAndStructureLoss()
    {
        Simulation sim = NewSim();
        EntityId g = sim.SpawnVehicle(0, Veh("test_tank"), new CellCoord(4, 4));
        EntityId hq = sim.PlaceBuilding(1, Bld("test_hq"), new CellCoord(12, 12));
        sim.Buildings.Health[hq.Index] = 1;

        Step(sim, Attack(sim, 0, ActorKind.Vehicle, g, ActorKind.Building, hq));
        Until(sim, () => !sim.Buildings.Slots.IsAlive(hq));
        Assert.Equal(1, sim.StatsOf(0).Kills);
        Assert.Equal(1, sim.StatsOf(1).StructuresLost);
        Assert.Equal(0, sim.StatsOf(1).UnitsLost);
        Assert.Equal(0, sim.StatsOf(1).StructuresBuilt);   // PlaceBuilding is setup, not a Place command
    }

    [Fact]
    public void DebugKill_IsALossButNotAKill()
    {
        Simulation sim = NewSim();
        EntityId u = sim.SpawnVehicle(1, Veh("test_tank"), new CellCoord(6, 6));
        Step(sim, Kill(sim, ActorKind.Vehicle, u));
        Assert.False(sim.Units.Slots.IsAlive(u));
        Assert.Equal(1, sim.StatsOf(1).UnitsLost);
        Assert.Equal(0, sim.StatsOf(0).Kills);
    }

    [Fact]
    public void Production_CountsBuiltUnitsAndPlacedStructures()
    {
        Simulation sim = BaseSim(debug: true);
        Step(sim, ProductionFixture.Build(sim, ProductionCategory.Infantry, Inf("test_rifleman")),
            ProductionFixture.Build(sim, ProductionCategory.Vehicle, Veh("test_jeep")));
        Until(sim, () => AliveInfantry(sim, 0) == 1 && AliveVehicles(sim, 0) == 1);
        Step(sim, ProductionFixture.Place(sim, Bld("test_refinery"), 20, 20));

        PlayerStats s = sim.StatsOf(0);
        // Rifleman + jeep + the refinery's free harvester (a produced spawn, so it counts); none were lost.
        Assert.Equal(AliveInfantry(sim, 0) + AliveVehicles(sim, 0), s.UnitsBuilt);
        Assert.Equal(3, s.UnitsBuilt);
        Assert.Equal(1, s.StructuresBuilt);   // the refinery; the setup buildings were not placed by command
        Assert.Equal(0, s.UnitsLost);
        Assert.Equal(0, s.StructuresLost);
    }

    [Fact]
    public void Harvest_CreditsHarvestedEqualsStoredDelta()
    {
        var (sim, _) = EconomyFixture.Basic();
        int storedBefore = sim.Players.Stored[0];
        Until(sim, () => sim.Players.Stored[0] >= storedBefore + 20 * 25, 3000);
        Assert.Equal(sim.Players.Stored[0] - storedBefore, sim.StatsOf(0).CreditsHarvested);
        Assert.Equal(0, sim.StatsOf(0).CreditsSpent);
    }

    [Fact]
    public void Spend_CreditsSpentEqualsCashAndStoredDrop()
    {
        Simulation sim = BaseSim(debug: false);
        int startTotal = sim.Players.Cash[0] + sim.Players.Stored[0];
        Step(sim, ProductionFixture.Build(sim, ProductionCategory.Infantry, Inf("test_rifleman")),
            ProductionFixture.Build(sim, ProductionCategory.Vehicle, Veh("test_jeep")));
        Until(sim, () => AliveInfantry(sim, 0) == 1 && AliveVehicles(sim, 0) == 1);

        int endTotal = sim.Players.Cash[0] + sim.Players.Stored[0];
        Assert.True(sim.StatsOf(0).CreditsSpent > 0);
        Assert.Equal(startTotal - endTotal, sim.StatsOf(0).CreditsSpent);
        Assert.Equal(0, sim.StatsOf(0).CreditsHarvested);
    }

    [Fact]
    public void SaveLoad_KeepsCountersAndHash_AndContinuesIdentically()
    {
        Simulation sim = NewSim();
        EntityId g0 = sim.SpawnVehicle(0, Veh("test_tank"), new CellCoord(4, 4));
        EntityId e0 = sim.SpawnVehicle(1, Veh("test_tank"), new CellCoord(6, 4));
        sim.Units.Health[e0.Index] = 1;
        Step(sim, Attack(sim, 0, ActorKind.Vehicle, g0, ActorKind.Vehicle, e0));
        Until(sim, () => !sim.Units.Slots.IsAlive(e0));

        Simulation loaded = Roundtrip(sim);
        Assert.Equal(sim.StatsOf(0), loaded.StatsOf(0));
        Assert.Equal(sim.StatsOf(1), loaded.StatsOf(1));
        Assert.Equal(1, loaded.StatsOf(0).Kills);
        Assert.Equal(sim.ComputeStateHash(), loaded.ComputeStateHash());

        for (int t = 0; t < 100; t++) { sim.Step(None); loaded.Step(None); }
        Assert.Equal(sim.ComputeStateHash(), loaded.ComputeStateHash());
        Assert.Equal(sim.StatsOf(0), loaded.StatsOf(0));
    }
}
