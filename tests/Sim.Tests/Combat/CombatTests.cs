// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Sim.Core;
using Rts.Sim.Missions;
using Rts.Sim.Rules;
using static Rts.Sim.Tests.Combat.CombatFixture;

namespace Rts.Sim.Tests.Combat;

public class CombatTests
{
    private static WarheadType Warhead(string id) =>
        Loaded.Sim.Warheads[SimRules.IndexOf(Loaded.Sim.Warheads, id, w => w.Id)];

    // ---- damage ----------------------------------------------------------------------------

    [Theory]
    [InlineData("test_ap", ArmorClass.Heavy, 40, 40)]
    [InlineData("test_ap", ArmorClass.Unarmored, 40, 10)]
    [InlineData("test_ap", ArmorClass.Structure, 40, 20)]
    [InlineData("test_small_arms", ArmorClass.Fortified, 6, 1)]   // 0.3 rounds up to the minimum of 1
    [InlineData("test_small_arms", ArmorClass.Heavy, 8, 1)]       // 1.2 truncates to 1
    [InlineData("test_he", ArmorClass.Light, 80, 64)]
    public void DamageTable_AppliesVersusPercent_WithMinimumOne(string warhead, ArmorClass armor, int damage, int expected)
    {
        Assert.Equal(expected, Simulation.ComputeDamage(damage, Warhead(warhead), armor));
    }

    [Fact]
    public void Splash_FullInCentre_HalfInNeighbours_HitsAllies_NotBeyond()
    {
        var sim = NewSim();
        var truck = sim.SpawnVehicle(0, Veh("test_artillery_truck"), new CellCoord(10, 10));
        var target = sim.SpawnVehicle(1, Veh("test_tank"), new CellCoord(18, 10));        // heavy: he 60%
        var neighbour = sim.SpawnVehicle(1, Veh("test_crawler"), new CellCoord(19, 10));  // heavy, 300 hp
        var far = sim.SpawnVehicle(1, Veh("test_crawler"), new CellCoord(20, 10));        // 2 cells away: untouched
        var ally = sim.SpawnVehicle(0, Veh("test_jeep"), new CellCoord(18, 11));          // light: he 80%, hits allies

        Run(sim, 80, Attack(sim, 0, ActorKind.Vehicle, truck, ActorKind.Vehicle, target));

        Assert.Equal(400 - 48, sim.Units.Health[target.Index]);
        Assert.Equal(300 - 24, sim.Units.Health[neighbour.Index]);
        Assert.Equal(300, sim.Units.Health[far.Index]);
        Assert.Equal(150 - 32, sim.Units.Health[ally.Index]);
        Assert.Equal(0, sim.Projectiles.Slots.Count);
    }

    [Fact]
    public void NonAllyWarhead_DoesNotHurtOwnersUnitsInTheImpactCell()
    {
        var sim = NewSim();
        var tank = sim.SpawnVehicle(0, Veh("test_tank"), new CellCoord(10, 10));
        var enemy = sim.SpawnInfantry(1, Inf("test_rifleman"), new CellCoord(10, 6), 1);
        var friend = sim.SpawnInfantry(0, Inf("test_rifleman"), new CellCoord(10, 6), 2);
        Run(sim, 40, Attack(sim, 0, ActorKind.Vehicle, tank, ActorKind.Infantry, enemy));
        Assert.Equal(40, sim.Infantry.Health[enemy.Index]);   // ap vs unarmored: 10
        Assert.Equal(50, sim.Infantry.Health[friend.Index]);
    }

    // ---- aiming ----------------------------------------------------------------------------

