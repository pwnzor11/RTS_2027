// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Sim.Core;
using Rts.Sim.Missions;
using Rts.Sim.Rules;
using Rts.Sim.Tests.Determinism;

namespace Rts.Sim.Tests.M6;

/// <summary>M6-05: raiders plunder an enemy silo/refinery, withdraw (or are consumed), and earn spoils for harvester kills.</summary>
public sealed class RaidTests
{
    // vehicle types: 0 raid truck (raider), 1 harvester; infantry: 0 raider, 1 one-use raider, 2 plain; buildings: 0 silo, 1 refinery, 2 hq
    private const int Truck = 0, Harv = 1, Raider = 0, OneUse = 1, Plain = 2, Silo = 0, Refinery = 1, Hq = 2;
    private static readonly CellCoord SiloCell = new(30, 10), HqCell = new(10, 30);

    private static SimSetup Setup(int plunderCap = 1000, int spoils = 250, bool neutral = false)
    {
        var vehicles = new[]
        {
            ScriptedGame.Vehicle("a_truck", 37, 100) with { Raider = true },
            ScriptedGame.Vehicle("b_harv", 11, 200) with { HarvestCapacity = 20, Cost = 800 },
        };
        var infantry = new[]
        {
            ScriptedGame.Infantry("a_raider", 20, 50) with { Raider = true },
            ScriptedGame.Infantry("b_oneuse", 20, 50) with { Raider = true, RaiderOneUse = true },
            ScriptedGame.Infantry("c_plain", 20, 50),
        };
        var buildings = new[]
        {
            ScriptedGame.Building("a_silo", 2, 2, 300) with { Storage = 1500 },
            ScriptedGame.Building("b_refinery", 3, 3, 500) with { Storage = 2000, Refinery = true },
            ScriptedGame.Building("c_hq", 3, 3, 500),
        };
        var rules = ScriptedGame.RulesWith(vehicles, infantry) with
        {
            Buildings = buildings,
            Economy = EconomyRules.Default with { PlunderCap = plunderCap, SpoilsPermille = spoils },
            Upgrades = new[] { new UpgradeType("raid_up", Faction.Authority, 0, 0, Array.Empty<string>(), "none") { PlunderPermille = 1500 } },
        };
        return new SimSetup(1, Rts.Sim.Map.MapData.CreateFlat(64, 64), neutral ? 3 : 2, rules, 0x1234UL)
        {
            AllowDebug = true,
            Teams = neutral ? new byte[] { 0, 1, Teams.Neutral } : null,
        };
    }

    private static Simulation NewSim(SimSetup setup, Action<Simulation>? extra = null)
    {
        var sim = new Simulation(setup);
        sim.PlaceBuilding(0, Hq, HqCell);
        sim.PlaceBuilding(1, Silo, SiloCell);
        extra?.Invoke(sim);
        return sim;
    }

    private static void Run(Simulation sim, int ticks, params Command[] first)
    {
        sim.Step(first);
        for (int t = 1; t < ticks; t++) sim.Step(Array.Empty<Command>());
    }

    private static bool RunUntil(Simulation sim, Func<bool> done, int max = 1500)
    {
        for (int t = 0; t < max && !done(); t++) sim.Step(Array.Empty<Command>());
        return done();
    }

    private static Command Enter(Simulation sim, byte player, ActorKind kind, EntityId subject, EntityId target) =>
        new(sim.Tick, player, 1, CommandKind.Enter, subject, target, default, (int)kind | ((int)ActorKind.Building << 8));

    private static EntityId SiloId(Simulation sim) => sim.Map.BuildingAt(SiloCell);

    private static (Simulation Sim, EntityId Unit, int Cash0) Scenario(SimSetup setup, int stored, bool upgrade, int type, bool vehicle = false)
    {
        EntityId unit = default;
        var sim = NewSim(setup, s =>
        {
            unit = vehicle ? s.SpawnVehicle(0, type, new CellCoord(24, 10)) : s.SpawnInfantry(0, type, new CellCoord(24, 10));
        });
        sim.Players.Stored[1] = stored;
        if (upgrade) sim.Players.Upgrades[0] |= 1UL;
        return (sim, unit, sim.Players.Cash[0]);
    }

