// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.AI;
using Rts.AI.Recruit;
using Rts.Data;
using Rts.Sim;
using Rts.Sim.Core;
using Rts.Sim.Missions;
using Rts.Sim.Rules;
using Rts.Sim.View;
using Xunit.Abstractions;

namespace AI.Tests.Tiers;

/// <summary>M3-11: one focused test per tier capability, on both factions where the capability is faction-neutral. The tier-1 profile is the
/// control: the same scenario must NOT trigger the capability when its flag is off.</summary>
public class TierCapabilityTests(ITestOutputHelper log)
{
    private static DifficultyProfile Tier(int tier) => TestWorld.Tiers[tier - 1];

    // ---- profile data -------------------------------------------------------------------------------

    [Fact]
    public void CapabilityFlagsOnlyEverGrowWithTheTier()
    {
        static bool[] Flags(DifficultyProfile p) =>
            [p.Repair, p.ReplaceHarvesters, p.RespondToAttacks, p.Scatter, p.EnemyAwareMix, p.TwoPronged, p.CounterDefences,
             p.Air, p.ActiveScouting, p.Harass, p.UseUpgrades, p.AdaptBuild, p.Superweapons, p.MultiFront, p.FocusFire];
        for (int t = 1; t < 5; t++)
        {
            bool[] lo = Flags(Tier(t)), hi = Flags(Tier(t + 1));
            for (int i = 0; i < lo.Length; i++)
                Assert.True(!lo[i] || hi[i], $"flag #{i} is on at tier {t} but off at tier {t + 1}");
            Assert.True(Tier(t + 1).DecisionIntervalTicks <= Tier(t).DecisionIntervalTicks, "decision interval never gets longer");
        }
        Assert.False(Tier(1).Repair || Tier(1).ReplaceHarvesters || Tier(1).RespondToAttacks || Tier(1).Scatter);
        Assert.True(Tier(2).Repair && Tier(2).ReplaceHarvesters && Tier(2).RespondToAttacks && Tier(2).Scatter);
        Assert.True(Tier(3).EnemyAwareMix && Tier(3).TwoPronged && Tier(3).CounterDefences && Tier(3).ExpansionTick > 0);
        Assert.True(Tier(4).Air && Tier(4).Superweapons && Tier(4).ActiveScouting && Tier(4).Harass && Tier(4).UseUpgrades && Tier(4).PredictedRetreat);
        Assert.True(Tier(5).MultiFront && Tier(5).AdaptBuild && Tier(5).DecisionIntervalTicks < Tier(4).DecisionIntervalTicks);
    }

    // ---- tier 2 -------------------------------------------------------------------------------------

    [Theory]
    [InlineData(Faction.Authority)]
    [InlineData(Faction.Directorate)]
    public void RegularReplacesADestroyedHarvester(Faction f)
    {
        var rig = new TierRig(f, Tier(2), r => r.Sim.Players.Cash[0] = 12000);
        string factory = f == Faction.Directorate ? "dir_assembly_bore" : "auth_factory";
        rig.Run(6000, () => rig.Count(0, factory) >= 1 && rig.Harvesters(0).Count > 0);
        rig.Run(200);
        List<EntityId> before = rig.Harvesters(0);
        Assert.NotEmpty(before);
        foreach (EntityId h in before) rig.Kill(h, ActorKind.Vehicle);
        rig.Run(2);
        Assert.Empty(rig.Harvesters(0));
        rig.Run(3000, () => rig.Harvesters(0).Count > 0);
        log.WriteLine($"{f}: replaced {rig.Ai.HarvestersReplaced}, harvesters now {rig.Harvesters(0).Count}");
        Assert.True(rig.Ai.HarvestersReplaced >= 1);
        Assert.NotEmpty(rig.Harvesters(0));
    }