    [Fact]
    public void Turret_TurnsIndependentlyOfHull()
    {
        var sim = NewSim();
        var tank = sim.SpawnVehicle(0, Veh("test_tank"), new CellCoord(10, 10));
        var victim = sim.SpawnInfantry(1, Inf("test_rifleman"), new CellCoord(14, 10));   // due east: facing 64
        sim.Units.Facing[tank.Index] = 0;   // M2-01: setup spawns face the map centre; this test wants north
        sim.Step(Array.Empty<Command>());
        sim.Step(new[] { Attack(sim, 0, ActorKind.Vehicle, tank, ActorKind.Infantry, victim) });
        sim.Step(None); sim.Step(None);
        Assert.Equal(0, sim.Units.Facing[tank.Index]);
        Assert.Equal(30, sim.CombatData.At(ActorKind.Vehicle, tank.Index).TurretFacing);   // 3 ticks x 10
        Assert.Equal(50, sim.Infantry.Health[victim.Index]);                               // not aimed yet
        for (int i = 0; i < 10; i++) sim.Step(None);
        Assert.Equal(0, sim.Units.Facing[tank.Index]);
        Assert.Equal(64, sim.CombatData.At(ActorKind.Vehicle, tank.Index).TurretFacing);
        Assert.Equal(new LeptonCoord(10 * 256 + 128, 10 * 256 + 128), sim.Units.Position[tank.Index]);
        Assert.Equal(1, sim.Projectiles.Slots.Count);                                     // shell in flight
        Assert.Equal(50, sim.Infantry.Health[victim.Index]);                              // not hit yet
    }

    [Fact]
    public void HullMountedJeep_TurnsInPlaceToFire()
    {
        var sim = NewSim();
        var jeep = sim.SpawnVehicle(0, Veh("test_jeep"), new CellCoord(10, 10));
        var victim = sim.SpawnInfantry(1, Inf("test_rifleman"), new CellCoord(13, 10));   // 3 cells east, in range
        sim.Units.Facing[jeep.Index] = 0;   // M2-01: setup spawns face the map centre; this test wants north
        var start = sim.Units.Position[jeep.Index];
        Run(sim, 3, Attack(sim, 0, ActorKind.Vehicle, jeep, ActorKind.Infantry, victim));
        Assert.Equal(36, sim.Units.Facing[jeep.Index]);       // 3 ticks x turn rate 12
        Assert.Equal(50, sim.Infantry.Health[victim.Index]);  // still turning, no shot yet
        Run(sim, 10);
        Assert.Equal(64, sim.Units.Facing[jeep.Index]);
        Assert.Equal(start, sim.Units.Position[jeep.Index]);
        Assert.True(sim.Infantry.Health[victim.Index] < 50);
    }

    [Fact]
    public void Artillery_WillNotFireInsideMinRange_ButFiresOutsideIt()
    {
        var sim = NewSim();
        var truck = sim.SpawnVehicle(0, Veh("test_artillery_truck"), new CellCoord(10, 10));
        var close = sim.SpawnVehicle(1, Veh("test_tank"), new CellCoord(11, 10));   // 256 < 768
        Run(sim, 60, Attack(sim, 0, ActorKind.Vehicle, truck, ActorKind.Vehicle, close));
        Assert.Equal(0, sim.Projectiles.Slots.Count);
        Assert.Equal(400, sim.Units.Health[close.Index]);

        var far = Boundary.LateSpawn.Vehicle(sim, 1, Veh("test_tank"), new CellCoord(10, 18));
        Run(sim, 30, Attack(sim, 0, ActorKind.Vehicle, truck, ActorKind.Vehicle, far));   // hull turns ~22 ticks first
        Assert.Equal(1, sim.Projectiles.Slots.Count);
    }

    [Fact]
    public void Projectile_FlightTime_IsCeilOfDistanceOverSpeed()
    {
        var sim = NewSim();
        var tank = sim.SpawnVehicle(0, Veh("test_tank"), new CellCoord(10, 10));
        var victim = sim.SpawnInfantry(1, Inf("test_rifleman"), new CellCoord(10, 6));   // due north: no turning needed, 1024 away
        sim.Units.Facing[tank.Index] = 0;   // M2-01: setup spawns face the map centre; this test wants north
        sim.Step(new[] { Attack(sim, 0, ActorKind.Vehicle, tank, ActorKind.Infantry, victim) });
        Assert.Equal(1, sim.Projectiles.Slots.Count);            // spawned and moved during tick 0
        int expectedTicks = (1024 + 64 - 1) / 64;                // 16
        for (int t = 1; t < expectedTicks - 1; t++) sim.Step(None);
        Assert.Equal(1, sim.Projectiles.Slots.Count);            // alive after expectedTicks - 1 steps
        Assert.Equal(50, sim.Infantry.Health[victim.Index]);
        sim.Step(None);
        Assert.Equal(0, sim.Projectiles.Slots.Count);            // detonated on step number expectedTicks
        Assert.Equal(40, sim.Infantry.Health[victim.Index]);
    }

