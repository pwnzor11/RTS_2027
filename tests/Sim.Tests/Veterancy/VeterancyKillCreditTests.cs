// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Sim.Core;
using Rts.Sim.Map;
using Rts.Sim.Rules;
using Rts.Sim.Tests.Boundary;
using Rts.Sim.Tests.M4;
using static Rts.Sim.Tests.Combat.CombatFixture;

namespace Rts.Sim.Tests.Veterancy;

/// <summary>VET-2 (docs/design/14_veterancy.md §2.4): the shooter travels with the shot and the lethal hit credits kill value to it.</summary>
public sealed class VeterancyKillCreditTests
{
    internal static VeterancyRules On => new() { Enabled = true };

    private static Simulation NewVetSim(bool enabled = true, ulong seed = 1) =>
        new(new SimSetup(seed, MapData.CreateFlat(32, 32), 2, enabled ? Loaded.Sim with { Veterancy = On } : Loaded.Sim, Loaded.RulesHash) { AllowDebug = true });

    private static int Wh(string id) => SimRules.IndexOf(Loaded.Sim.Warheads, id, w => w.Id);

    [Fact]
    public void InstantKill_CreditsTheVictimsCost()
    {
        var sim = NewVetSim();
        var rifle = sim.SpawnInfantry(0, Inf("test_rifleman"), new CellCoord(10, 10));
        var foe = sim.SpawnInfantry(1, Inf("test_engineer"), new CellCoord(13, 10));   // cost 500
        sim.Infantry.Health[foe.Index] = 1;
        Run(sim, 40, Attack(sim, 0, ActorKind.Infantry, rifle, ActorKind.Infantry, foe));
        Assert.False(sim.Infantry.Slots.IsAlive(foe));
        Assert.Equal(500, sim.Veterancy.InfantryKillValue[rifle.Index]);
        Assert.Equal(0, sim.Veterancy.VehicleKillValue.Sum());
        Assert.Equal(1, sim.Stats.Kills[0]);   // the per-player kill counter is unchanged by the new credit
    }

    [Fact]
    public void ShellKill_CreditsTheShooterAfterTheFlight_AndTheShellCarriesIt()
    {
        var sim = NewVetSim();
        var tank = sim.SpawnVehicle(0, Veh("test_tank"), new CellCoord(10, 10));
        var jeep = sim.SpawnVehicle(1, Veh("test_jeep"), new CellCoord(14, 10));   // cost 400
        sim.Units.Health[jeep.Index] = 1;
        Run(sim, 1, Attack(sim, 0, ActorKind.Vehicle, tank, ActorKind.Vehicle, jeep));
        bool seen = false;
        for (int t = 0; t < 80 && sim.Units.Slots.IsAlive(jeep); t++)
        {
            sim.Step(None);
            for (int p = 1; p < sim.Projectiles.Capacity; p++)
                if (sim.Projectiles.Slots.IsIndexAlive(p))
                {
                    Assert.Equal(new ActorRef(ActorKind.Vehicle, tank), sim.Projectiles.Shooter[p]);
                    seen = true;
                }
        }
        Assert.True(seen, "a shell should have been in flight");
        Assert.False(sim.Units.Slots.IsAlive(jeep));
        Assert.Equal(400, sim.Veterancy.VehicleKillValue[tank.Index]);
    }

    [Fact]
    public void BuildingKill_PaysTheBuildingPermille()
    {
        var sim = NewVetSim();
        var tank = sim.SpawnVehicle(0, Veh("test_tank"), new CellCoord(10, 10));
        var barracks = sim.PlaceBuilding(1, Bld("test_barracks"), new CellCoord(14, 10));   // cost 300, 500 permille
        sim.Buildings.Health[barracks.Index] = 1;
        Run(sim, 80, Attack(sim, 0, ActorKind.Vehicle, tank, ActorKind.Building, barracks));
        Assert.False(sim.Buildings.Slots.IsAlive(barracks));
        Assert.Equal(150, sim.Veterancy.VehicleKillValue[tank.Index]);
    }

    [Fact]
    public void Disabled_NothingIsCredited_AndNoShooterIsStored()
    {
        var sim = NewVetSim(enabled: false);
        var tank = sim.SpawnVehicle(0, Veh("test_tank"), new CellCoord(10, 10));
        var jeep = sim.SpawnVehicle(1, Veh("test_jeep"), new CellCoord(14, 10));
        sim.Units.Health[jeep.Index] = 1;
        Run(sim, 1, Attack(sim, 0, ActorKind.Vehicle, tank, ActorKind.Vehicle, jeep));
        for (int t = 0; t < 80 && sim.Units.Slots.IsAlive(jeep); t++)
        {
            sim.Step(None);
            for (int p = 1; p < sim.Projectiles.Capacity; p++)
                if (sim.Projectiles.Slots.IsIndexAlive(p)) Assert.True(sim.Projectiles.Shooter[p].Id.IsNone);
        }
        Assert.False(sim.Units.Slots.IsAlive(jeep));
        Assert.Equal(0, sim.Veterancy.VehicleKillValue.Sum() + sim.Veterancy.InfantryKillValue.Sum());
        Assert.Equal(1, sim.Stats.Kills[0]);
    }

