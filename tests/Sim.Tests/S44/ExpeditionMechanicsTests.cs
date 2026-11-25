// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Data;
using Rts.Sim.Core;
using Rts.Sim.Map;
using Rts.Sim.Rules;

namespace Rts.Sim.Tests.S44;

/// <summary>S44 package expedition: Provision Store stocked/unstocked supply (M-10), hover harvester on Shallow (M-13), deploy only on a
/// landing-coast cell (M-16). Shipped rules.</summary>
public sealed class ExpeditionMechanicsTests
{
    private static readonly RuleSet Rules = RulesLoader.Load(RulesLocator.FindRulesDir(AppContext.BaseDirectory)!);
    private static int B(string id) => SimRules.IndexOf(Rules.Sim.Buildings, id, x => x.Id);
    private static int V(string id) => SimRules.IndexOf(Rules.Sim.Vehicles, id, x => x.Id);

    private static MapData Map(int w, int h, Func<int, int, LandType> land, Action<OverlayKind[], byte[]>? paint = null)
    {
        var l = new LandType[w * h];
        for (int y = 0; y < h; y++) for (int x = 0; x < w; x++) l[y * w + x] = land(x, y);
        var ov = new OverlayKind[w * h]; var dens = new byte[w * h];
        paint?.Invoke(ov, dens);
        return new MapData("s44", "rust_belt", w, h, new ushort[w * h], l, new byte[w * h], ov, dens, new[] { new CellCoord(2, 2), new CellCoord(w - 3, h - 3) });
    }

    private static Simulation New(MapData map) =>
        new(new SimSetup(5, map, 2, Rules.Sim, Rules.RulesHash) { Factions = [Faction.Expedition, Faction.Authority] });

    // ---- M-10 ------------------------------------------------------------------------------------

    private static (Simulation Sim, int Depot) StoreGame(int stores)
    {
        Simulation sim = New(Map(40, 40, (_, _) => LandType.Clear));
        Assert.False(sim.PlaceBuilding(0, B("exp_landing_depot"), new CellCoord(10, 10)).IsNone);
        for (int k = 0; k < stores; k++) Assert.False(sim.PlaceBuilding(0, B("exp_provision_store"), new CellCoord(15 + 2 * k, 10)).IsNone);
        return (sim, 0);
    }

    [Fact]
    public void AStoreGivesTenSupplyWhileStockedAndFiveWhenNot()
    {
        (Simulation sim, _) = StoreGame(1);
        int depot = Rules.Sim.Buildings[B("exp_landing_depot")].SupplyProvided;
        int interval = Rules.Sim.Shipments.IntervalTicks, window = Rules.Sim.Supply.StockIntervals * interval;
        Assert.Equal(depot + 10, sim.SupplyCap(0));   // start of game: stocked
        sim.Shipments.LastArrivalTick[0] = 0;
        while (sim.Tick <= window) sim.Step(Array.Empty<Command>());
        Assert.Equal(depot + 5, sim.SupplyCap(0));    // no shipment for stock_intervals intervals
        sim.Shipments.LastArrivalTick[0] = sim.Tick;  // a tender docked (the cap cache is per tick, so look on the next one)
        sim.Step(Array.Empty<Command>());
        Assert.Equal(depot + 10, sim.SupplyCap(0));
    }

    [Fact]
    public void LastArrivalSurvivesSaveLoadAndMovesTheHash()
    {
        (Simulation sim, _) = StoreGame(1);
        ulong before = sim.ComputeStateHash();
        sim.Shipments.LastArrivalTick[0] = 777;
        Assert.NotEqual(before, sim.ComputeStateHash());
        var ms = new MemoryStream();
        sim.Save(ms);
        var setup = new SimSetup(5, Map(40, 40, (_, _) => LandType.Clear), 2, Rules.Sim, Rules.RulesHash) { Factions = [Faction.Expedition, Faction.Authority] };
        Simulation loaded = Simulation.Load(new MemoryStream(ms.ToArray()), setup);
        Assert.Equal(777, loaded.Shipments.LastArrivalTick[0]);
        Assert.Equal(sim.ComputeStateHash(), loaded.ComputeStateHash());
    }

    // ---- M-13 ------------------------------------------------------------------------------------