    [Fact]
    public void Plunder_TakesPercentOfStored()
    {
        var (sim, unit, cash) = Scenario(Setup(), 1000, false, Raider);
        sim.Step(new[] { Enter(sim, 0, ActorKind.Infantry, unit, SiloId(sim)) });
        Assert.True(RunUntil(sim, () => sim.Players.Cash[0] > cash));
        Assert.Equal(200, sim.Players.Cash[0] - cash);
        Assert.Equal(800, sim.Players.Stored[1]);
        Assert.Equal(200, sim.StatsOf(0).CreditsHarvested);
    }

    [Fact]
    public void Plunder_RespectsCap()
    {
        var (sim, unit, cash) = Scenario(Setup(plunderCap: 50), 1000, false, Raider);
        sim.Step(new[] { Enter(sim, 0, ActorKind.Infantry, unit, SiloId(sim)) });
        Assert.True(RunUntil(sim, () => sim.Players.Cash[0] > cash));
        Assert.Equal(50, sim.Players.Cash[0] - cash);
        Assert.Equal(950, sim.Players.Stored[1]);
    }

    [Fact]
    public void Plunder_UpgradeScalesTheYield_ThenCapApplies()
    {
        var (sim, unit, cash) = Scenario(Setup(), 1000, true, Raider);
        sim.Step(new[] { Enter(sim, 0, ActorKind.Infantry, unit, SiloId(sim)) });
        Assert.True(RunUntil(sim, () => sim.Players.Cash[0] > cash));
        Assert.Equal(300, sim.Players.Cash[0] - cash);
        var (sim2, unit2, cash2) = Scenario(Setup(plunderCap: 250), 1000, true, Raider);
        sim2.Step(new[] { Enter(sim2, 0, ActorKind.Infantry, unit2, SiloId(sim2)) });
        Assert.True(RunUntil(sim2, () => sim2.Players.Cash[0] > cash2));
        Assert.Equal(250, sim2.Players.Cash[0] - cash2);
    }

    [Fact]
    public void Plunder_ByVehicleRaider_AtARefinery()
    {
        EntityId truck = default;
        var sim = NewSim(Setup(), s =>
        {
            truck = s.SpawnVehicle(0, Truck, new CellCoord(24, 10));
            s.PlaceBuilding(1, Refinery, new CellCoord(40, 40));
        });
        sim.Players.Stored[1] = 500;
        int cash = sim.Players.Cash[0];
        sim.Step(new[] { Enter(sim, 0, ActorKind.Vehicle, truck, sim.Map.BuildingAt(new CellCoord(40, 40))) });
        Assert.True(RunUntil(sim, () => sim.Players.Cash[0] > cash, 3000));
        Assert.Equal(100, sim.Players.Cash[0] - cash);
    }

    [Fact]
    public void OneUseRaider_IsConsumed()
    {
        var (sim, unit, cash) = Scenario(Setup(), 1000, false, OneUse);
        sim.Step(new[] { Enter(sim, 0, ActorKind.Infantry, unit, SiloId(sim)) });
        Assert.True(RunUntil(sim, () => sim.Players.Cash[0] > cash));
        Assert.False(sim.Infantry.Slots.IsAlive(unit));
    }

    [Fact]
    public void Raider_WithdrawsHome_AndClearsTheFlag()
    {
        var (sim, unit, cash) = Scenario(Setup(), 1000, false, Raider);
        sim.Step(new[] { Enter(sim, 0, ActorKind.Infantry, unit, SiloId(sim)) });
        Assert.True(RunUntil(sim, () => sim.Players.Cash[0] > cash));
        Assert.True(RunUntil(sim, () => sim.Infantry.Mission[unit.Index] == MissionKind.Withdraw, 5));
        Assert.Equal(1, sim.Effects.InfantryWithdraw[unit.Index]);
        Assert.True(RunUntil(sim, () => sim.Infantry.Mission[unit.Index] == MissionKind.Guard, 700));
        Assert.Equal(0, sim.Effects.InfantryWithdraw[unit.Index]);
        CellCoord at = sim.Infantry.Position[unit.Index].ToCell();
        Assert.True(Math.Max(Math.Abs(at.X - HqCell.X), Math.Abs(at.Y - HqCell.Y)) <= 4, $"ended at {at}");
    }

