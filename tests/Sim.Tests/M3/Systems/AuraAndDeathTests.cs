// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Sim.Core;
using Rts.Sim.Map;
using static Rts.Sim.Tests.M3.Systems.SystemsFixture;

namespace Rts.Sim.Tests.M3.Systems;

/// <summary>M3-06 auras (speed, rate of fire, expiry) and death weapons.</summary>
public sealed class AuraAndDeathTests
{
    [Fact]
    public void Aura_AppliesInRange_ToMatchingFriendlies_AndNotToSelfOrEnemies()
    {
        Simulation sim = NewSim();
        EntityId aura = sim.SpawnVehicle(0, Aura, new CellCoord(10, 10));
        EntityId near = sim.SpawnVehicle(0, Grunt, new CellCoord(13, 10));
        EntityId far = sim.SpawnVehicle(0, Grunt, new CellCoord(20, 10));
        EntityId enemy = sim.SpawnVehicle(1, Grunt, new CellCoord(11, 10));
        EntityId otherType = sim.SpawnVehicle(0, Truck, new CellCoord(11, 12));
        Run(sim, 1);
        Assert.Equal(1500, sim.Effects.VehicleSpeedPermille[near.Index]);
        Assert.Equal(800, sim.Effects.VehicleRofPermille[near.Index]);
        Assert.Equal(0, sim.Effects.VehicleSpeedPermille[far.Index]);
        Assert.Equal(0, sim.Effects.VehicleSpeedPermille[enemy.Index]);
        Assert.Equal(0, sim.Effects.VehicleSpeedPermille[otherType.Index]);
        Assert.Equal(0, sim.Effects.VehicleSpeedPermille[aura.Index]);
    }

    [Fact]
    public void Aura_IsRecomputedOnlyEveryTenTicks_AndExpiresWhenOutOfRange()
    {
        Simulation sim = NewSim();
        sim.SpawnVehicle(0, Aura, new CellCoord(5, 5));
        EntityId g = sim.SpawnVehicle(0, Grunt, new CellCoord(8, 5));
        Run(sim, 1);
        Assert.Equal(1500, sim.Effects.VehicleSpeedPermille[g.Index]);
        Run(sim, 1, Move(sim, 0, ActorKind.Vehicle, g, 30, 5));
        Run(sim, 400);
        Assert.True(sim.Units.Position[g.Index].ToCell().X > 12);
        Assert.Equal(0, sim.Effects.VehicleSpeedPermille[g.Index]);
        Assert.Equal(0, sim.Effects.VehicleRofPermille[g.Index]);
    }

    [Fact]
    public void Aura_Expires_WhenTheSourceDies()
    {
        Simulation sim = NewSim();
        EntityId aura = sim.SpawnVehicle(0, Aura, new CellCoord(5, 5));
        EntityId g = sim.SpawnVehicle(0, Grunt, new CellCoord(7, 5));
        Run(sim, 1);
        Assert.Equal(1500, sim.Effects.VehicleSpeedPermille[g.Index]);
        Run(sim, 1, Kill(sim, ActorKind.Vehicle, aura));
        Run(sim, 10);
        Assert.Equal(0, sim.Effects.VehicleSpeedPermille[g.Index]);
    }

    [Fact]
    public void Aura_Stacking_BestSingleSourceWins_NotMultiplied()
    {
        Simulation sim = NewSim();
        sim.SpawnVehicle(0, Aura, new CellCoord(5, 5));
        sim.SpawnVehicle(0, Aura, new CellCoord(5, 7));
        EntityId g = sim.SpawnVehicle(0, Grunt, new CellCoord(7, 6));
        Run(sim, 1);
        Assert.Equal(1500, sim.Effects.VehicleSpeedPermille[g.Index]);
        Assert.Equal(800, sim.Effects.VehicleRofPermille[g.Index]);
    }