    [Fact]
    public void TheHoverHarvesterWorksOreOnShallowCells()
    {
        // ore only on a Shallow lagoon east of the refinery; a land harvester cannot reach it, the Skimmer can
        Simulation sim = New(Map(40, 30, (x, y) => x >= 20 && x < 26 && y >= 8 && y < 14 ? LandType.Shallow : LandType.Clear, (ov, d) =>
        {
            for (int y = 9; y < 13; y++) for (int x = 21; x < 25; x++) { ov[y * 40 + x] = OverlayKind.Ashfall; d[y * 40 + x] = 12; }
        }));
        Assert.False(sim.PlaceBuilding(0, B("exp_landing_depot"), new CellCoord(3, 10)).IsNone);
        Assert.False(sim.PlaceBuilding(0, B("exp_dredge_works"), new CellCoord(8, 10)).IsNone);
        EntityId h = sim.SpawnVehicle(0, V("exp_skimmer"), new CellCoord(12, 12));
        Assert.False(h.IsNone);
        int total0 = Total(sim);
        for (int t = 0; t < 3000 && Total(sim) >= total0; t++) sim.Step(Array.Empty<Command>());
        Assert.True(Total(sim) < total0, "the skimmer harvested nothing from the shallow field");
    }

    private static int Total(Simulation sim)
    {
        int s = 0;
        for (int y = 0; y < sim.Map.Height; y++) for (int x = 0; x < sim.Map.Width; x++) s += sim.Map.DensityAt(new CellCoord(x, y));
        return s;
    }

    // ---- M-16 ------------------------------------------------------------------------------------

    private static (Simulation Sim, EntityId Ship) ShipGame(LandType shore)
    {
        // sea x < 12, a two-wide shore strip x = 12..13 of the given land, plain ground beyond
        Simulation sim = New(Map(30, 20, (x, _) => x < 12 ? LandType.Water : x < 14 ? shore : LandType.Clear));
        EntityId ship = sim.SpawnVehicle(0, V("exp_command_ship"), new CellCoord(11, 10));
        Assert.False(ship.IsNone);
        return (sim, ship);
    }

    private static int Depots(Simulation s)
    {
        int t = B("exp_landing_depot"), n = 0;
        for (int i = 1; i < s.Buildings.Capacity; i++) if (s.Buildings.Slots.IsIndexAlive(i) && s.Buildings.TypeIndex[i] == t) n++;
        return n;
    }

    [Fact]
    public void TheCommandShipDeploysOnALandingCoastFootprint()
    {
        (Simulation sim, EntityId ship) = ShipGame(LandType.Coast);
        sim.Step(new[] { new Command(sim.Tick, 0, 1, CommandKind.Deploy, ship, default, new CellCoord(11, 10), 0) });
        for (int i = 0; i < 5; i++) sim.Step(Array.Empty<Command>());
        Assert.Equal(1, Depots(sim));
    }

    [Fact]
    public void TheCommandShipRefusesToDeployWhereNoFootprintHoldsALandingCell()
    {
        (Simulation sim, EntityId ship) = ShipGame(LandType.Clear);   // buildable shore, but no beach
        sim.Step(new[] { new Command(sim.Tick, 0, 1, CommandKind.Deploy, ship, default, new CellCoord(11, 10), 0) });
        for (int i = 0; i < 5; i++) sim.Step(Array.Empty<Command>());
        Assert.Equal(0, Depots(sim));
        Assert.True(sim.Units.Slots.IsAlive(ship));
    }

    [Fact]
    public void TheCommandShipDeploysOnAShoreCellNextToShallowWater()
    {
        // sea x < 10 deep, x = 10..11 shallow (the ship sits there), plain buildable ground from x = 12: no Coast anywhere
        Simulation sim = New(Map(30, 20, (x, _) => x < 10 ? LandType.Water : x < 12 ? LandType.Shallow : LandType.Clear));
        EntityId ship = sim.SpawnVehicle(0, V("exp_command_ship"), new CellCoord(11, 10));
        Assert.False(ship.IsNone);
        sim.Step(new[] { new Command(sim.Tick, 0, 1, CommandKind.Deploy, ship, default, new CellCoord(11, 10), 0) });
        for (int i = 0; i < 5; i++) sim.Step(Array.Empty<Command>());
        Assert.Equal(1, Depots(sim));
    }
}
