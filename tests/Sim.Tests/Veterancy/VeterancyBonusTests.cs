// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Sim.Core;
using Rts.Sim.Map;
using Rts.Sim.Rules;
using Rts.Sim.View;
using static Rts.Sim.Tests.Combat.CombatFixture;

namespace Rts.Sim.Tests.Veterancy;

/// <summary>VET-3 (docs/design/14_veterancy.md §2.2–§2.3): rank from kill value, and the bonuses per rank: damage dealt, damage taken,
/// reload, speed, rank-3 self-heal. The table is the design table.</summary>
public sealed class VeterancyBonusTests
{
    private static readonly int[] KillValueOfRank = { 0, 600, 1600, 3200 };

    internal static VeterancyRules Table => new()
    {
        Enabled = true,
        DamagePermille = new[] { 1100, 1200, 1300 },
        TakenPermille = new[] { 950, 900, 800 },
        ReloadPermille = new[] { 950, 900, 800 },
        SpeedPermille = new[] { 1000, 1050, 1100 },
        HealPermille = new[] { 0, 0, 10 },
    };

    /// <summary>The fixture rules with a rifle that hits for 100 (so permille bonuses show) and rifleman/tank health out of the way.</summary>
    private static SimRules Rules(VeterancyRules vet, Func<InfantryType, InfantryType>? infantry = null)
    {
        SimRules r = Loaded.Sim;
        return r with
        {
            Veterancy = vet,
            Weapons = r.Weapons.Select(w => w.Id == "test_rifle" ? w with { Damage = 100 } : w).ToArray(),
            Infantry = infantry == null ? r.Infantry : r.Infantry.Select(infantry).ToArray(),
        };
    }

    private static Simulation NewSim(VeterancyRules vet, Func<InfantryType, InfantryType>? infantry = null) =>
        new(new SimSetup(1, MapData.CreateFlat(32, 32), 2, Rules(vet, infantry), Loaded.RulesHash) { AllowDebug = true });

    // ---- rank from kill value -------------------------------------------------------------------

    [Fact]
    public void Rank_FollowsTheThresholds_ForVehiclesAndSoldiers()
    {
        var sim = NewSim(Table);
        var jeep = sim.SpawnVehicle(0, Veh("test_jeep"), new CellCoord(5, 5));
        var rifle = sim.SpawnInfantry(0, Inf("test_rifleman"), new CellCoord(8, 8));
        (int Kv, int Rank)[] cases = { (0, 0), (1, 0), (599, 0), (600, 1), (1599, 1), (1600, 2), (3199, 2), (3200, 3), (1_000_000, 3) };
        foreach ((int kv, int rank) in cases)
        {
            sim.Veterancy.VehicleKillValue[jeep.Index] = kv;
            sim.Veterancy.InfantryKillValue[rifle.Index] = kv;
            Assert.Equal(rank, sim.RankOf(ActorKind.Vehicle, jeep.Index));
            Assert.Equal(rank, sim.RankOf(ActorKind.Infantry, rifle.Index));
        }
        Assert.Equal(0, sim.RankOf(ActorKind.Building, 1));
        Assert.Equal(0, sim.RankOf(ActorKind.Vehicle, 40));   // a free slot
    }

    [Fact]
    public void Rank_IsZeroWhenDisabled_AndForTypesThatCannotRank()
    {
        var off = NewSim(new VeterancyRules());
        var jeep = off.SpawnVehicle(0, Veh("test_jeep"), new CellCoord(5, 5));
        off.Veterancy.VehicleKillValue[jeep.Index] = 99999;
        Assert.Equal(0, off.RankOf(ActorKind.Vehicle, jeep.Index));

        var sim = NewSim(Table);
        var harvester = sim.SpawnVehicle(0, Veh("test_harvester"), new CellCoord(5, 5));   // unarmed: veteran defaults to false
        var engineer = sim.SpawnInfantry(0, Inf("test_engineer"), new CellCoord(8, 8));
        sim.Veterancy.VehicleKillValue[harvester.Index] = 99999;
        sim.Veterancy.InfantryKillValue[engineer.Index] = 99999;
        Assert.Equal(0, sim.RankOf(ActorKind.Vehicle, harvester.Index));
        Assert.Equal(0, sim.RankOf(ActorKind.Infantry, engineer.Index));
    }