    [Theory]
    [InlineData(Faction.Authority)]
    [InlineData(Faction.Directorate)]
    public void RecruitDoesNotReplaceAHarvester(Faction f)
    {
        var rig = new TierRig(f, Tier(1));
        rig.Run(4500);
        foreach (EntityId h in rig.Harvesters(0)) rig.Kill(h, ActorKind.Vehicle);
        rig.Run(3000);
        Assert.Equal(0, rig.Ai.HarvestersReplaced);
    }

    [Theory]
    [InlineData(Faction.Authority)]
    [InlineData(Faction.Directorate)]
    public void RegularRepairsADamagedStructure_RecruitDoesNot(Faction f)
    {
        int Repairs(int tier, out int healthBefore, out int healthAfter)
        {
            var rig = new TierRig(f, Tier(tier));
            rig.Run(1800);
            int victim = -1;
            for (int i = 1; i < rig.Sim.Buildings.Capacity && victim < 0; i++)
                if (rig.Sim.Buildings.Slots.IsIndexAlive(i) && rig.Sim.Buildings.Player[i] == 0
                    && rig.World.Sim.Buildings[rig.Sim.Buildings.TypeIndex[i]].Category == ProductionCategory.Structure
                    && !rig.World.Sim.Buildings[rig.Sim.Buildings.TypeIndex[i]].Wall && rig.World.Sim.Buildings[rig.Sim.Buildings.TypeIndex[i]].Cost > 0)
                    victim = i;
            Assert.True(victim > 0);
            rig.Sim.Buildings.Health[victim] /= 2;
            healthBefore = rig.Sim.Buildings.Health[victim];
            rig.Run(900);
            healthAfter = rig.Sim.Buildings.Slots.IsIndexAlive(victim) ? rig.Sim.Buildings.Health[victim] : 0;
            return rig.Ai.Repairs;
        }
        Assert.True(Repairs(2, out int before, out int after) >= 1);
        Assert.True(after > before, $"health {before} -> {after}");
        Assert.Equal(0, Repairs(1, out _, out _));
    }

    /// <summary>Scenario: a harvester (no "desperate" wave), a few idle soldiers at home, then a structure loses health.</summary>
    private static TierRig HomeGuardRig(Faction f, int tier, int soldiers = 3) =>
        new(f, Tier(tier), r =>
        {
            CellCoord yard = r.Yard(0);
            r.SpawnHarvester(0, f, yard);
            r.SpawnFoot(0, f, soldiers, new CellCoord(yard.X + 3, yard.Y + 4));
        });

    [Theory]
    [InlineData(Faction.Authority)]
    [InlineData(Faction.Directorate)]
    public void AllTiersDefendAStructureUnderAttack(Faction f)
    {
        int Attacks(int tier)
        {
            TierRig rig = HomeGuardRig(f, tier);
            rig.Run(700);
            int before = rig.Issue(CommandKind.AttackMove);
            for (int i = 1; i < rig.Sim.Buildings.Capacity; i++)
                if (rig.Sim.Buildings.Slots.IsIndexAlive(i) && rig.Sim.Buildings.Player[i] == 0) { rig.Sim.Buildings.Health[i] -= 40; break; }
            rig.Run(130);
            return rig.Issue(CommandKind.AttackMove) - before;
        }
        Assert.True(Attacks(2) >= 1, "idle soldiers were sent to the damaged structure");
        Assert.True(Attacks(1) >= 1, "tier 1 keeps the original defence rule");
    }

