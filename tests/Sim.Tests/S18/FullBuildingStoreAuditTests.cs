// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Sim.Core;
using Rts.Sim.Map;
using Rts.Sim.Tests.M3.Burrow;
using Rts.Sim.Tests.Production;
using Rts.Sim.World;
using static Rts.Sim.Rules.ProductionCategory;

namespace Rts.Sim.Tests.S18;

/// <summary>S18 audit (SIMDET-02 / SP-02 / SIM-RULES-14): what happens when the building store has no free slot.</summary>
public sealed class FullBuildingStoreAuditTests
{
    private static readonly Command[] None = Array.Empty<Command>();

    /// <summary>A BurrowWorld sim whose building store holds exactly <paramref name="buildings"/> buildings, all used by 1x1 blocks.</summary>
    private static Simulation FullBurrowSim(int buildings = 3)
    {
        var sim = new Simulation(BurrowWorld.Setup() with { Limits = new SimLimits(16, 16, buildings + 1, 16) });   // slot 0 is reserved
        for (int k = 0; k < buildings; k++) Assert.False(sim.PlaceBuilding(1, BurrowWorld.Block, new CellCoord(2 + 3 * k, 2)).IsNone);
        Assert.True(sim.PlaceBuilding(1, BurrowWorld.Block, new CellCoord(40, 2)).IsNone, "the store should now be full");
        return sim;
    }

    // M13-07 (D-205): inverted from Audit_DeployInPlace_...: the deploy is refused and the vehicle stays.
    [Fact]
    public void DeployInPlace_WithAFullBuildingStore_IsRefused_AndTheVehicleStays()
    {
        Simulation sim = FullBurrowSim();
        EntityId train = sim.SpawnVehicle(0, BurrowWorld.Train, new CellCoord(20, 20));
        int buildings = sim.Buildings.Slots.Count;
        sim.Step(new[] { BurrowWorld.Cmd(sim, CommandKind.Deploy, train, default, (int)ActorKind.Vehicle) });
        Assert.True(sim.Units.Slots.IsAlive(train));                        // the land-train is still there
        Assert.Equal(buildings, sim.Buildings.Slots.Count);                 // and no Shaft exists
        Assert.True(sim.Map.BuildingAt(new CellCoord(20, 20)).IsNone);
    }

    // M13-07: inverted from Audit_BoreBreakThrough_...: the Bore is kept (it surfaces as a unit instead).
    [Fact]
    public void BoreBreakThrough_WithAFullBuildingStore_KeepsTheBore()
    {
        Simulation sim = FullBurrowSim();
        EntityId bore = sim.SpawnVehicle(0, BurrowWorld.Bore, new CellCoord(10, 10));
        int buildings = sim.Buildings.Slots.Count;
        sim.Step(new[] { BurrowWorld.DeployAt(sim, bore, 20, 10) });
        for (int t = 0; t < 2000 && sim.Units.Slots.IsAlive(bore); t++) sim.Step(None);
        Assert.True(sim.Units.Slots.IsAlive(bore));
        Assert.Equal(buildings, sim.Buildings.Slots.Count);
        Assert.True(sim.Map.BuildingAt(new CellCoord(20, 10)).IsNone);
        Assert.Empty(sim.Tunnels.Telegraphs);
    }

    [Fact]
    public void Desired_Deploy_WithAFullBuildingStore_KeepsTheVehicle()
    {
        Simulation sim = FullBurrowSim();
        EntityId train = sim.SpawnVehicle(0, BurrowWorld.Train, new CellCoord(20, 20));
        EntityId bore = sim.SpawnVehicle(0, BurrowWorld.Bore, new CellCoord(10, 10));
        sim.Step(new[] { BurrowWorld.Cmd(sim, CommandKind.Deploy, train, default, (int)ActorKind.Vehicle), BurrowWorld.DeployAt(sim, bore, 20, 10) });
        for (int t = 0; t < 600; t++) sim.Step(None);
        Assert.True(sim.Units.Slots.IsAlive(train));
        Assert.True(sim.Units.Slots.IsAlive(bore));
    }

    /// <summary>Fixture base (HQ, power, barracks, factory) in a store with room for exactly those four plus <paramref name="spare"/>.</summary>
    private static Simulation TightBase(int spare)
    {
        var sim = new Simulation(new SimSetup(1, MapData.CreateFlat(32, 32), 2, ProductionFixture.Loaded.Sim, ProductionFixture.Loaded.RulesHash)
        { Limits = new SimLimits(64, 64, 4 + spare + 1, 64) });
        foreach (var (id, x, y) in new[] { ("test_hq", 4, 4), ("test_power", 12, 4), ("test_barracks", 8, 4), ("test_factory", 4, 8) })
            Assert.False(sim.PlaceBuilding(0, ProductionFixture.Bld(id), new CellCoord(x, y)).IsNone);
        ProductionFixture.SyncPower(sim);
        sim.Players.Cash[0] = 10000;
        return sim;
    }

