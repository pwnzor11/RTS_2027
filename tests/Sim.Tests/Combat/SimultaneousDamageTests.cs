// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Sim.Core;
using Rts.Sim.Rules;
using static Rts.Sim.Tests.Combat.CombatFixture;

namespace Rts.Sim.Tests.Combat;

/// <summary>D-065: all damage of a tick is collected and applied together, so shooters die together whatever their slot order.</summary>
public class SimultaneousDamageTests
{
    private static bool Alive(Simulation sim, ActorKind kind, EntityId id) =>
        kind == ActorKind.Infantry ? sim.Infantry.Slots.IsAlive(id) : kind == ActorKind.Vehicle ? sim.Units.Slots.IsAlive(id) : sim.Buildings.Slots.IsAlive(id);

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void TwoRiflemenWithOneHitpoint_BothDieInTheSameTick_InEitherSlotOrder(bool swapSpawnOrder)
    {
        var sim = NewSim();
        EntityId a, b;
        if (!swapSpawnOrder)
        {
            a = sim.SpawnInfantry(0, Inf("test_rifleman"), new CellCoord(10, 10));
            b = sim.SpawnInfantry(1, Inf("test_rifleman"), new CellCoord(13, 10));
        }
        else
        {
            b = sim.SpawnInfantry(1, Inf("test_rifleman"), new CellCoord(13, 10));
            a = sim.SpawnInfantry(0, Inf("test_rifleman"), new CellCoord(10, 10));
        }
        sim.Infantry.Health[a.Index] = 1; sim.Infantry.Health[b.Index] = 1;
        sim.Step(new[]
        {
            Attack(sim, 0, ActorKind.Infantry, a, ActorKind.Infantry, b),
            Attack(sim, 1, ActorKind.Infantry, b, ActorKind.Infantry, a),
        });
        int ticks = 0;
        while (Alive(sim, ActorKind.Infantry, a) && Alive(sim, ActorKind.Infantry, b) && ticks++ < 100) sim.Step(None);
        Assert.False(Alive(sim, ActorKind.Infantry, a));
        Assert.False(Alive(sim, ActorKind.Infantry, b));
        Assert.True(ticks < 100, "the duel should end");
    }

    [Fact]
    public void TurretAndRiflemanWithOneHitpoint_BothDieInTheSameTick()
    {
        var sim = NewSim();
        sim.PlaceBuilding(0, Bld("test_power"), new CellCoord(4, 4));
        EntityId turret = sim.PlaceBuilding(0, Bld("test_turret"), new CellCoord(10, 10));
        EntityId rifle = sim.SpawnInfantry(1, Inf("test_rifleman"), new CellCoord(13, 10));
        sim.Buildings.Health[turret.Index] = 100000; sim.Infantry.Health[rifle.Index] = 100000;
        // Warm up until both hold their target and have fired, then make both fragile and ready to fire on the same tick.
        sim.Step(new[] { Attack(sim, 1, ActorKind.Infantry, rifle, ActorKind.Building, turret) });
        for (int i = 0; i < 20; i++) sim.Step(None);
        Assert.False(sim.CombatData.At(ActorKind.Building, turret.Index).AttackTarget.Id.IsNone);
        Assert.False(sim.CombatData.At(ActorKind.Infantry, rifle.Index).AttackTarget.Id.IsNone);
        sim.Buildings.Health[turret.Index] = 1; sim.Infantry.Health[rifle.Index] = 1;
        sim.CombatData.At(ActorKind.Building, turret.Index).ReloadTicks = 2;
        sim.CombatData.At(ActorKind.Infantry, rifle.Index).ReloadTicks = 1;   // handlers run before the tick's countdown, turrets after it: 1 and 2 fire together on tick 2
        int ticks = 0;
        while (Alive(sim, ActorKind.Building, turret) && Alive(sim, ActorKind.Infantry, rifle) && ticks++ < 100) sim.Step(None);
        Assert.True(!Alive(sim, ActorKind.Building, turret) && !Alive(sim, ActorKind.Infantry, rifle),
            $"after {ticks} ticks: turret alive {Alive(sim, ActorKind.Building, turret)}, rifleman alive {Alive(sim, ActorKind.Infantry, rifle)}");
    }

