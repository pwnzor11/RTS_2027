// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Data;
using Rts.Sim.Core;
using Rts.Sim.Map;
using Rts.Sim.Production;
using Rts.Sim.Rules;
using static Rts.Sim.Tests.Production.ProductionFixture;

namespace Rts.Sim.Tests.S44;

/// <summary>S44 M-03 (the Drydock builds and repairs heavy ships) and M-12 (bay repair inside a landing craft or Skyhook), on the shipped rules.
/// Map: 40x40, land x &lt; 10, deep water x &gt;= 10. Shipyard at (10,2) (3x3), Drydock at (10,8) (4x3, both touching the shore; within the shipyard's beachhead radius).</summary>
public sealed class DrydockTests
{
    private const int Size = 40, Coast = 10;

    private static readonly RuleSet Loaded = RulesLoader.Load(RulesLocator.FindRulesDir(AppContext.BaseDirectory)!);
    private static int Veh(string id) => SimRules.IndexOf(Loaded.Sim.Vehicles, id, v => v.Id);
    private static int Inf(string id) => SimRules.IndexOf(Loaded.Sim.Infantry, id, v => v.Id);
    private static int Bld(string id) => SimRules.IndexOf(Loaded.Sim.Buildings, id, v => v.Id);

    private static SimSetup NewSetup()
    {
        var cells = new LandType[Size * Size];
        for (int y = 0; y < Size; y++) for (int x = 0; x < Size; x++) cells[y * Size + x] = x >= Coast ? LandType.Water : LandType.Clear;
        var map = new MapData("coast", "rust_belt", Size, Size, new ushort[Size * Size], cells, new byte[Size * Size], new OverlayKind[Size * Size],
            new byte[Size * Size], new[] { new CellCoord(1, 1), new CellCoord(Size - 2, Size - 2) });
        return new SimSetup(1, map, 2, Loaded.Sim with { Supply = Loaded.Sim.Supply with { Enabled = false } }, Loaded.RulesHash);   // no supply cap in the way
    }

    private static Simulation NewWorld(bool shipyard = true, bool drydock = true, int cash = 50000)
    {
        var sim = new Simulation(NewSetup());
        if (drydock || shipyard) Assert.False(sim.PlaceBuilding(0, Bld("exp_landing_depot"), new CellCoord(6, 6)).IsNone, "depot (beachhead source)");
        if (shipyard)   // the tier gates: Plotting Room (tier 2) and Core Hall (tier 3)
        {
            Assert.False(sim.PlaceBuilding(0, Bld("exp_plotting_room"), new CellCoord(2, 10)).IsNone, "plotting room");
            Assert.False(sim.PlaceBuilding(0, Bld("exp_core_hall"), new CellCoord(2, 14)).IsNone, "core hall");
            Assert.False(sim.PlaceBuilding(0, Bld("exp_shipyard"), new CellCoord(10, 2)).IsNone, "shipyard");
        }
        if (drydock) Assert.False(sim.PlaceBuilding(0, Bld("exp_drydock"), new CellCoord(10, 8)).IsNone, "drydock");
        sim.Players.PowerOut[0] = 1000; sim.Players.PowerUse[0] = 0;
        sim.Players.Cash[0] = cash;
        return sim;
    }

    private static List<int> Units(Simulation sim)
    {
        var l = new List<int>();
        for (int i = 1; i < sim.Units.Capacity; i++) if (sim.Units.Slots.IsIndexAlive(i)) l.Add(i);
        return l;
    }

    private static void WaitFor(Simulation sim, int type, int max = 12000)
    {
        for (int i = 0; i < max; i++) { sim.Step(None); if (Find(sim, type) >= 0) return; }
        int lane = ProductionState.Lane(0, ProductionCategory.Vehicle);
        Assert.Fail($"unit not built: queue={sim.Production.QueueLength(0, ProductionCategory.Vehicle)} cash={sim.Players.Cash[0]} power={sim.Players.PowerOut[0]}/{sim.Players.PowerUse[0]} "
            + $"defeated={sim.Players.Defeated[0]} unlocked={sim.IsUnlocked(0, ProductionCategory.Vehicle, type)} block={sim.QueueBlockReason(0, ProductionCategory.Vehicle, type)} tick={sim.Tick} "
            + $"ticks={sim.Production.Ticks[lane]}/{sim.Production.Total[lane]} stalled={sim.Production.Stalled[lane]} held={sim.Production.Held[lane]} units={Units(sim).Count}");
    }

    private static int Find(Simulation sim, int type) => Units(sim).FirstOrDefault(i => sim.Units.TypeIndex[i] == type, -1);