    [Theory]
    [InlineData(Faction.Authority)]
    [InlineData(Faction.Directorate)]
    public void RegularScattersUnitsHitBySplash_RecruitDoesNot(Faction f)
    {
        int Scatters(int tier)
        {
            TierRig rig = HomeGuardRig(f, tier, 4);
            // The splash lands on soldiers standing still in the open, a pair close together. A cheaper tree (D-205 Directorate numbers) sends
            // the first wave out before tick 900, so wait for the moment such a pair exists instead of assuming a fixed tick.
            var idle = new List<int>();
            for (int t = 0; t < 900 && idle.Count < 2; t += 10)
            {
                rig.Run(10);
                idle.Clear();
                for (int i = 1; i < rig.Sim.Infantry.Capacity; i++)
                    if (rig.Sim.Infantry.Slots.IsIndexAlive(i) && rig.Sim.Infantry.Player[i] == 0
                        && rig.Sim.Infantry.Mission[i] is MissionKind.Guard or MissionKind.None
                        && !ActorLayers.IsCarried(rig.Sim.LayerOf(ActorKind.Infantry, i)))   // S19: a garrisoned soldier is not in the open (D-205)
                        idle.Add(i);
                bool pair = false;
                foreach (int x in idle) foreach (int y in idle)
                    if (x != y && Math.Max(Math.Abs(rig.Sim.Infantry.Position[x].X - rig.Sim.Infantry.Position[y].X), Math.Abs(rig.Sim.Infantry.Position[x].Y - rig.Sim.Infantry.Position[y].Y)) <= 2 * 256) pair = true;
                if (!pair) idle.Clear();
            }
            Assert.True(idle.Count >= 2, $"at least two idle soldiers stand together in the open (tier {tier}: {idle.Count})");
            foreach (int i in idle)
                rig.Sim.Infantry.Health[i] -= Math.Max(1, rig.World.Sim.Infantry[rig.Sim.Infantry.TypeIndex[i]].MaxHealth / 5);
            rig.Run(90);
            return rig.Issue(CommandKind.Scatter);
        }
        Assert.True(Scatters(2) >= 1);
        Assert.Equal(0, Scatters(1));
    }

    // ---- tier 3 -------------------------------------------------------------------------------------

    [Theory]
    [InlineData(Faction.Authority)]
    [InlineData(Faction.Directorate)]
    public void VeteranExpandsWithASecondRefinery_RegularDoesNot(Faction f)
    {
        DifficultyProfile veteran = Tier(3) with { ExpansionTick = 600 };
        var rig = new TierRig(f, veteran);
        rig.Run(6500);
        string refinery = TierRig.RefineryId(f);
        log.WriteLine($"{f}: {refinery} x{rig.Count(0, refinery)}");
        Assert.True(rig.Count(0, refinery) >= 2, "a second refinery was built");
        var control = new TierRig(f, Tier(2));
        control.Run(6500);
        Assert.Equal(1, control.Count(0, refinery));
    }

    /// <summary>Scenario for the wave tests: a big idle-army at home next to two enemy structures 14 cells apart (both seen at once, so
    /// both are remembered); waves launch by the timer, not by army size.</summary>
    private static TierRig WaveRig(Faction f, DifficultyProfile profile, int soldiers = 16) =>
        new(f, profile with { AttackThreshold = 500, ExpansionTick = 0, ActiveScouting = false, Harass = false }, r =>
        {
            CellCoord yard = r.Yard(0);
            r.SpawnHarvester(0, f, yard);
            var mid = new CellCoord(yard.X + 2, yard.Y + 6);
            r.SpawnFoot(0, f, soldiers, mid);
            // two enemy structures 16+ cells apart, each with one of our soldiers beside it so both are seen (and remembered) at once
            var pa = new CellCoord(yard.X + 18, yard.Y + 2); var pb = new CellCoord(yard.X + 2, yard.Y + 20);
            r.PlaceNear(1, "auth_power", pa);
            r.PlaceNear(1, "auth_silo", pb);
            r.SpawnFoot(0, f, 1, new CellCoord(pa.X, pa.Y + 3));
            r.SpawnFoot(0, f, 1, new CellCoord(pb.X + 3, pb.Y));
        });