    // M13-07: inverted from Audit_ReadyStructure_CannotBePlacedInAFullStore_WhileCanPlaceSaysOk: the ghost is refused with StoreFull, and Cancel (M13-06) frees the lane.
    [Fact]
    public void ReadyStructure_InAFullStore_IsRefusedWithStoreFull_AndCanBeCancelledForARefund()
    {
        Simulation sim = TightBase(spare: 0);
        int power = ProductionFixture.Bld("test_power");
        Assert.True(sim.CanBuild(0, Structure, power));                      // nothing stops the player starting it
        sim.Step(new[] { ProductionFixture.Build(sim, Structure, power) });
        ProductionFixture.RunUntil(sim, () => sim.Production.ReadyType(0, Structure) == power);
        int cash = sim.Players.Cash[0];
        var site = new CellCoord(1, 1);
        Assert.Equal(PlacementResult.StoreFull, sim.CanPlace(0, power, site));   // the ghost is red, with a reason
        Assert.False(sim.CanPlaceReady(0, power, site));
        sim.Step(new[] { ProductionFixture.Place(sim, power, site.X, site.Y) });
        Assert.Equal(4, sim.Buildings.Slots.Count);                         // nothing was placed
        Assert.True(sim.Map.BuildingAt(site).IsNone);
        Assert.Equal(power, sim.Production.ReadyType(0, Structure));        // the item still occupies the lane
        Assert.Equal(cash, sim.Players.Cash[0]);
        Assert.Equal(QueueBlockReason.StructureWaiting, sim.QueueBlockReason(0, Structure, power));
        sim.Step(new[] { ProductionFixture.Cancel(sim, Structure, Simulation.ReadyCancelPosition) });
        Assert.Equal(-1, sim.Production.ReadyType(0, Structure));
        Assert.True(sim.Players.Cash[0] > cash);                            // the price came back
    }

    [Fact]
    public void Desired_FullStore_IsReportedByCanPlace()
    {
        Simulation sim = TightBase(spare: 0);
        int power = ProductionFixture.Bld("test_power");
        Assert.Equal(PlacementResult.StoreFull, sim.CanPlace(0, power, new CellCoord(1, 1)));   // M13-07 (the CanBuild alternative was not taken)
    }

    // ---- SIM-RULES-03 --------------------------------------------------------------------------

    // M13-06 (D-205): the former Audit_ pins are inverted; the ready item is cancelled with Arg2 = -1, position 0 is only the queue front.
    [Fact]
    public void ReadyStructure_CancelledWithMinusOne_RefundsInFull_AndFreesTheLane()
    {
        Simulation sim = ProductionFixture.Base();
        int power = ProductionFixture.Bld("test_power"), barracks = ProductionFixture.Bld("test_barracks");
        int before = sim.Players.Cash[0];
        sim.Step(new[] { ProductionFixture.Build(sim, Structure, power) });
        ProductionFixture.RunUntil(sim, () => sim.Production.ReadyType(0, Structure) == power);
        sim.Step(new[] { ProductionFixture.Cancel(sim, Structure, Simulation.ReadyCancelPosition) });
        Assert.Equal(before, sim.Players.Cash[0]);
        Assert.Equal(QueueBlockReason.None, sim.QueueBlockReason(0, Structure, barracks));
        sim.Step(new[] { ProductionFixture.Build(sim, Structure, barracks) });
        Assert.Equal(1, sim.Production.QueueLength(0, Structure));
    }

    [Fact]
    public void CancelPositionZero_WithAReadyStructure_CancelsOnlyTheQueueFront_NeverTheReadyItem()
    {
        Simulation sim = ProductionFixture.Base();
        int power = ProductionFixture.Bld("test_power"), barracks = ProductionFixture.Bld("test_barracks");
        sim.Step(new[] { ProductionFixture.Build(sim, Structure, power), ProductionFixture.Build(sim, Structure, barracks) });
        ProductionFixture.RunUntil(sim, () => sim.Production.ReadyType(0, Structure) == power);
        int cash = sim.Players.Cash[0];
        sim.Step(new[] { ProductionFixture.Cancel(sim, Structure, 0) });
        Assert.Equal(0, sim.Production.QueueLength(0, Structure));
        Assert.Equal(power, sim.Production.ReadyType(0, Structure));        // the finished plant stays
        Assert.Equal(cash, sim.Players.Cash[0]);                            // the waiting barracks had paid nothing
        sim.Step(new[] { ProductionFixture.Cancel(sim, Structure, Simulation.ReadyCancelPosition) });
        Assert.Equal(-1, sim.Production.ReadyType(0, Structure));
    }

    [Fact]
    public void ReadyCancel_WithNothingReady_OrOnAUnitLane_IsIgnored()
    {
        Simulation sim = ProductionFixture.Base();
        int cash = sim.Players.Cash[0];
        sim.Step(new[] { ProductionFixture.Cancel(sim, Structure, Simulation.ReadyCancelPosition), ProductionFixture.Cancel(sim, Infantry, Simulation.ReadyCancelPosition) });
        Assert.Equal(cash, sim.Players.Cash[0]);
    }

    [Fact]
    public void Desired_ReadyStructure_CanBeCancelledForARefund()
    {
        Simulation sim = ProductionFixture.Base();
        int power = ProductionFixture.Bld("test_power");
        int before = sim.Players.Cash[0];
        sim.Step(new[] { ProductionFixture.Build(sim, Structure, power) });
        ProductionFixture.RunUntil(sim, () => sim.Production.ReadyType(0, Structure) == power);
        sim.Step(new[] { ProductionFixture.Cancel(sim, Structure, -1) });   // proposal: Arg2 = -1 means "the ready item"
        Assert.Equal(-1, sim.Production.ReadyType(0, Structure));
        Assert.Equal(before, sim.Funds(0));
    }
}
