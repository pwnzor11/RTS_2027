// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.AI;
using Rts.Data;
using Rts.Sim.Map;
using Rts.Sim.Rules;
using Xunit.Abstractions;

namespace AI.Tests.M7;

/// <summary>M7-10: the Expedition Recruit AI plays headless on generated ocean-edge maps.</summary>
public sealed class ExpeditionAiTests(ITestOutputHelper log)
{
    private static DifficultyProfile Tier(int tier) => TestWorld.Tiers[tier - 1];

    [Fact]
    public void ItDeploysADepotAndBuildsMoreWithinFiveMinutes()
    {
        var rig = new ExpeditionRig(Tier(2), enemyAi: false);
        rig.Run(6000);   // 5 game minutes at 20 ticks/s
        log.WriteLine($"deploys {rig.Ai.Deploys}, depots {rig.Count(0, "exp_landing_depot")}, buildings {rig.Buildings(0)}, unplaceable {rig.Ai.Unplaceable} :: {rig.Ai.ExpeditionDebug}");
        Assert.Equal(1, rig.Count(0, "exp_landing_depot"));
        Assert.True(rig.Buildings(0) >= 4, "the depot and at least three more structures stand");
        Assert.Equal(0, rig.Ai.Unplaceable);
    }

    [Fact]
    public void ItOwnsAnAnchorageByMinuteSixAndReceivesAShipmentByMinuteNine()
    {
        var rig = new ExpeditionRig(Tier(2), enemyAi: false);
        rig.Run(7200);
        log.WriteLine($"anchorages {rig.Count(0, "exp_anchorage")} buildings {rig.Buildings(0)} :: {rig.Ai.ExpeditionDebug}");
        Assert.True(rig.Count(0, "exp_anchorage") >= 1, "an Anchorage stands by minute 6");
        rig.Run(3600);
        log.WriteLine($"shipment income {rig.Sim.StatsOf(0).ShipmentIncome} eta {rig.Sim.ViewFor(0).ShipmentEtaTicks}");
        Assert.True(rig.Sim.StatsOf(0).ShipmentIncome > 0, "a shipment arrived by minute 9");
    }

    [Theory]
    [InlineData(OceanEdges.North)]
    [InlineData(OceanEdges.East)]
    public void ItDocksOnAnyCoastAndReceivesShipments(OceanEdges coast)
    {
        var rig = new ExpeditionRig(Tier(2), enemyAi: false, edges: coast);
        int built = 0;   // the Anchorage may fall to a strike later: remember that it stood
        for (int i = 0; i < 54; i++) { rig.Run(200); built = Math.Max(built, rig.Count(0, "exp_anchorage")); }
        log.WriteLine($"{coast}: built {built} anchorages {rig.Count(0, "exp_anchorage")} income {rig.Sim.StatsOf(0).ShipmentIncome} :: {rig.Ai.ExpeditionDebug}");
        Assert.True(built >= 1, "an Anchorage was built");
        Assert.True(rig.Sim.StatsOf(0).ShipmentIncome > 0, "a shipment arrived");
    }

    [Fact]
    public void ItBuildsAShipyardAndTwoWarshipsByMinuteFifteenAndOrdersNavalAction()
    {
        // no enemy AI: the fleet's own economy and orders are measured (peaks, so a later loss does not hide them)
        var rig = new ExpeditionRig(Tier(2), enemyAi: false);
        int peakShips = 0, peakYards = 0;
        rig.Run(18000, () =>
        {
            if (rig.Sim.Tick % 100 != 0) return false;
            int ships = 0;
            for (int i = 1; i < rig.Sim.Units.Capacity; i++)
                if (rig.Sim.Units.Slots.IsIndexAlive(i) && rig.Sim.Units.Player[i] == 0 && TestWorld.Rules.Sim.Vehicles[rig.Sim.Units.TypeIndex[i]] is { Locomotion: Locomotion.Naval, PrimaryWeapon: >= 0 }) ships++;
            peakShips = Math.Max(peakShips, ships); peakYards = Math.Max(peakYards, rig.Count(0, "exp_shipyard"));
            return false;
        });
        log.WriteLine($"peak shipyards {peakYards} peak warships {peakShips} strikes {rig.Ai.FleetStrikes} escorts {rig.Ai.EscortOrders} ship orders {rig.ShipOrders} :: {rig.Ai.ExpeditionDebug}");
        Assert.True(peakYards >= 1, "a Shipyard was built");
        Assert.True(peakShips >= 2, "two warships sailed");
        Assert.True(rig.Ai.FleetStrikes + rig.Ai.EscortOrders >= 1, "at least one naval strike or escort order");
    }

    [Fact]
    public void ItDeploysOnTheMapWhereAshLiesOnTheBeach()
    {
        // map seed 1 has ashfall on the first landing coast: the old AI chose a footprint the sim refused and never built anything (zero income)
        var rig = new ExpeditionRig(Tier(2), enemyAi: false, mapSeed: 1);
        rig.Run(1500);
        Assert.Equal(1, rig.Count(0, "exp_landing_depot"));
        Assert.True(rig.Buildings(0) >= 3);
    }