    [Theory]
    [InlineData(Faction.Authority)]
    [InlineData(Faction.Directorate)]
    public void VeteranSplitsABigWaveIntoTwoPronged_RegularDoesNot(Faction f)
    {
        TierRig rig = WaveRig(f, Tier(3));
        rig.Run(6000, () => rig.Ai.Waves >= 1);
        var dbgOwn = new List<ViewActor>(); var dbgKnown = new List<ViewActor>();
        rig.Sim.ViewFor(0).CopyOwn(dbgOwn, false); rig.Sim.ViewFor(0).CopyKnownEnemyBuildings(dbgKnown);
        log.WriteLine($"{f}: waves {rig.Ai.Waves} prongs {rig.Ai.Prongs} at tick {rig.Sim.Tick}; mobiles {dbgOwn.Count} known {string.Join(";", dbgKnown.Select(b => b.Cell.X + "," + b.Cell.Y))}");
        Assert.True(rig.Ai.Waves >= 1, "a wave was launched");
        Assert.True(rig.Ai.Prongs >= 1, "the wave went out as two prongs");
        TierRig control = WaveRig(f, Tier(2));
        control.Run(6000, () => control.Ai.Waves >= 1);
        Assert.True(control.Ai.Waves >= 1);
        Assert.Equal(0, control.Ai.Prongs);
    }

    [Fact]
    public void VeteranPicksTheUnitThatCountersWhatItHasSeen()
    {
        // Authority AI with a barracks and power; the enemy parks tanks (heavy armour) in its sight.
        var rig = new TierRig(Faction.Authority, Tier(3), r =>
        {
            CellCoord yard = r.Yard(0);
            r.PlaceNear(0, "auth_power", yard);
            r.PlaceNear(0, "auth_barracks", yard);
            for (int i = 0; i < 4; i++) r.Sim.SpawnVehicle(1, r.VehicleType("auth_tank"), new CellCoord(yard.X + 5 + i, yard.Y + 5));
        });
        rig.Run(5);
        var know = new RecruitKnowledge();
        PlayerView view = rig.Sim.ViewFor(0);
        know.Refresh(view, "auth_conyard");
        var planner = new TierPlanner(Tier(3), RecruitFaction.For(Faction.Authority));
        var known = new List<ViewActor>();
        planner.Observe(know, known);
        Assert.True(planner.SeenEnemy);
        Assert.True(planner.HeavyShare >= 60, "the enemy army is mostly heavy armour: " + planner.HeavyShare);

        int rifle = rig.InfantryType("auth_rifleman"), rocket = rig.InfantryType("auth_rocketeer");
        Assert.True(know.Buildable(ProductionCategory.Infantry, rifle) && know.Buildable(ProductionCategory.Infantry, rocket));
        var rng = new AiRandom(7, 0);
        int[] bag = [rifle, rocket];
        int rockets = 0;
        for (int i = 0; i < 60; i++)
            if (planner.PickUnit(know, ProductionCategory.Infantry, bag, rifle, rng) == rocket) rockets++;
        log.WriteLine($"rocketeers picked {rockets}/60 against heavy armour");
        Assert.True(rockets >= 30, "most picks counter heavy armour with the rocketeer");

        // without the flag the bag decides
        var plain = new TierPlanner(Tier(2), RecruitFaction.For(Faction.Authority));
        plain.Observe(know, known);
        for (int i = 0; i < 20; i++) Assert.Equal(rifle, plain.PickUnit(know, ProductionCategory.Infantry, bag, rifle, rng));
    }

    // ---- tier 4 -------------------------------------------------------------------------------------

    [Fact]
    public void ElitePicksAnAircraftWhenItCanBuildOne()
    {
        var rig = new TierRig(Faction.Directorate, Tier(4) with { ActiveScouting = false, Harass = false }, r =>
        {
            r.Sim.Players.Cash[0] = 12000;
            CellCoord yard = r.Yard(0);
            r.PlaceNear(0, "dir_reactor_vent", yard);
            r.PlaceNear(0, "dir_reactor_vent", yard);
            r.PlaceNear(0, "dir_smelter_vent", yard);
            r.PlaceNear(0, "dir_assembly_bore", yard);
            r.PlaceNear(0, "dir_isotope_works", yard);
            r.PlaceNear(0, "dir_mooring", yard);
        });
        rig.Run(2500, () => rig.Ai.AircraftOrdered >= 1);
        Assert.True(rig.Ai.AircraftOrdered >= 1, "a dirigible was queued");
        int air = rig.VehicleType("dir_dirigible");
        rig.Run(1500);
        int flying = 0;
        for (int i = 1; i < rig.Sim.Units.Capacity; i++)
            if (rig.Sim.Units.Slots.IsIndexAlive(i) && rig.Sim.Units.Player[i] == 0 && rig.Sim.Units.TypeIndex[i] == air) flying++;
        log.WriteLine($"dirigibles alive: {flying}, ordered {rig.Ai.AircraftOrdered}");
        Assert.True(flying >= 1);
        // Authority has no aircraft: the same flag does nothing there
        var auth = new TierRig(Faction.Authority, Tier(4));
        auth.Run(3000);
        Assert.Equal(0, auth.Ai.AircraftOrdered);
    }