    // ---- missions --------------------------------------------------------------------------

    [Fact]
    public void Attack_ChasesIntoRange_ThenKills_ThenGuards()
    {
        var sim = NewSim();
        var tank = sim.SpawnVehicle(0, Veh("test_tank"), new CellCoord(5, 5));
        var victim = sim.SpawnInfantry(1, Inf("test_rifleman"), new CellCoord(17, 5));   // 12 cells away
        Run(sim, 600, Attack(sim, 0, ActorKind.Vehicle, tank, ActorKind.Infantry, victim));
        Assert.False(sim.Infantry.Slots.IsAlive(victim));
        Assert.True(sim.Units.Position[tank.Index].X > 5 * 256 + 128);
        Assert.Equal(MissionKind.Guard, sim.Units.Mission[tank.Index]);
        Assert.True(sim.CombatData.At(ActorKind.Vehicle, tank.Index).AttackTarget.Id.IsNone);
    }

    [Fact]
    public void Attack_OnOwnUnit_OrWeaponlessSubject_IsIgnored()
    {
        var sim = NewSim();
        var tank = sim.SpawnVehicle(0, Veh("test_tank"), new CellCoord(5, 5));
        var own = sim.SpawnVehicle(0, Veh("test_jeep"), new CellCoord(7, 5));
        var crawler = sim.SpawnVehicle(0, Veh("test_crawler"), new CellCoord(9, 5));
        var enemy = sim.SpawnVehicle(1, Veh("test_jeep"), new CellCoord(7, 7));
        sim.Step(new[]
        {
            Attack(sim, 0, ActorKind.Vehicle, tank, ActorKind.Vehicle, own),
            Attack(sim, 0, ActorKind.Vehicle, crawler, ActorKind.Vehicle, enemy),
            Attack(sim, 1, ActorKind.Vehicle, tank, ActorKind.Vehicle, enemy),   // not the owner
        });
        Assert.Equal(MissionKind.None, sim.Units.Mission[tank.Index]);
        Assert.Equal(MissionKind.None, sim.Units.Mission[crawler.Index]);
    }

    [Fact]
    public void Guard_OnlyAcquiresVisibleEnemies()
    {
        var sim = NewSim();
        var truck = sim.SpawnVehicle(0, Veh("test_artillery_truck"), new CellCoord(10, 10));   // range 10 cells, sight 6
        var enemy = sim.SpawnVehicle(1, Veh("test_crawler"), new CellCoord(18, 10));           // 8 cells: in range, unseen
        Run(sim, 60, Guard(sim, 0, ActorKind.Vehicle, truck, 10, 10));
        Assert.True(sim.CombatData.At(ActorKind.Vehicle, truck.Index).AttackTarget.Id.IsNone);
        Assert.Equal(0, sim.Projectiles.Slots.Count);
        Assert.Equal(300, sim.Units.Health[enemy.Index]);

        Boundary.LateSpawn.Infantry(sim, 0, Inf("test_engineer"), new CellCoord(15, 10));   // scout reveals the enemy
        Run(sim, 40);
        Assert.Equal(enemy, sim.CombatData.At(ActorKind.Vehicle, truck.Index).AttackTarget.Id);
        Assert.Equal(300, sim.Units.Health[enemy.Index]);                                 // shell in flight, not hit yet
        Assert.Equal(1, sim.Projectiles.Slots.Count);
    }

    [Fact]
    public void Guard_ChasesWithinSightThenReturnsToGuardPoint()
    {
        var sim = NewSim();
        var jeep = sim.SpawnVehicle(0, Veh("test_jeep"), new CellCoord(10, 10));      // range 3.5, sight 7
        var bait = sim.SpawnVehicle(1, Veh("test_tank"), new CellCoord(10, 7));       // 3 cells north, in range
        Run(sim, 20, Guard(sim, 0, ActorKind.Vehicle, jeep, 10, 10));   // first scan has no vision yet; next at tick 15
        Assert.Equal(bait, sim.CombatData.At(ActorKind.Vehicle, jeep.Index).AttackTarget.Id);
        // Bait flees far beyond the leash; the jeep must give up and come home.
        sim.Units.Position[bait.Index] = new LeptonCoord(10 * 256 + 128, 30 * 256);
        Run(sim, 400);
        Assert.True(sim.CombatData.At(ActorKind.Vehicle, jeep.Index).AttackTarget.Id.IsNone);
        Assert.Equal(new CellCoord(10, 10), sim.Units.Position[jeep.Index].ToCell());
    }

