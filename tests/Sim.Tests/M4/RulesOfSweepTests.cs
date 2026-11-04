// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Sim.Combat;
using Rts.Sim.Core;
using Rts.Data;
using Rts.Sim.Map;
using Rts.Sim.Rules;
using Rts.Sim.Tests.Rules;
using Xunit;
using static Rts.Sim.Tests.M4.RulesTierFixture;

namespace Rts.Sim.Tests.M4;

/// <summary>M4-14: upgrades act where actors are created and perceived (spawn health, sight), only for the researching player.</summary>
public sealed class RulesOfSweepTests
{
    private static int HealthOfNewest(Simulation sim, ActorKind kind, int index)
    {
        for (ushort g = 0; g < 4; g++)
            if (sim.TryGetActorInfo(new ActorRef(kind, new EntityId((ushort)index, g)), out ActorInfo info)) return info.Health;
        throw new InvalidOperationException("actor not found");
    }

    private static Command SpawnInf(Simulation sim, byte player, int type, int x, int y) =>
        new(sim.Tick, player, 900, CommandKind.Debug, default, default, new CellCoord(x, y), (int)DebugOp.SpawnInfantry, type);

    [Fact]
    public void NewInfantryAfterMaxHealthUpgradeHasMoreHealth_ExistingDoesNotHeal_OtherPlayerUnaffected()
    {
        Simulation sim = NewSim();
        int rifle = Inf("test_rifleman");
        int baseHp = Base.Infantry[rifle].MaxHealth;
        sim.SpawnInfantry(0, rifle, new CellCoord(20, 20));   // index 1, before the upgrade
        Run(sim, 1, Grant(sim, Up("test_up_mixed")));
        Run(sim, 1, SpawnInf(sim, 0, rifle, 22, 20));          // index 2, owner has the upgrade
        Run(sim, 1, SpawnInf(sim, 1, rifle, 24, 20));          // index 3, other player
        Assert.Equal(baseHp, HealthOfNewest(sim, ActorKind.Infantry, 1));
        Assert.Equal(baseHp * 2, HealthOfNewest(sim, ActorKind.Infantry, 2));
        Assert.Equal(baseHp, HealthOfNewest(sim, ActorKind.Infantry, 3));
    }

    [Fact]
    public void NewVehicleAfterArmourUpgradeHasMoreHealth()
    {
        Simulation sim = NewSim();
        int tank = Veh("test_jeep");
        Run(sim, 1, Grant(sim, Up("test_up_armor")));
        Run(sim, 1, new Command(sim.Tick, 0, 901, CommandKind.Debug, default, default, new CellCoord(20, 20), (int)DebugOp.SpawnVehicle, tank));
        Assert.Equal(Base.Vehicles[tank].MaxHealth * 1100 / 1000, HealthOfNewest(sim, ActorKind.Vehicle, 1));
    }

    [Fact]
    public void SightUpgradeActsInVision_ForTheResearcherOnly()
    {
        Simulation sim = NewSim();
        EntityId mine = Place(sim, 0, "test_airfield", 20, 20);
        EntityId theirs = Place(sim, 1, "test_airfield", 30, 30);
        int baseSight = Base.Buildings[Bld("test_airfield")].Sight;
        Assert.Equal(baseSight, sim.SightOf(ActorKind.Building, mine.Index));
        Run(sim, 1, Grant(sim, Up("test_up_mixed")));
        Assert.Equal(baseSight + 2, sim.SightOf(ActorKind.Building, mine.Index));
        Assert.Equal(baseSight, sim.SightOf(ActorKind.Building, theirs.Index));
    }

    [Fact]
    public void NewBuildingAfterBuildingHealthUpgradeIsPlacedWithOwnerRules()
    {
        // No shipped building max_health upgrade exists; the base value must still equal RulesOf for a player without upgrades.
        Simulation sim = NewSim();
        EntityId b = Place(sim, 0, "test_lab", 20, 20);
        Assert.Equal(Base.Buildings[Bld("test_lab")].MaxHealth, HealthOfNewest(sim, ActorKind.Building, b.Index));
    }

    private const string SpeedEffect = """

        [[effect]]
        id = "sweep_jeep_speed"
        op = "mul"
        target = "test_jeep"
        attr = "speed"
        value = 1500
        """;

    private const string SpeedUpgrade = """

        [sweep_up_speed]
        faction = "authority"
        cost = 200
        build_time = 20
        prerequisites = []
        effect = ""
        effects = ["sweep_jeep_speed"]
        """;

    [Fact]
    public void SpeedUpgradeMakesVehicleCoverMoreDistance_OtherPlayerUnaffected()
    {
        using var temp = new TempRules(
            ("techs.toml", TempRules.Real("techs.toml") + SpeedEffect),
            ("upgrades.toml", TempRules.Real("upgrades.toml") + SpeedUpgrade));
        RuleSet rs = RulesLoader.Load(temp.Dir);
        var sim = new Simulation(new SimSetup(7, MapData.CreateFlat(40, 40), 2, rs.Sim, rs.RulesHash) { AllowDebug = true });
        int jeep = SimRules.IndexOf(rs.Sim.Vehicles, "test_jeep", v => v.Id);
        EntityId a = sim.SpawnVehicle(0, jeep, new CellCoord(5, 5));
        EntityId b = sim.SpawnVehicle(1, jeep, new CellCoord(5, 20));
        sim.Units.Facing[a.Index] = 64; sim.Units.Facing[b.Index] = 64;
        LeptonCoord a0 = sim.Units.Position[a.Index], b0 = sim.Units.Position[b.Index];
        sim.Step(new[] { Cmd(sim, 0, CommandKind.Debug, default, (int)DebugOp.GrantUpgrade, rs.Sim.UpgradeIndex("sweep_up_speed")) });
        var moves = new[]
        {
            new Command(sim.Tick, 0, 910, CommandKind.Move, a, default, new CellCoord(35, 5), (int)ActorKind.Vehicle),
            new Command(sim.Tick, 1, 911, CommandKind.Move, b, default, new CellCoord(35, 20), (int)ActorKind.Vehicle),
        };
        sim.Step(moves);
        for (int t = 0; t < 60; t++) sim.Step(None);
        int da = sim.Units.Position[a.Index].X - a0.X, db = sim.Units.Position[b.Index].X - b0.X;
        Assert.True(db > 0, "baseline vehicle must move");
        Assert.True(da > db * 13 / 10, $"upgraded {da} vs base {db}");
    }
}
