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

    // S18 audit: pins current behaviour of SIMDET-02; flip the assertion when fixed.
    [Fact]
    public void Audit_DeployInPlace_WithAFullBuildingStore_RemovesTheVehicleAndBuildsNothing()
    {
        Simulation sim = FullBurrowSim();
        EntityId train = sim.SpawnVehicle(0, BurrowWorld.Train, new CellCoord(20, 20));
        int buildings = sim.Buildings.Slots.Count;
        sim.Step(new[] { BurrowWorld.Cmd(sim, CommandKind.Deploy, train, default, (int)ActorKind.Vehicle) });
        Assert.False(sim.Units.Slots.IsAlive(train));                       // the land-train is gone
        Assert.Equal(buildings, sim.Buildings.Slots.Count);                 // and no Shaft exists
        Assert.True(sim.Map.BuildingAt(new CellCoord(20, 20)).IsNone);
    }

    // S18 audit: pins current behaviour of SIMDET-02; flip the assertion when fixed.
    [Fact]
    public void Audit_BoreBreakThrough_WithAFullBuildingStore_LosesTheBore()
    {
        Simulation sim = FullBurrowSim();
        EntityId bore = sim.SpawnVehicle(0, BurrowWorld.Bore, new CellCoord(10, 10));
        int buildings = sim.Buildings.Slots.Count;
        sim.Step(new[] { BurrowWorld.DeployAt(sim, bore, 20, 10) });
        for (int t = 0; t < 2000 && sim.Units.Slots.IsAlive(bore); t++) sim.Step(None);
        Assert.False(sim.Units.Slots.IsAlive(bore));
        Assert.Equal(buildings, sim.Buildings.Slots.Count);
        Assert.True(sim.Map.BuildingAt(new CellCoord(20, 10)).IsNone);
        Assert.Empty(sim.Tunnels.Telegraphs);
    }

    [Fact(Skip = "S18 SIMDET-02: awaiting owner decision")]
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

    // S18 audit: pins current behaviour of SIMDET-02 / SP-02; flip the assertion when fixed.
    [Fact]
    public void Audit_ReadyStructure_CannotBePlacedInAFullStore_WhileCanPlaceSaysOk()
    {
        Simulation sim = TightBase(spare: 0);
        int power = ProductionFixture.Bld("test_power");
        Assert.True(sim.CanBuild(0, Structure, power));                      // nothing stops the player starting it
        sim.Step(new[] { ProductionFixture.Build(sim, Structure, power) });
        ProductionFixture.RunUntil(sim, () => sim.Production.ReadyType(0, Structure) == power);
        int cash = sim.Players.Cash[0];
        var site = new CellCoord(1, 1);
        Assert.Equal(PlacementResult.Ok, sim.CanPlace(0, power, site));     // the ghost is green
        Assert.True(sim.CanPlaceReady(0, power, site));
        sim.Step(new[] { ProductionFixture.Place(sim, power, site.X, site.Y) });
        Assert.Equal(4, sim.Buildings.Slots.Count);                         // nothing was placed
        Assert.True(sim.Map.BuildingAt(site).IsNone);
        Assert.Equal(power, sim.Production.ReadyType(0, Structure));        // the item still occupies the lane
        Assert.Equal(cash, sim.Players.Cash[0]);                            // paid in full, no refund
        Assert.Equal(QueueBlockReason.StructureWaiting, sim.QueueBlockReason(0, Structure, power));
    }

    [Fact(Skip = "S18 SIMDET-02 / SP-02: awaiting owner decision")]
    public void Desired_FullStore_IsReportedByCanPlace()
    {
        Simulation sim = TightBase(spare: 0);
        int power = ProductionFixture.Bld("test_power");
        Assert.NotEqual(PlacementResult.Ok, sim.CanPlace(0, power, new CellCoord(1, 1)));
        Assert.False(sim.CanBuild(0, Structure, power));   // or: refuse to start a structure nobody can place
    }

    // ---- SIM-RULES-03 --------------------------------------------------------------------------

    // S18 audit: pins current behaviour of SIM-RULES-03; flip the assertion when fixed.
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Audit_ReadyStructure_CannotBeCancelledOrRefunded_AndBlocksItsLane(int cancelPosition)
    {
        Simulation sim = ProductionFixture.Base();
        int power = ProductionFixture.Bld("test_power"), barracks = ProductionFixture.Bld("test_barracks");
        sim.Step(new[] { ProductionFixture.Build(sim, Structure, power) });
        ProductionFixture.RunUntil(sim, () => sim.Production.ReadyType(0, Structure) == power);
        int cash = sim.Players.Cash[0];
        sim.Step(new[] { ProductionFixture.Cancel(sim, Structure, cancelPosition) });
        Assert.Equal(power, sim.Production.ReadyType(0, Structure));        // still ready
        Assert.Equal(cash, sim.Players.Cash[0]);                            // nothing refunded
        sim.Step(new[] { ProductionFixture.Build(sim, Structure, barracks) });
        Assert.Equal(0, sim.Production.QueueLength(0, Structure));          // and the lane accepts nothing new
        Assert.Equal(QueueBlockReason.StructureWaiting, sim.QueueBlockReason(0, Structure, barracks));
        ProductionFixture.Run(sim, 2000);                                   // 100 s later: unchanged, it never times out
        Assert.Equal(power, sim.Production.ReadyType(0, Structure));
        Assert.Equal(cash, sim.Players.Cash[0]);
    }

    // S18 audit: pins current behaviour of SIM-RULES-03; flip the assertion when fixed.
    [Fact]
    public void Audit_QueuedItemBehindAReadyStructure_IsCancelledInstead_WhenCancelPositionZeroIsSent()
    {
        Simulation sim = ProductionFixture.Base();
        int power = ProductionFixture.Bld("test_power"), barracks = ProductionFixture.Bld("test_barracks");
        sim.Step(new[] { ProductionFixture.Build(sim, Structure, power), ProductionFixture.Build(sim, Structure, barracks) });
        ProductionFixture.RunUntil(sim, () => sim.Production.ReadyType(0, Structure) == power);
        Assert.Equal(1, sim.Production.QueueLength(0, Structure));
        sim.Step(new[] { ProductionFixture.Cancel(sim, Structure, 0) });
        Assert.Equal(0, sim.Production.QueueLength(0, Structure));          // the waiting barracks went
        Assert.Equal(power, sim.Production.ReadyType(0, Structure));        // the finished plant stays
    }

    [Fact(Skip = "S18 SIM-RULES-03: awaiting owner decision")]
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