    [Fact]
    public void Splash_IsAppliedOncePerTargetPerHit_AndHitsFromTwoShellsAddUp()
    {
        var sim = NewSim();
        var truck = sim.SpawnVehicle(0, Veh("test_artillery_truck"), new CellCoord(10, 10));
        var truck2 = sim.SpawnVehicle(0, Veh("test_artillery_truck"), new CellCoord(10, 12));
        var target = sim.SpawnVehicle(1, Veh("test_tank"), new CellCoord(18, 11));
        var neighbour = sim.SpawnVehicle(1, Veh("test_crawler"), new CellCoord(19, 11));
        Run(sim, 120,
            Attack(sim, 0, ActorKind.Vehicle, truck, ActorKind.Vehicle, target),
            Attack(sim, 0, ActorKind.Vehicle, truck2, ActorKind.Vehicle, target));
        // Each shell does 48 to the tank and 24 to its neighbour (see CombatTests.Splash_...); two shells, once each per hit.
        int hits = (400 - sim.Units.Health[target.Index]) / 48;
        Assert.InRange(hits, 2, 3);
        Assert.Equal(400 - 48 * hits, sim.Units.Health[target.Index]);
        Assert.Equal(300 - 24 * hits, sim.Units.Health[neighbour.Index]);
    }

    [Fact]
    public void MirrorDuel_WithSwappedSlots_EndsWithMirroredCasualties()
    {
        int[] first = Duel(swapSlots: false), second = Duel(swapSlots: true);
        // Result per west/east side: identical whichever player (slot) owns which side.
        Assert.Equal(first[0], first[1]);   // survivors
        Assert.Equal(first[2], first[3]);   // remaining health
        Assert.Equal(first, second);
    }

    // West army = side 0, east army = side 1. Returns [survivors west, survivors east, hp west, hp east].
    private static int[] Duel(bool swapSlots)
    {
        var sim = NewSim(1, 32);
        byte west = swapSlots ? (byte)1 : (byte)0, east = (byte)(1 - west);
        var cmds = new List<Command>();
        ushort seq = 0;
        int[] types = { Veh("test_tank"), Veh("test_jeep"), Veh("test_tank") };
        // Spawn order follows the player number, so the slot order of the two armies swaps with swapSlots.
        var ids = new EntityId[2, 3];
        for (int pass = 0; pass < 2; pass++)
            for (int k = 0; k < 3; k++)
            {
                byte p = (byte)pass;
                bool isWest = p == west;
                int x = isWest ? 8 : 23;
                ids[p, k] = sim.SpawnVehicle(p, types[k], new CellCoord(x, 12 + 2 * k));
                sim.Units.Facing[ids[p, k].Index] = (byte)(isWest ? 64 : 192);
                cmds.Add(new Command(0, p, seq++, CommandKind.AttackMove, ids[p, k], EntityId.None,
                    new CellCoord(isWest ? 23 : 8, 12 + 2 * k), (int)ActorKind.Vehicle));
            }
        sim.Step(cmds);
        for (int t = 1; t < 1500; t++) sim.Step(None);
        int Count(byte p) { int n = 0; for (int i = 1; i < sim.Units.Capacity; i++) if (sim.Units.Slots.IsIndexAlive(i) && sim.Units.Player[i] == p) n++; return n; }
        int Hp(byte p) { int n = 0; for (int i = 1; i < sim.Units.Capacity; i++) if (sim.Units.Slots.IsIndexAlive(i) && sim.Units.Player[i] == p) n += sim.Units.Health[i]; return n; }
        return new[] { Count(west), Count(east), Hp(west), Hp(east) };
    }
}