    [Fact]
    public void EliteFiresAReadySuperweaponAtKnownEnemyStructures()
    {
        EntityId sunrise = default;
        var rig = new TierRig(Faction.Directorate, Tier(4), r =>
        {
            CellCoord yard = r.Yard(0);
            r.PlaceNear(0, "dir_reactor_vent", yard);
            r.PlaceNear(0, "dir_reactor_vent", yard);
            r.PlaceNear(0, "dir_smelter_vent", yard);
            r.PlaceNear(0, "dir_isotope_works", yard);
            sunrise = r.PlaceNear(0, "dir_sunrise", yard);
            // two enemy structures a few cells from our shaft: seen at once, and inside the entrance range of the weapon
            r.PlaceNear(1, "auth_power", new CellCoord(yard.X + 7, yard.Y));
            r.PlaceNear(1, "auth_silo", new CellCoord(yard.X + 7, yard.Y + 4));
        });
        int total = rig.World.Sim.Buildings[rig.BuildingType("dir_sunrise")].SuperweaponChargeTicks;
        Assert.True(total > 0);
        rig.Run(40);
        int enemyBefore = rig.Count(1, "auth_power") + rig.Count(1, "auth_silo");
        Assert.Equal(2, enemyBefore);
        rig.Sim.Effects.BuildingCharge[sunrise.Index] = total;
        rig.Run(120);
        log.WriteLine($"special orders: {rig.Ai.SuperweaponsFired}; enemy structures left {rig.Count(1, "auth_power") + rig.Count(1, "auth_silo")}");
        Assert.True(rig.Ai.SuperweaponsFired >= 1);
        Assert.True(rig.Count(1, "auth_power") + rig.Count(1, "auth_silo") < enemyBefore, "the blast destroyed enemy structures");
        Assert.True(rig.Sim.Effects.BuildingCharge[sunrise.Index] < total, "the charge was spent");
    }

    [Theory]
    [InlineData(Faction.Authority)]
    [InlineData(Faction.Directorate)]
    public void EliteRetreatsAGroupThatHasLostTooMuch_RegularKeepsFighting(Faction f)
    {
        int Retreats(DifficultyProfile p, out int waves)
        {
            TierRig rig = WaveRig(f, p);
            rig.Run(6000, () => rig.Ai.Waves >= 1);
            waves = rig.Ai.Waves;
            Assert.True(waves >= 1);
            // 30 percent health: above the single-unit line (25) so nobody retreats alone, below the group line (25 + 10)
            for (int i = 1; i < rig.Sim.Infantry.Capacity; i++)
                if (rig.Sim.Infantry.Slots.IsIndexAlive(i) && rig.Sim.Infantry.Player[i] == 0)
                    rig.Sim.Infantry.Health[i] = Math.Max(1, rig.World.Sim.Infantry[rig.Sim.Infantry.TypeIndex[i]].MaxHealth * 30 / 100);
            for (int i = 1; i < rig.Sim.Units.Capacity; i++)
                if (rig.Sim.Units.Slots.IsIndexAlive(i) && rig.Sim.Units.Player[i] == 0)
                    rig.Sim.Units.Health[i] = Math.Max(1, rig.World.Sim.Vehicles[rig.Sim.Units.TypeIndex[i]].MaxHealth * 30 / 100);
            rig.Run(120);
            log.WriteLine($"{f}: group retreats {rig.Ai.GroupRetreats}, single retreats {rig.Ai.Retreats}");
            Assert.Equal(0, rig.Ai.Retreats);
            return rig.Ai.GroupRetreats;
        }
        Assert.True(Retreats(Tier(4) with { RetreatHealthPct = 25 }, out _) >= 1);   // s12: the shipped elite profile has the health retreat off (measured), the capability is still gated on the flag
        Assert.Equal(0, Retreats(Tier(3), out _));
    }