    [Fact]
    public void AttackMove_EngagesOnTheWay_ThenResumes()
    {
        var sim = NewSim();
        var jeep = sim.SpawnVehicle(0, Veh("test_jeep"), new CellCoord(4, 10));
        var victim = sim.SpawnInfantry(1, Inf("test_rifleman"), new CellCoord(12, 11));
        Run(sim, 400, AttackMove(sim, 0, ActorKind.Vehicle, jeep, 20, 10));
        Assert.False(sim.Infantry.Slots.IsAlive(victim));
        Assert.Equal(new CellCoord(20, 10), sim.Units.Position[jeep.Index].ToCell());
        Assert.Equal(MissionKind.Guard, sim.Units.Mission[jeep.Index]);
    }

    [Fact]
    public void AttackMove_WithoutWeapon_JustMoves()
    {
        var sim = NewSim();
        var crawler = sim.SpawnVehicle(0, Veh("test_crawler"), new CellCoord(4, 10));
        sim.SpawnInfantry(1, Inf("test_rifleman"), new CellCoord(8, 10));
        Run(sim, 300, AttackMove(sim, 0, ActorKind.Vehicle, crawler, 12, 10));
        Assert.Equal(new CellCoord(12, 10), sim.Units.Position[crawler.Index].ToCell());
    }

    [Fact]
    public void TurretBuilding_KillsApproachingInfantry()
    {
        var sim = NewSim();
        sim.PlaceBuilding(0, Bld("test_turret"), new CellCoord(10, 10));
        sim.PlaceBuilding(0, Bld("test_power"), new CellCoord(4, 4));   // M2: the turret needs_power; without a plant it holds fire
        var raider = sim.SpawnInfantry(1, Inf("test_rifleman"), new CellCoord(10, 15));
        Run(sim, 300, Move(sim, 1, ActorKind.Infantry, raider, 10, 12));
        Assert.False(sim.Infantry.Slots.IsAlive(raider));
    }

    [Fact]
    public void DeadTarget_ClearsAttackersAndProjectileTargets()
    {
        var sim = NewSim();
        var tank = sim.SpawnVehicle(0, Veh("test_tank"), new CellCoord(10, 10));
        var jeep = sim.SpawnVehicle(0, Veh("test_jeep"), new CellCoord(11, 10));
        var victim = sim.SpawnInfantry(1, Inf("test_rifleman"), new CellCoord(10, 7));
        sim.Units.Facing[tank.Index] = 0; sim.Units.Facing[jeep.Index] = 0;   // M2-01: setup spawns face the map centre; this test wants north
        sim.Step(new[]
        {
            Attack(sim, 0, ActorKind.Vehicle, tank, ActorKind.Infantry, victim),
            Attack(sim, 0, ActorKind.Vehicle, jeep, ActorKind.Infantry, victim),
        });
        Assert.Equal(1, sim.Projectiles.Slots.Count);
        int p = sim.Projectiles.Slots.AliveIndexes().Single();
        Assert.Equal(victim, sim.Projectiles.Target[p].Id);
        // Park the attackers so only the removal hook can clear them, then kill the victim.
        sim.Units.WakeTick[tank.Index] = sim.Tick + 50;
        sim.Units.WakeTick[jeep.Index] = sim.Tick + 50;
        sim.Infantry.Health[victim.Index] = 0;
        sim.Step(None);
        Assert.False(sim.Infantry.Slots.IsAlive(victim));
        Assert.True(sim.CombatData.At(ActorKind.Vehicle, tank.Index).AttackTarget.Id.IsNone);
        Assert.True(sim.CombatData.At(ActorKind.Vehicle, jeep.Index).AttackTarget.Id.IsNone);
        Assert.True(sim.Projectiles.Target[p].Id.IsNone);
        Assert.Equal(1, sim.Projectiles.Slots.Count);   // the shell still flies on and hits the cell
        Run(sim, 30);
        Assert.Equal(0, sim.Projectiles.Slots.Count);
    }