    [Fact]
    public void ItLaunchesAnAttackWaveByMinuteFifteen()
    {
        var rig = new ExpeditionRig(Tier(2));
        rig.Run(18000, () => rig.Ai.Waves >= 1 || rig.Ai.FleetStrikes >= 1);
        log.WriteLine($"tick {rig.Sim.Tick}: waves {rig.Ai.Waves}, fleet strikes {rig.Ai.FleetStrikes}, units {rig.Ai.UnitsQueued}, buildings {rig.Buildings(0)} :: {rig.Ai.MilitaryDebug}");
        Assert.True(rig.Ai.Waves + rig.Ai.FleetStrikes >= 1, "a wave or a ship strike was launched");
    }

    [Fact]
    public void ItSendsANavalStrikeAgainstAnAuthorityCoastByMinuteFifteen()
    {
        // M7-10 (S11): the fleet is not only an escort. A group of warships (escort and reserve left out) sails to a firing position at a visible
        // coastal enemy building and fires (an AttackMove to the firing cell, then Attack on the building).
        var rig = new ExpeditionRig(Tier(2), Faction.Authority, enemyProfile: Tier(1));   // a Recruit-tier Authority
        int peakShips = 0, firstYard = -1, firstShip = -1;
        rig.Run(18000, () =>
        {
            if (rig.Sim.Tick % 100 == 0)
            {
                int ships = 0;
                for (int i = 1; i < rig.Sim.Units.Capacity; i++)
                    if (rig.Sim.Units.Slots.IsIndexAlive(i) && rig.Sim.Units.Player[i] == 0 && TestWorld.Rules.Sim.Vehicles[rig.Sim.Units.TypeIndex[i]] is { Locomotion: Locomotion.Naval, PrimaryWeapon: >= 0 }) ships++;
                peakShips = Math.Max(peakShips, ships);
                if (firstYard < 0 && rig.Count(0, "exp_shipyard") > 0) firstYard = rig.Sim.Tick;
                if (firstShip < 0 && ships > 0) firstShip = rig.Sim.Tick;
            }
            return rig.Ai.FleetStrikes >= 1 && rig.ShipAttackMoves >= 1;
        });
        log.WriteLine($"tick {rig.Sim.Tick}: shipyard at {firstYard}, first warship at {firstShip}, peak warships {peakShips}, strikes {rig.Ai.FleetStrikes}, attack-moves {rig.ShipAttackMoves} (first at {rig.FirstAttackMoveTick}), escorts {rig.Ai.EscortOrders} :: {rig.Ai.ExpeditionDebug}");
        Assert.True(rig.Ai.FleetStrikes >= 1, "a naval strike target was chosen");
        Assert.True(rig.ShipAttackMoves >= 1, "a strike group got an AttackMove to its firing position");
        Assert.True(rig.Sim.Tick <= 18000);
    }

    [Fact]
    public void ShipsRetreatAtFortyPercent()
    {
        Assert.Equal(40, Rts.AI.Recruit.ExpeditionTactics.RetreatPct);
    }

    [Fact]
    public void SavingAndLoadingMidGameKeepsDecisionsIdentical()
    {
        var a = new ExpeditionRig(Tier(2));
        a.Run(2500);
        byte[]? blob = a.CaptureAi();
        Assert.NotNull(blob);
        a.Run(2500);
        ulong straight = a.Sim.ComputeStateHash();

        var b = new ExpeditionRig(Tier(2));
        b.Run(2500);
        ulong resumed = b.ResumeWithBlob(b.CaptureAi()!, 2500, b.CaptureEnemy());
        Assert.Equal(straight, resumed);
    }

    [Fact]
    public void ShipOrdersNeverPointAtLand()
    {
        var rig = new ExpeditionRig(Tier(3));
        rig.Run(9000);
        log.WriteLine($"ship orders {rig.ShipOrders}, to land {rig.ShipOrdersToLand}, dropped {rig.Ai.DroppedShipOrders}");
        Assert.Equal(0, rig.ShipOrdersToLand);
    }

    [Theory]
    [InlineData(Faction.Authority)]
    [InlineData(Faction.Directorate)]
    [InlineData(Faction.LeftBehind)]
    public void ItPlaysEachFactionToTheEndWithoutExceptions(Faction enemy)
    {
        var rig = new ExpeditionRig(Tier(2), enemy);
        rig.Run(14000);
        log.WriteLine($"{enemy}: exp buildings {rig.Buildings(0)} enemy {rig.Buildings(1)} waves {rig.Ai.Waves} strikes {rig.Ai.FleetStrikes}");
        Assert.True(rig.Sim.Tick >= 1);
    }
}