    [Fact]
    public void DataIsWired()
    {
        BuildingType dd = Loaded.Sim.Buildings[Bld("exp_drydock")];
        Assert.True(dd.HeavyYard);
        Assert.Equal(ProductionCategory.Vehicle, dd.Produces);
        Assert.True(dd.ShipRepairPermille > 0);
        Assert.Equal("exp_drydock", Loaded.Sim.Vehicles[Veh("exp_cruiser")].BuiltAt);
        Assert.Equal("exp_drydock", Loaded.Sim.Vehicles[Veh("exp_carrier")].BuiltAt);
        Assert.Equal("", Loaded.Sim.Vehicles[Veh("exp_destroyer")].BuiltAt);
        Assert.True(Loaded.Sim.Vehicles[Veh("exp_landing_craft")].BayRepairPermille > 0);
        Assert.True(Loaded.Sim.Vehicles[Veh("exp_skyhook")].BayRepairPermille > 0);
        Assert.Equal(0, Loaded.Sim.Vehicles[Veh("exp_cruiser")].BayRepairPermille);
    }

    [Fact]
    public void ACruiserIsBuiltAtTheDrydock_NotTheShipyard()
    {
        Simulation sim = NewWorld();
        int cruiser = Veh("exp_cruiser");
        Assert.Equal(QueueBlockReason.None, sim.QueueBlockReason(0, ProductionCategory.Vehicle, cruiser));
        Step(sim, Build(sim, ProductionCategory.Vehicle, cruiser));
        WaitFor(sim, cruiser);
        int u = Find(sim, cruiser);
        CellCoord c = sim.Units.Position[u].ToCell();
        Assert.Equal(LandType.Water, sim.Map.LandAt(c));
        Assert.InRange(c.Y, 7, 14);   // around the drydock (12..15, 8..10), not the shipyard at y 2..4
        Assert.InRange(c.X, 8, 20);
    }

    [Fact]
    public void AShipyardAloneCannotBuildAHeavyShip_AndADrydockAloneIsNoGeneralFactory()
    {
        Simulation noDock = NewWorld(drydock: false);
        Assert.False(noDock.IsUnlocked(0, ProductionCategory.Vehicle, Veh("exp_cruiser")));   // prerequisite exp_drydock

        Simulation dockOnly = NewWorld(shipyard: false);
        Assert.Equal(QueueBlockReason.NoFactory, dockOnly.QueueBlockReason(0, ProductionCategory.Vehicle, Veh("exp_cruiser")));
        Assert.Equal(QueueBlockReason.NoFactory, dockOnly.QueueBlockReason(0, ProductionCategory.Vehicle, Veh("exp_destroyer")));
    }

    [Fact]
    public void ADestroyerStillLeavesTheShipyard_EvenWithADrydock()
    {
        Simulation sim = NewWorld();
        int destroyer = Veh("exp_destroyer");
        Step(sim, Build(sim, ProductionCategory.Vehicle, destroyer));
        WaitFor(sim, destroyer);
        CellCoord c = sim.Units.Position[Find(sim, destroyer)].ToCell();
        Assert.True(c.Y <= 6, "left the shipyard at y 2..4, not the drydock at y 8..10; was " + c);
    }

    [Fact]
    public void TheDrydockRepairsANearbyShipSlowlyAtACost_AndNotAFarOne()
    {
        Simulation sim = NewWorld();
        int cruiser = Veh("exp_cruiser");
        VehicleType vt = Loaded.Sim.Vehicles[cruiser];
        EntityId near = sim.SpawnVehicle(0, cruiser, new CellCoord(15, 9));
        EntityId far = sim.SpawnVehicle(0, cruiser, new CellCoord(30, 30));
        sim.Units.Health[near.Index] = vt.MaxHealth / 2; sim.Units.Health[far.Index] = vt.MaxHealth / 2;
        int cash0 = sim.Players.Cash[0];
        int hp0 = sim.Units.Health[near.Index];

        Run(sim, 100);
        int rate = Loaded.Sim.Buildings[Bld("exp_drydock")].ShipRepairPermille;
        int perPeriod = Math.Max(1, vt.MaxHealth * rate / 1000);
        int healed = sim.Units.Health[near.Index] - hp0;
        Assert.InRange(healed, perPeriod * 9, perPeriod * 11);   // 10 periods in 100 ticks
        Assert.Equal(vt.MaxHealth / 2, sim.Units.Health[far.Index]);
        Assert.True(sim.Players.Cash[0] < cash0, "repair costs credits");
        long expectedMax = (long)healed * Loaded.Sim.Economy.RepairCostPermille * vt.Cost / ((long)vt.MaxHealth * 1000) + 10;
        Assert.InRange(cash0 - sim.Players.Cash[0], 1, expectedMax);
    }