    [Fact]
    public void Aura_SpeedsUpMovement()
    {
        Simulation sim = NewSim();
        sim.SpawnVehicle(0, Aura, new CellCoord(3, 3));
        EntityId boosted = sim.SpawnVehicle(0, Grunt, new CellCoord(4, 4));
        EntityId plain = sim.SpawnVehicle(0, Grunt, new CellCoord(4, 30));
        Run(sim, 1);
        int bx = sim.Units.Position[boosted.Index].X, px = sim.Units.Position[plain.Index].X;
        Run(sim, 1, Move(sim, 0, ActorKind.Vehicle, boosted, 20, 4), Move(sim, 0, ActorKind.Vehicle, plain, 20, 30));
        Run(sim, 9);   // still inside the aura radius (recompute is at tick 10)
        int boostedDist = sim.Units.Position[boosted.Index].X - bx, plainDist = sim.Units.Position[plain.Index].X - px;
        Assert.True(boostedDist > plainDist, $"{boostedDist} vs {plainDist}");
    }

    [Fact]
    public void Aura_ShortensTheReload()
    {
        static int FirstReload(bool withAura)
        {
            Simulation sim = NewSim();
            if (withAura) sim.SpawnVehicle(0, Aura, new CellCoord(3, 3));
            EntityId g = sim.SpawnVehicle(0, Grunt, new CellCoord(4, 4));
            sim.PlaceBuilding(1, Tower, new CellCoord(7, 4));
            sim.Players.Cash[0] = 0;
            Run(sim, 10);   // aura recomputed at tick 0 and 10
            EntityId target = sim.Map.BuildingAt(new CellCoord(7, 4));
            sim.Step(new[] { Cmd(sim, 0, CommandKind.Attack, g, target, default, (int)ActorKind.Vehicle | ((int)ActorKind.Building << 8)) });
            for (int i = 0; i < 40; i++)
            {
                if (sim.CombatData.At(ActorKind.Vehicle, g.Index).ReloadTicks > 0) return sim.CombatData.At(ActorKind.Vehicle, g.Index).ReloadTicks;
                sim.Step(None);
            }
            throw new InvalidOperationException("never fired");
        }
        Assert.Equal(29, FirstReload(false));   // 30 ticks, one already counted down in the firing tick
        Assert.Equal(23, FirstReload(true));    // 30 * 800 / 1000 = 24
    }

    [Fact]
    public void DeathWeapon_DetonatesAtTheDyingActorsCell()
    {
        Simulation sim = NewSim();
        EntityId crawler = sim.SpawnVehicle(0, Crawler, new CellCoord(10, 10));
        EntityId neighbour = sim.SpawnVehicle(1, Grunt, new CellCoord(11, 10));
        EntityId distant = sim.SpawnVehicle(1, Grunt, new CellCoord(14, 10));
        EntityId friend = sim.SpawnVehicle(0, Grunt, new CellCoord(10, 11));
        Run(sim, 1);
        Run(sim, 1, Kill(sim, ActorKind.Vehicle, crawler));
        Run(sim, 2);
        Assert.False(sim.Units.Slots.IsAlive(crawler));
        Assert.Equal(100 - 25, sim.Units.Health[neighbour.Index]);   // splash 1: half damage one cell away
        Assert.Equal(100, sim.Units.Health[distant.Index]);
        Assert.Equal(100, sim.Units.Health[friend.Index]);           // warhead does not hit allies
    }

    [Fact]
    public void DeathWeapon_FullDamageInTheSameCell_AndAKilledNeighbourDiesToo()
    {
        Simulation sim = NewSim();
        EntityId crawler = sim.SpawnVehicle(0, Crawler, new CellCoord(10, 10));
        EntityId weak = sim.SpawnInfantry(1, Rifle, new CellCoord(10, 10), 1);
        Run(sim, 1, Kill(sim, ActorKind.Vehicle, crawler));
        Run(sim, 2);
        Assert.False(sim.Infantry.Slots.IsAlive(weak));              // 50 damage vs 50 health
    }
}