    [Fact]
    public void ExplicitVeteranFlag_OverridesTheArmedDefault()
    {
        SimRules r = Rules(Table);
        r = r with
        {
            Vehicles = r.Vehicles.Select(v => v.Id == "test_harvester" ? v with { Veteran = true } : v.Id == "test_jeep" ? v with { Veteran = false } : v).ToArray(),
        };
        var sim = new Simulation(new SimSetup(1, MapData.CreateFlat(32, 32), 2, r, Loaded.RulesHash));
        var harvester = sim.SpawnVehicle(0, Veh("test_harvester"), new CellCoord(5, 5));
        var jeep = sim.SpawnVehicle(0, Veh("test_jeep"), new CellCoord(8, 8));
        sim.Veterancy.VehicleKillValue[harvester.Index] = 700; sim.Veterancy.VehicleKillValue[jeep.Index] = 700;
        Assert.Equal(1, sim.RankOf(ActorKind.Vehicle, harvester.Index));
        Assert.Equal(0, sim.RankOf(ActorKind.Vehicle, jeep.Index));
    }

    [Fact]
    public void ThresholdScale_LowersTheBarOnly()
    {
        var sim = NewSim(Table, i => i with { VetThresholdPermille = 800 });   // 480 / 1280 / 2560
        var rifle = sim.SpawnInfantry(0, Inf("test_rifleman"), new CellCoord(8, 8));
        (int Kv, int Rank)[] cases = { (479, 0), (480, 1), (1279, 1), (1280, 2), (2559, 2), (2560, 3) };
        foreach ((int kv, int rank) in cases)
        {
            sim.Veterancy.InfantryKillValue[rifle.Index] = kv;
            Assert.Equal(rank, sim.RankOf(ActorKind.Infantry, rifle.Index));
        }
    }

    // ---- damage dealt ---------------------------------------------------------------------------

    private static int FirstHit(Simulation sim, EntityId victim)
    {
        for (int t = 0; t < 60; t++)
        {
            sim.Step(None);
            int hp = sim.Infantry.Health[victim.Index];
            if (hp < 100_000) return 100_000 - hp;
        }
        return 0;
    }

    [Theory]
    [InlineData(0, 100)]
    [InlineData(1, 110)]
    [InlineData(2, 120)]
    [InlineData(3, 130)]
    public void DamageDealt_ScalesWithTheShootersRank(int rank, int expected)
    {
        var sim = NewSim(Table);
        var shooter = sim.SpawnInfantry(0, Inf("test_rifleman"), new CellCoord(10, 10));
        var victim = sim.SpawnInfantry(1, Inf("test_rifleman"), new CellCoord(13, 10));
        sim.Infantry.Health[victim.Index] = 100_000;
        sim.Infantry.Health[shooter.Index] = 100_000;
        sim.Veterancy.InfantryKillValue[shooter.Index] = KillValueOfRank[rank];
        sim.Step(new[] { Attack(sim, 0, ActorKind.Infantry, shooter, ActorKind.Infantry, victim) });
        sim.Infantry.Health[victim.Index] = 100_000;   // the first tick may already have fired
        Assert.Equal(expected, FirstHit(sim, victim));
    }

    // ---- damage taken ---------------------------------------------------------------------------

    [Theory]
    [InlineData(0, 100)]
    [InlineData(1, 95)]
    [InlineData(2, 90)]
    [InlineData(3, 80)]
    public void DamageTaken_ScalesWithTheVictimsRank(int rank, int expected)
    {
        var sim = NewSim(Table);
        var shooter = sim.SpawnInfantry(0, Inf("test_rifleman"), new CellCoord(10, 10));
        var victim = sim.SpawnInfantry(1, Inf("test_rifleman"), new CellCoord(13, 10));
        sim.Infantry.Health[victim.Index] = 100_000;
        sim.Infantry.Health[shooter.Index] = 100_000;
        sim.Veterancy.InfantryKillValue[victim.Index] = KillValueOfRank[rank];
        sim.Step(new[] { Attack(sim, 0, ActorKind.Infantry, shooter, ActorKind.Infantry, victim) });
        sim.Infantry.Health[victim.Index] = 100_000;
        Assert.Equal(expected, FirstHit(sim, victim));
    }