    [Theory]
    [InlineData(Faction.Authority)]
    [InlineData(Faction.Directorate)]
    public void EliteSendsAUnitThatIsNearlyDeadHome(Faction f)
    {
        var rig = new TierRig(f, Tier(4) with { ActiveScouting = false, Harass = false, RetreatHealthPct = 25 }, r =>
        {
            CellCoord yard = r.Yard(0);
            r.SpawnHarvester(0, f, yard);
            r.SpawnFoot(0, f, 3, new CellCoord(yard.X + 3, yard.Y + 4));
        });
        rig.Run(600);
        for (int i = 1; i < rig.Sim.Infantry.Capacity; i++)
            if (rig.Sim.Infantry.Slots.IsIndexAlive(i) && rig.Sim.Infantry.Player[i] == 0)
            {
                rig.Sim.Infantry.Health[i] = Math.Max(1, rig.World.Sim.Infantry[rig.Sim.Infantry.TypeIndex[i]].MaxHealth / 10);
                break;
            }
        rig.Run(60);
        Assert.Equal(1, rig.Ai.Retreats);
    }

    [Theory]
    [InlineData(Faction.Authority)]
    [InlineData(Faction.Directorate)]
    public void EliteScoutsAndRaidsAHarvesterItSaw(Faction f)
    {
        // a lone enemy harvester wanders in our sight; the raiding party must be sent against it   // s13: raiders wait for a 30-unit army (14 for Directorate)
        var rig = new TierRig(f, Tier(4) with { AttackThreshold = 500, ExpansionTick = 0 }, r =>
        {
            CellCoord yard = r.Yard(0);
            r.SpawnHarvester(0, f, yard);
            r.SpawnFoot(0, f, 32, new CellCoord(yard.X + 2, yard.Y + 5));
            r.Sim.SpawnVehicle(1, r.VehicleType("auth_scraper"), new CellCoord(yard.X + 9, yard.Y + 5));
        });
        rig.Run(900);
        log.WriteLine($"{f}: scouts {rig.Ai.Scouts}, raid parties {rig.Ai.RaidParties}, strikes {rig.Ai.RaidStrikes}");
        Assert.True(rig.Ai.Scouts >= 1, "a scout was sent");
        Assert.True(rig.Ai.RaidParties >= 1, "a raiding party was formed");
        Assert.True(rig.Ai.RaidStrikes >= 1, "the raiders attacked the harvester");
    }

    [Theory]
    [InlineData(Faction.Authority)]
    [InlineData(Faction.Directorate)]
    public void EliteResearchesAnUpgradeWhenItCan(Faction f)
    {
        if (f == Faction.Directorate && TestWorld.Rules.Sim.Upgrades.All(u => u.Faction != Faction.Directorate)) return;   // no Directorate upgrade exists yet
        var rig = new TierRig(f, Tier(4) with { AttackThreshold = 500, ExpansionTick = 0 }, r =>
        {
            CellCoord yard = r.Yard(0);
            r.SpawnHarvester(0, f, yard);
            r.SpawnFoot(0, f, 14, new CellCoord(yard.X + 2, yard.Y + 5));   // M4-12: research waits for a 12-unit army
            r.Sim.Players.Cash[0] = 20000;
        });
        rig.Run(5000, () => rig.Ai.UpgradesOrdered >= 1);
        log.WriteLine($"{f}: upgrades ordered {rig.Ai.UpgradesOrdered}");
        Assert.True(rig.Ai.UpgradesOrdered >= 1);
    }
}