    // ---- save / determinism ----------------------------------------------------------------

    [Fact]
    public void SaveLoad_MidFlight_GivesIdenticalHashes()
    {
        var sim = NewSim();
        var truck = sim.SpawnVehicle(0, Veh("test_artillery_truck"), new CellCoord(10, 10));
        var target = sim.SpawnVehicle(1, Veh("test_tank"), new CellCoord(10, 18));
        Run(sim, 30, Attack(sim, 0, ActorKind.Vehicle, truck, ActorKind.Vehicle, target));
        Assert.Equal(1, sim.Projectiles.Slots.Count);

        using var ms = new MemoryStream();
        sim.Save(ms);
        ms.Position = 0;
        var loaded = Simulation.Load(ms, sim.Setup);
        Assert.Equal(sim.ComputeStateHash(), loaded.ComputeStateHash());
        Assert.Equal(1, loaded.Projectiles.Slots.Count);

        for (int t = 0; t < 150; t++)
        {
            sim.Step(None); loaded.Step(None);
            Assert.Equal(sim.ComputeStateHash(), loaded.ComputeStateHash());
        }
        Assert.True(sim.Units.Health[target.Index] < 400);
    }

    /// <summary>Runs the scripted battle. Returns the state hash after every tick and the units lost by each player.</summary>
    private static List<ulong> Battle(ulong seed, out int[] casualtiesByPlayer)
    {
        var sim = NewSim(seed, 48);
        string[] vehicles = { "test_tank", "test_tank", "test_jeep", "test_jeep" };
        var cmds = new List<Command>();
        var units = new List<(byte Player, EntityId Id, bool Vehicle)>();
        for (byte p = 0; p < 2; p++)
        {
            int x0 = p == 0 ? 6 : 40, dir = p == 0 ? 1 : -1;
            for (int k = 0; k < 4; k++)
            {
                var id = sim.SpawnVehicle(p, Veh(vehicles[k]), new CellCoord(x0, 20 + k * 2));
                units.Add((p, id, true));
                cmds.Add(AttackMove(sim, p, ActorKind.Vehicle, id, x0 + dir * 30, 22));
            }
            for (int k = 0; k < 2; k++)
            {
                var id = sim.SpawnInfantry(p, Inf("test_rifleman"), new CellCoord(x0 + dir, 21 + k * 3), k);
                units.Add((p, id, false));
                cmds.Add(AttackMove(sim, p, ActorKind.Infantry, id, x0 + dir * 30, 22));
            }
        }
        var hashes = new List<ulong>();
        for (int t = 0; t < 700; t++)
        {
            sim.Step(t == 0 ? cmds : None);
            hashes.Add(sim.ComputeStateHash());
        }
        casualtiesByPlayer = new int[2];
        foreach (var (player, id, vehicle) in units)
        {
            bool alive = vehicle ? sim.Units.Slots.IsAlive(id) : sim.Infantry.Slots.IsAlive(id);
            if (!alive) casualtiesByPlayer[player]++;
        }
        return hashes;
    }

    [Fact]
    public void ScriptedBattle_IsDeterministic_AndSeedSensitive()
    {
        var a = Battle(1, out int[] casualtiesA);
        var b = Battle(1, out int[] casualtiesB);
        var c = Battle(2, out _);
        Assert.Equal(a, b);
        Assert.Equal(casualtiesA, casualtiesB);
        Assert.True(casualtiesA.Sum() > 0, "the battle should actually kill something");
        Assert.Equal(new[] { 6, 4 }, casualtiesA);                 // exact losses per player for seed 1
        Assert.Equal(0xDD1088B14A69B1EDUL, a[^1]);                  // exact final tick hash for seed 1 (M2-19: single-hit shells; M3-01, M4-01, M5-01: appended parts; M4-06: live stats counters; M6-02: rigs/salvage parts, effects v2; VET-1: veterancy part; VET-2: shooter column; M7-02: shipments, beachhead, naval parts)
        Assert.NotEqual(a[^1], c[^1]);
    }

    [Fact]
    public void CombatParts_AreAppendedInOrder()
    {
        var names = NewSim().StateParts.Select(p => p.Name).ToArray();
        Assert.Equal(new[] { "combat", "projectiles" }, names[6..8]);
    }
}