    [Fact]
    public void FriendlyAndNeutralTargets_FallThroughToNormalEnter()
    {
        var setup = Setup(neutral: true);
        EntityId unit = default;
        var sim = NewSim(setup, s =>
        {
            unit = s.SpawnInfantry(0, Raider, new CellCoord(24, 10));
            s.PlaceBuilding(0, Silo, new CellCoord(20, 20));
            s.PlaceBuilding(2, Silo, new CellCoord(40, 20));
        });
        sim.Players.Stored[0] = 1000; sim.Players.Stored[2] = 1000;
        int cash = sim.Players.Cash[0];
        foreach (var cell in new[] { new CellCoord(20, 20), new CellCoord(40, 20) })
        {
            sim.Step(new[] { Enter(sim, 0, ActorKind.Infantry, unit, sim.Map.BuildingAt(cell)) });
            Assert.Equal(MissionKind.None, sim.Infantry.NextMission[unit.Index]);
            Assert.NotEqual(MissionKind.Capture, sim.Infantry.Mission[unit.Index]);
        }
        Run(sim, 100);
        Assert.Equal(cash, sim.Players.Cash[0]);
        Assert.Equal(1000, sim.Players.Stored[2]);
    }

    [Fact]
    public void Spoils_OnlyForHarvesterKills()
    {
        EntityId harv = default, truck = default, own = default;
        var sim = NewSim(Setup(), s =>
        {
            harv = s.SpawnVehicle(1, Harv, new CellCoord(40, 40));
            truck = s.SpawnVehicle(1, Truck, new CellCoord(42, 40));
            own = s.SpawnVehicle(0, Harv, new CellCoord(44, 40));
        });
        int cash = sim.Players.Cash[0];
        sim.OnSpoilsLethalHit(new ActorRef(ActorKind.Vehicle, truck), 0);
        sim.OnSpoilsLethalHit(new ActorRef(ActorKind.Vehicle, own), 0);   // friendly fire
        Assert.Equal(cash, sim.Players.Cash[0]);
        sim.OnSpoilsLethalHit(new ActorRef(ActorKind.Vehicle, harv), 0);
        Assert.Equal(cash + 200, sim.Players.Cash[0]);   // 800 * 250 / 1000
        var off = NewSim(Setup(spoils: 0), s => harv = s.SpawnVehicle(1, Harv, new CellCoord(40, 40)));
        int c2 = off.Players.Cash[0];
        off.OnSpoilsLethalHit(new ActorRef(ActorKind.Vehicle, harv), 0);
        Assert.Equal(c2, off.Players.Cash[0]);
    }

    [Fact]
    public void SaveLoad_MidWithdraw_ReplaysToTheSameHash()
    {
        var setup = Setup();
        var (sim, unit, cash) = Scenario(setup, 1000, false, Raider);
        sim.Step(new[] { Enter(sim, 0, ActorKind.Infantry, unit, SiloId(sim)) });
        Assert.True(RunUntil(sim, () => sim.Infantry.Mission[unit.Index] == MissionKind.Withdraw));
        Run(sim, 20);
        var ms = new MemoryStream();
        sim.Save(ms);
        var loaded = Simulation.Load(new MemoryStream(ms.ToArray()), setup);
        Assert.Equal(sim.ComputeStateHash(), loaded.ComputeStateHash());
        Run(sim, 300); Run(loaded, 300);
        Assert.Equal(sim.ComputeStateHash(), loaded.ComputeStateHash());
        Assert.Equal(MissionKind.Guard, loaded.Infantry.Mission[unit.Index]);
    }

    [Fact]
    public void SameScript_SameHash()
    {
        ulong Go()
        {
            var (sim, unit, _) = Scenario(Setup(), 1000, true, Raider);
            Run(sim, 400, Enter(sim, 0, ActorKind.Infantry, unit, SiloId(sim)));
            return sim.ComputeStateHash();
        }
        Assert.Equal(Go(), Go());
    }
}