    [Fact]
    public void FriendlyFire_EarnsNothing()
    {
        var sim = NewVetSim();
        var truck = sim.SpawnVehicle(0, Veh("test_artillery_truck"), new CellCoord(10, 10));
        var target = sim.SpawnVehicle(1, Veh("test_tank"), new CellCoord(18, 10));
        var ally = sim.SpawnVehicle(0, Veh("test_jeep"), new CellCoord(18, 11));   // in the splash, hits_allies
        sim.Units.Health[ally.Index] = 1;
        Run(sim, 120, Attack(sim, 0, ActorKind.Vehicle, truck, ActorKind.Vehicle, target));
        Assert.False(sim.Units.Slots.IsAlive(ally));
        Assert.Equal(0, sim.Veterancy.VehicleKillValue[truck.Index]);
    }

    [Fact]
    public void SplashKill_CreditsTheShooterOncePerVictim()
    {
        var sim = NewVetSim();
        var truck = sim.SpawnVehicle(0, Veh("test_artillery_truck"), new CellCoord(10, 10));
        var target = sim.SpawnVehicle(1, Veh("test_tank"), new CellCoord(18, 10));     // 800
        var neighbour = sim.SpawnVehicle(1, Veh("test_jeep"), new CellCoord(19, 10));  // 400
        sim.Units.Health[target.Index] = 1; sim.Units.Health[neighbour.Index] = 1;
        Run(sim, 120, Attack(sim, 0, ActorKind.Vehicle, truck, ActorKind.Vehicle, target));
        Assert.False(sim.Units.Slots.IsAlive(target));
        Assert.False(sim.Units.Slots.IsAlive(neighbour));
        Assert.Equal(1200, sim.Veterancy.VehicleKillValue[truck.Index]);
    }

    [Fact]
    public void HitsWithoutAShooter_NeverCredit()
    {
        var sim = NewVetSim();
        var tank = sim.SpawnVehicle(0, Veh("test_tank"), new CellCoord(10, 10));
        var foe = sim.SpawnVehicle(1, Veh("test_jeep"), new CellCoord(14, 10));
        sim.Detonate(0, 5000, Wh("test_ap"), sim.Units.Position[foe.Index], new ActorRef(ActorKind.Vehicle, foe), true);   // like a death weapon or superweapon
        sim.Step(None);
        Assert.False(sim.Units.Slots.IsAlive(foe));
        Assert.Equal(0, sim.Veterancy.VehicleKillValue.Sum() + sim.Veterancy.InfantryKillValue.Sum());
        Assert.True(sim.Units.Slots.IsAlive(tank));
    }

    [Fact]
    public void ShooterGone_AndItsSlotReused_EarnsNothing()
    {
        var sim = NewVetSim();
        var tank = sim.SpawnVehicle(0, Veh("test_tank"), new CellCoord(10, 10));
        var jeep = sim.SpawnVehicle(1, Veh("test_jeep"), new CellCoord(15, 10));
        sim.Units.Health[jeep.Index] = 1;
        Run(sim, 1, Attack(sim, 0, ActorKind.Vehicle, tank, ActorKind.Vehicle, jeep));
        for (int t = 0; t < 40 && sim.Projectiles.Slots.Count == 0; t++) sim.Step(None);
        Assert.Equal(1, sim.Projectiles.Slots.Count);
        Assert.True(sim.Units.Slots.IsAlive(jeep));

        sim.Units.Health[tank.Index] = 0;   // the shooter dies with the shell in the air
        sim.Step(None);
        Assert.False(sim.Units.Slots.IsAlive(tank));
        EntityId fresh = LateSpawn.Vehicle(sim, 0, Veh("test_tank"), new CellCoord(3, 3));
        Assert.Equal(tank.Index, fresh.Index);          // the slot is reused
        Assert.NotEqual(tank.Generation, fresh.Generation);

        for (int t = 0; t < 40 && sim.Units.Slots.IsAlive(jeep); t++) sim.Step(None);
        Assert.False(sim.Units.Slots.IsAlive(jeep));    // the shell still killed the jeep
        Assert.Equal(0, sim.Veterancy.VehicleKillValue[fresh.Index]);
        Assert.Equal(0, sim.Veterancy.VehicleKillValue.Sum());
    }