    [Fact]
    public void DamageTaken_NeverDropsBelowOne()
    {
        var sim = NewSim(Table);
        var victim = sim.SpawnVehicle(1, Veh("test_tank"), new CellCoord(13, 10));
        sim.Veterancy.VehicleKillValue[victim.Index] = 3200;
        int hp = sim.Units.Health[victim.Index];
        sim.Detonate(0, 1, SimRules.IndexOf(Loaded.Sim.Warheads, "test_small_arms", w => w.Id), sim.Units.Position[victim.Index],
            new ActorRef(ActorKind.Vehicle, victim), true);   // 1 x 10 % -> 1 (minimum), x 800 permille -> still 1
        sim.Step(None);
        Assert.Equal(hp - 1, sim.Units.Health[victim.Index]);
    }

    // ---- reload ---------------------------------------------------------------------------------

    [Theory]
    [InlineData(0, 15)]
    [InlineData(1, 14)]   // 15 x 950 / 1000 = 14.25
    [InlineData(2, 13)]   // 13.5
    [InlineData(3, 12)]
    public void Reload_ScalesWithTheShootersRank(int rank, int interval)
    {
        var sim = NewSim(Table);
        var shooter = sim.SpawnInfantry(0, Inf("test_rifleman"), new CellCoord(10, 10));
        var victim = sim.SpawnInfantry(1, Inf("test_rifleman"), new CellCoord(13, 10));
        sim.Infantry.Health[victim.Index] = 100_000; sim.Infantry.Health[shooter.Index] = 100_000;
        sim.Veterancy.InfantryKillValue[shooter.Index] = KillValueOfRank[rank];
        var hits = new List<int>();
        sim.Step(new[] { Attack(sim, 0, ActorKind.Infantry, shooter, ActorKind.Infantry, victim) });
        int last = sim.Infantry.Health[victim.Index];
        for (int t = 0; t < 120; t++)
        {
            sim.Step(None);
            int hp = sim.Infantry.Health[victim.Index];
            if (hp < last) hits.Add(sim.Tick);
            last = hp;
        }
        Assert.True(hits.Count >= 6);
        for (int k = 1; k < hits.Count; k++) Assert.Equal(interval, hits[k] - hits[k - 1]);
    }

    // ---- speed ----------------------------------------------------------------------------------

    [Theory]
    [InlineData(0, 1000)]
    [InlineData(1, 1000)]
    [InlineData(2, 1050)]
    [InlineData(3, 1100)]
    public void Speed_ComposesWithTheAuraFactor(int rank, int permille)
    {
        var sim = NewSim(Table);
        var jeep = sim.SpawnVehicle(0, Veh("test_jeep"), new CellCoord(5, 5));
        sim.Veterancy.VehicleKillValue[jeep.Index] = KillValueOfRank[rank];
        Assert.Equal(permille, sim.MoveSpeedPermille(ActorKind.Vehicle, jeep.Index));
        sim.Effects.VehicleSpeedPermille[jeep.Index] = 1200;   // an aura of +20 %: 1200 x 1050 / 1000 = 1260
        Assert.Equal(1200 * permille / 1000, sim.MoveSpeedPermille(ActorKind.Vehicle, jeep.Index));
    }

    [Fact]
    public void Speed_RankThreeJeepCoversMoreGround()
    {
        static int Travelled(int kv)
        {
            var sim = NewSim(Table);
            var jeep = sim.SpawnVehicle(0, Veh("test_jeep"), new CellCoord(2, 16));
            sim.Veterancy.VehicleKillValue[jeep.Index] = kv;
            LeptonCoord start = sim.Units.Position[jeep.Index];
            Run(sim, 60, Move(sim, 0, ActorKind.Vehicle, jeep, 30, 16));
            return sim.Units.Position[jeep.Index].X - start.X;
        }
        int recruit = Travelled(0), hero = Travelled(3200);
        Assert.True(recruit > 0);
        Assert.True(hero > recruit * 105 / 100, $"recruit {recruit}, hero {hero}");
    }