    [Fact]
    public void NoRepairWithoutFunds_AndNeverAboveMaxHealth()
    {
        Simulation poor = NewWorld(cash: 0);
        int cruiser = Veh("exp_cruiser");
        VehicleType vt = Loaded.Sim.Vehicles[cruiser];
        EntityId s = poor.SpawnVehicle(0, cruiser, new CellCoord(15, 9));
        poor.Players.Cash[0] = 0;
        poor.Units.Health[s.Index] = vt.MaxHealth - 100;
        Run(poor, 60);
        Assert.Equal(vt.MaxHealth - 100, poor.Units.Health[s.Index]);

        poor.Players.Cash[0] = 100000;
        Run(poor, 3000);
        Assert.Equal(vt.MaxHealth, poor.Units.Health[s.Index]);
    }

    [Fact]
    public void BayRepairHealsPassengersInsideALandingCraft_AndNotThoseOutside()
    {
        Simulation sim = NewWorld(shipyard: false, drydock: false);
        int craftType = Veh("exp_landing_craft");
        int marineType = Inf("exp_marine");
        InfantryType mt = Loaded.Sim.Infantry[marineType];
        EntityId craft = sim.SpawnVehicle(0, craftType, new CellCoord(20, 20));
        EntityId inside = sim.SpawnInfantry(0, marineType, new CellCoord(20, 20));
        EntityId outside = sim.SpawnInfantry(0, marineType, new CellCoord(5, 5));
        sim.Tunnels.InfantryLayer[inside.Index] = ActorLayer.Passenger;
        sim.Tunnels.InfantryCarrier[inside.Index] = craft;
        sim.Infantry.Health[inside.Index] = 1; sim.Infantry.Health[outside.Index] = 1;

        Run(sim, 100);
        Assert.True(sim.Infantry.Health[inside.Index] > 1, "healed inside the bay");
        Assert.True(sim.Infantry.Health[inside.Index] <= mt.MaxHealth);
        Assert.Equal(1, sim.Infantry.Health[outside.Index]);

        Run(sim, 5000);
        Assert.Equal(mt.MaxHealth, sim.Infantry.Health[inside.Index]);
    }

    [Fact]
    public void BayRepairHealsACarriedVehicle_NoCharge()
    {
        Simulation sim = NewWorld(shipyard: false, drydock: false);
        int craftType = Veh("exp_landing_craft");
        int tankType = Veh("exp_glide_tank");
        VehicleType tt = Loaded.Sim.Vehicles[tankType];
        EntityId craft = sim.SpawnVehicle(0, craftType, new CellCoord(20, 20));
        EntityId tank = sim.SpawnVehicle(0, tankType, new CellCoord(20, 20));
        sim.Tunnels.VehicleLayer[tank.Index] = ActorLayer.Passenger;
        sim.Tunnels.VehicleCarrier[tank.Index] = craft;
        sim.Units.Health[tank.Index] = 10;
        int cash = sim.Players.Cash[0];
        Run(sim, 100);
        Assert.True(sim.Units.Health[tank.Index] > 10);
        Assert.True(sim.Units.Health[tank.Index] <= tt.MaxHealth);
        Assert.Equal(cash, sim.Players.Cash[0]);
    }

    [Fact]
    public void SaveRoundTripWhileRepairingAndBuilding_KeepsTheHashStraight()
    {
        SimSetup setup = NewSetup();
        Simulation Make()
        {
            var sim = new Simulation(setup);
            sim.PlaceBuilding(0, Bld("exp_landing_depot"), new CellCoord(6, 6));
            sim.PlaceBuilding(0, Bld("exp_plotting_room"), new CellCoord(2, 10));
            sim.PlaceBuilding(0, Bld("exp_core_hall"), new CellCoord(2, 14));
            sim.PlaceBuilding(0, Bld("exp_shipyard"), new CellCoord(10, 2));
            sim.PlaceBuilding(0, Bld("exp_drydock"), new CellCoord(10, 8));
            sim.Players.PowerOut[0] = 1000; sim.Players.Cash[0] = 50000;
            EntityId s = sim.SpawnVehicle(0, Veh("exp_cruiser"), new CellCoord(15, 9));
            sim.Units.Health[s.Index] = 300;
            Step(sim, Build(sim, ProductionCategory.Vehicle, Veh("exp_carrier")));
            return sim;
        }
        Simulation a = Make(), b = Make();
        Run(a, 150); Run(b, 150);
        Assert.Equal(a.ComputeStateHash(), b.ComputeStateHash());

        var ms = new MemoryStream();
        a.Save(ms);
        ms.Position = 0;
        Simulation loaded = Simulation.Load(ms, setup);
        Assert.Equal(a.ComputeStateHash(), loaded.ComputeStateHash());
        for (int i = 0; i < 200; i++)
        {
            a.Step(None); loaded.Step(None);
            Assert.Equal(a.ComputeStateHash(), loaded.ComputeStateHash());
        }
        Assert.True(a.Units.Health[Units(a)[0]] > 300);
    }
}