    [Fact]
    public void GarrisonedOccupant_EarnsItsOwn_AndKeepsItWhenItLeaves()
    {
        SimRules rules = GarrisonFixture.MakeRules();
        rules = rules with
        {
            Veterancy = On,
            Vehicles = rules.Vehicles.Select(v => v with { Cost = 300 }).ToArray(),
            Infantry = rules.Infantry.Select(i => i with { Cost = 100 }).ToArray(),
        };
        var sim = new Simulation(new SimSetup(1, GarrisonFixture.MakeMap(), 2, rules, 0x4303UL) { AllowDebug = true });
        EntityId bunker = sim.PlaceBuilding(0, GarrisonFixture.Bunker, new CellCoord(10, 10));
        EntityId rifle = sim.SpawnInfantry(0, GarrisonFixture.Rifle, new CellCoord(12, 10));
        EntityId target = sim.SpawnVehicle(1, GarrisonFixture.Dummy, new CellCoord(15, 10));
        sim.Units.Health[target.Index] = 10;   // one hit of the gun
        GarrisonFixture.Run(sim, 1, GarrisonFixture.EnterCmd(sim, 0, rifle, bunker));
        Assert.Equal(ActorLayer.Garrisoned, sim.LayerOf(ActorKind.Infantry, rifle.Index));
        GarrisonFixture.Run(sim, 120);
        Assert.False(sim.Units.Slots.IsAlive(target));
        Assert.Equal(300, sim.Veterancy.InfantryKillValue[rifle.Index]);
        GarrisonFixture.Run(sim, 1, GarrisonFixture.Unload(sim, 0, bunker));
        Assert.Equal(ActorLayer.Surface, sim.LayerOf(ActorKind.Infantry, rifle.Index));
        Assert.Equal(300, sim.Veterancy.InfantryKillValue[rifle.Index]);
    }

    [Fact]
    public void SaveLoadMidFlight_KeepsTheShooter_AndTheCreditAgrees()
    {
        var sim = NewVetSim();
        var tank = sim.SpawnVehicle(0, Veh("test_tank"), new CellCoord(10, 10));
        var jeep = sim.SpawnVehicle(1, Veh("test_jeep"), new CellCoord(15, 10));
        sim.Units.Health[jeep.Index] = 1;
        Run(sim, 1, Attack(sim, 0, ActorKind.Vehicle, tank, ActorKind.Vehicle, jeep));
        for (int t = 0; t < 40 && sim.Projectiles.Slots.Count == 0; t++) sim.Step(None);
        Assert.Equal(1, sim.Projectiles.Slots.Count);
        var ms = new MemoryStream();
        sim.Save(ms);
        var loaded = Simulation.Load(new MemoryStream(ms.ToArray()), sim.Setup);
        int slot = Enumerable.Range(1, sim.Projectiles.Capacity - 1).First(p => sim.Projectiles.Slots.IsIndexAlive(p));
        Assert.Equal(new ActorRef(ActorKind.Vehicle, tank), loaded.Projectiles.Shooter[slot]);
        Assert.Equal(sim.ComputeStateHash(), loaded.ComputeStateHash());
        for (int t = 0; t < 60; t++) { sim.Step(None); loaded.Step(None); Assert.Equal(sim.ComputeStateHash(), loaded.ComputeStateHash()); }
        Assert.Equal(400, loaded.Veterancy.VehicleKillValue[tank.Index]);
    }

    [Fact]
    public void ScriptedBattle_IsDeterministic_AndRanksAreEarned()
    {
        static (List<ulong> Hashes, int Total) Battle(ulong seed)
        {
            var sim = NewVetSim(seed: seed);
            var cmds = new List<Command>();
            string[] types = { "test_tank", "test_tank", "test_jeep", "test_jeep" };
            for (byte p = 0; p < 2; p++)
            {
                int x0 = p == 0 ? 6 : 25, dir = p == 0 ? 1 : -1;
                for (int k = 0; k < 4; k++)
                {
                    var id = sim.SpawnVehicle(p, Veh(types[k]), new CellCoord(x0, 10 + k * 2));
                    cmds.Add(AttackMove(sim, p, ActorKind.Vehicle, id, x0 + dir * 17, 12));
                }
                for (int k = 0; k < 2; k++)
                {
                    var id = sim.SpawnInfantry(p, Inf("test_rifleman"), new CellCoord(x0 + dir, 11 + k * 3), k);
                    cmds.Add(AttackMove(sim, p, ActorKind.Infantry, id, x0 + dir * 17, 12));
                }
            }
            var hashes = new List<ulong>();
            for (int t = 0; t < 700; t++) { sim.Step(t == 0 ? cmds : None); hashes.Add(sim.ComputeStateHash()); }
            return (hashes, sim.Veterancy.VehicleKillValue.Sum() + sim.Veterancy.InfantryKillValue.Sum());
        }
        var a = Battle(1); var b = Battle(1);
        Assert.Equal(a.Hashes, b.Hashes);
        Assert.Equal(a.Total, b.Total);
        Assert.True(a.Total > 0, "the battle should produce kills");
    }
}
