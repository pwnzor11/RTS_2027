// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.AI;
using Rts.Data;
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
    public void ItLaunchesAnAttackWaveByMinuteFifteen()
    {
        var rig = new ExpeditionRig(Tier(2));
        rig.Run(18000, () => rig.Ai.Waves >= 1 || rig.Ai.FleetStrikes >= 1);
        log.WriteLine($"tick {rig.Sim.Tick}: waves {rig.Ai.Waves}, fleet strikes {rig.Ai.FleetStrikes}, units {rig.Ai.UnitsQueued}, buildings {rig.Buildings(0)} :: {rig.Ai.MilitaryDebug}");
        Assert.True(rig.Ai.Waves + rig.Ai.FleetStrikes >= 1, "a wave or a ship strike was launched");
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