    // ---- self-heal ------------------------------------------------------------------------------

    private static int HealSteps(int fromTick, int toTickInclusive, int slot, int interval)
    {
        int n = 0;
        for (int t = fromTick; t <= toTickInclusive; t++) if ((t + slot) % interval == 0) n++;
        return n;
    }

    [Fact]
    public void RankThree_HealsOnePercentEveryTwentyTicks_WhenQuiet()
    {
        var sim = NewSim(Table);
        var tank = sim.SpawnVehicle(0, Veh("test_tank"), new CellCoord(5, 5));   // 400 hp: 1 % = 4
        sim.Veterancy.VehicleKillValue[tank.Index] = 3200;
        sim.Units.Health[tank.Index] = 100;
        for (int t = 0; t < 200; t++) sim.Step(None);
        Assert.Equal(100 + 4 * 10, sim.Units.Health[tank.Index]);   // 200 ticks hold exactly ten heal steps
    }

    [Fact]
    public void RankTwoAndBelow_DoNotHeal()
    {
        var sim = NewSim(Table);
        var tank = sim.SpawnVehicle(0, Veh("test_tank"), new CellCoord(5, 5));
        sim.Veterancy.VehicleKillValue[tank.Index] = 3199;
        sim.Units.Health[tank.Index] = 100;
        for (int t = 0; t < 200; t++) sim.Step(None);
        Assert.Equal(100, sim.Units.Health[tank.Index]);
    }

    [Fact]
    public void Heal_StopsAtMaxHealth_AndWaitsOutTheQuietPeriod()
    {
        var sim = NewSim(Table);
        var tank = sim.SpawnVehicle(0, Veh("test_tank"), new CellCoord(5, 5));
        sim.Veterancy.VehicleKillValue[tank.Index] = 3200;
        sim.Units.Health[tank.Index] = 398;
        for (int t = 0; t < 40; t++) sim.Step(None);
        Assert.Equal(400, sim.Units.Health[tank.Index]);   // clamped

        sim.Units.Health[tank.Index] = 100;
        int start = sim.Tick;
        sim.Veterancy.VehicleLastCombat[tank.Index] = start + 1;   // stored as tick + 1
        for (int t = 0; t < 99; t++) sim.Step(None);       // ticks start .. start + 98: still inside the 100-tick quiet window
        Assert.Equal(100, sim.Units.Health[tank.Index]);
        for (int t = 0; t < 40; t++) sim.Step(None);       // ticks start + 99 .. start + 138
        int steps = HealSteps(start + 100, start + 138, tank.Index, 20);
        Assert.InRange(steps, 1, 2);
        Assert.Equal(100 + 4 * steps, sim.Units.Health[tank.Index]);
    }

    [Fact]
    public void BeingHit_ResetsTheQuietTimer()
    {
        var sim = NewSim(Table);
        var tank = sim.SpawnVehicle(0, Veh("test_tank"), new CellCoord(5, 5));
        sim.Veterancy.VehicleKillValue[tank.Index] = 3200;
        sim.Units.Health[tank.Index] = 200;
        sim.Detonate(1, 10, SimRules.IndexOf(Loaded.Sim.Warheads, "test_he", w => w.Id), sim.Units.Position[tank.Index], new ActorRef(ActorKind.Vehicle, tank), true);
        sim.Step(None);
        int hitTick = sim.Veterancy.VehicleLastCombat[tank.Index];
        Assert.Equal(sim.Tick, hitTick);   // the hit landed in the tick before the current one: tick + 1
        int hp = sim.Units.Health[tank.Index];
        Assert.True(hp < 200);
        for (int t = 0; t < 99; t++) sim.Step(None);
        Assert.Equal(hp, sim.Units.Health[tank.Index]);
    }

    [Fact]
    public void SoldierHeal_IsAtLeastOne()
    {
        var sim = NewSim(Table);
        var rifle = sim.SpawnInfantry(0, Inf("test_rifleman"), new CellCoord(5, 5));   // 50 hp: 1 % = 0.5 -> 1
        sim.Veterancy.InfantryKillValue[rifle.Index] = 3200;
        sim.Infantry.Health[rifle.Index] = 10;
        for (int t = 0; t < 100; t++) sim.Step(None);
        Assert.Equal(15, sim.Infantry.Health[rifle.Index]);   // five heal steps in 100 ticks
    }

    // ---- off / view / save ----------------------------------------------------------------------

    [Fact]
    public void Disabled_NoBonusAppliesAndNothingIsRecorded()
    {
        var sim = NewSim(new VeterancyRules());
        var shooter = sim.SpawnInfantry(0, Inf("test_rifleman"), new CellCoord(10, 10));
        var victim = sim.SpawnInfantry(1, Inf("test_rifleman"), new CellCoord(13, 10));
        sim.Infantry.Health[victim.Index] = 100_000; sim.Infantry.Health[shooter.Index] = 100_000;
        sim.Veterancy.InfantryKillValue[shooter.Index] = 99999;
        sim.Step(new[] { Attack(sim, 0, ActorKind.Infantry, shooter, ActorKind.Infantry, victim) });
        sim.Infantry.Health[victim.Index] = 100_000;
        Assert.Equal(100, FirstHit(sim, victim));
        Assert.Equal(0, sim.Veterancy.InfantryLastCombat.Sum() + sim.Veterancy.VehicleLastCombat.Sum());
    }

    [Fact]
    public void PlayerView_ExposesRank_OnlyForListedActors()
    {
        var sim = NewSim(Table);
        var own = sim.SpawnVehicle(0, Veh("test_tank"), new CellCoord(10, 10));
        var near = sim.SpawnVehicle(1, Veh("test_jeep"), new CellCoord(13, 10));
        var far = sim.SpawnVehicle(1, Veh("test_tank"), new CellCoord(28, 28));
        sim.Veterancy.VehicleKillValue[own.Index] = 3200;
        sim.Veterancy.VehicleKillValue[near.Index] = 1700;
        sim.Veterancy.VehicleKillValue[far.Index] = 3200;
        sim.Step(None);
        var list = new List<ViewActor>();
        sim.ViewFor(0).CopyOwn(list);
        Assert.Equal(3, Assert.Single(list, a => a.Id == own && a.Kind == ActorKind.Vehicle).Rank);
        sim.ViewFor(0).CopyVisible(list);
        Assert.Equal(2, Assert.Single(list, a => a.Id == near && a.Kind == ActorKind.Vehicle && a.Owner == 1).Rank);
        Assert.DoesNotContain(list, a => a.Id == far && a.Kind == ActorKind.Vehicle);   // fogged: not listed, so no rank leaks
        sim.ViewFor(1).CopyOwn(list);
        Assert.Equal(2, Assert.Single(list, a => a.Id == near && a.Kind == ActorKind.Vehicle).Rank);
    }

    [Fact]
    public void SaveLoadMidHeal_ContinuesIdentically()
    {
        var sim = NewSim(Table);
        var tank = sim.SpawnVehicle(0, Veh("test_tank"), new CellCoord(5, 5));
        var rifle = sim.SpawnInfantry(0, Inf("test_rifleman"), new CellCoord(7, 7));
        sim.Veterancy.VehicleKillValue[tank.Index] = 3300; sim.Veterancy.InfantryKillValue[rifle.Index] = 4000;
        sim.Units.Health[tank.Index] = 50; sim.Infantry.Health[rifle.Index] = 5;
        for (int t = 0; t < 70; t++) sim.Step(None);
        var ms = new MemoryStream();
        sim.Save(ms);
        var loaded = Simulation.Load(new MemoryStream(ms.ToArray()), sim.Setup);
        Assert.Equal(sim.ComputeStateHash(), loaded.ComputeStateHash());
        for (int t = 0; t < 100; t++)
        {
            sim.Step(None); loaded.Step(None);
            Assert.Equal(sim.ComputeStateHash(), loaded.ComputeStateHash());
        }
        Assert.True(sim.Units.Health[tank.Index] > 50);
        Assert.Equal(sim.Units.Health[tank.Index], loaded.Units.Health[tank.Index]);
    }
}
