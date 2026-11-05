// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Data;
using Rts.Sim.Core;
using Rts.Sim.Map;
using Rts.Sim.Rules;
using Rts.Sim.Tests.Rules;
using Rts.Sim.World;

namespace Rts.Sim.Tests.M7;

/// <summary>M7-07 (docs/design/11_m7_plan.md §2.5): the beachhead zone gates Expedition placement, water buildings follow their footprint
/// mask, other structures never stand on Water or Shallow. Fixture rules plus four test-only types; a 40x40 map with a shore at x = 18..19.</summary>
public sealed class BeachheadTests
{
    private static readonly Command[] None = Array.Empty<Command>();

    private static RuleSet Rules()
    {
        string buildings = TempRules.Real("buildings.toml") + """

            [test_xdepot]
            footprint_w = 2
            footprint_h = 2
            max_health = 300
            armor = "structure"
            sight = 3
            primary_weapon = ""
            cost = 100
            power = 0
            prerequisites = []
            provides = []
            beachhead_source = true
            beachhead_radius = 5

            [test_xbeacon]
            footprint_w = 1
            footprint_h = 1
            max_health = 50
            armor = "structure"
            sight = 3
            primary_weapon = ""
            cost = 10
            power = 0
            prerequisites = []
            provides = []
            beachhead_source = true
            beachhead_radius = 3

            [test_xhut]
            footprint_w = 2
            footprint_h = 2
            max_health = 300
            armor = "structure"
            sight = 3
            primary_weapon = ""
            cost = 100
            power = 0
            prerequisites = []
            provides = []

            [test_xquay]
            footprint_w = 2
            footprint_h = 1
            max_health = 300
            armor = "structure"
            sight = 3
            primary_weapon = ""
            cost = 100
            power = 0
            prerequisites = []
            provides = []
            water_building = true
            water = [0, 1]
            """;
        using var temp = new TempRules(("buildings.toml", buildings));
        return RulesLoader.Load(temp.Dir);
    }

    private static int B(RuleSet r, string id) => SimRules.IndexOf(r.Sim.Buildings, id, x => x.Id);

    /// <summary>Clear land for x &lt; 18, Coast at x = 18, Shallow at x = 19, Water from x = 20; player 0 Expedition, player 1 Authority.</summary>
    private static (RuleSet R, SimSetup S) Setup()
    {
        RuleSet r = Rules();
        const int w = 40, h = 40;
        var land = new LandType[w * h];
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
                land[y * w + x] = x < 18 ? LandType.Clear : x == 18 ? LandType.Coast : x == 19 ? LandType.Shallow : LandType.Water;
        var map = new MapData("shore", "rust_belt", w, h, new ushort[w * h], land, new byte[w * h], new OverlayKind[w * h], new byte[w * h],
            new[] { new CellCoord(2, 2), new CellCoord(w - 3, h - 3) });
        var setup = new SimSetup(1, map, 2, r.Sim, r.RulesHash) { Factions = new[] { Faction.Expedition, Faction.Authority } };
        return (r, setup);
    }

    private static int ZoneCells(Simulation sim, int player)
    {
        int n = 0;
        foreach (ulong w in sim.Beachhead.Zone[player]) n += System.Numerics.BitOperations.PopCount(w);
        return n;
    }

    [Fact]
    public void Zone_GrowsWithSecondSource_AndShrinksWhenOneDies()
    {
        var (r, setup) = Setup();
        var sim = new Simulation(setup);
        Assert.Equal(0, ZoneCells(sim, 0));
        EntityId depot = sim.PlaceBuilding(0, B(r, "test_xdepot"), new CellCoord(5, 5));
        Assert.False(depot.IsNone);
        Assert.Equal(12 * 12, ZoneCells(sim, 0));   // 2x2 footprint plus 5 cells each side, Chebyshev
        Assert.True(sim.Beachhead.Contains(0, 5 * 40 + 0));
        Assert.True(sim.Beachhead.Contains(0, 10 * 40 + 11));
        Assert.False(sim.Beachhead.Contains(0, 5 * 40 + 12));
        EntityId beacon = sim.PlaceBuilding(0, B(r, "test_xbeacon"), new CellCoord(11, 5));   // on the edge of the depot zone
        Assert.False(beacon.IsNone);
        int both = ZoneCells(sim, 0);
        Assert.True(both > 12 * 12);
        Assert.True(sim.Beachhead.Contains(0, 5 * 40 + 14));   // 3 cells east of the beacon
        Assert.Equal(0, ZoneCells(sim, 1));                    // the other player's zone is untouched

        sim.Step(new[] { new Command(0, 0, 0, CommandKind.Sell, beacon, default, default, (int)ActorKind.Building) });
        Assert.Equal(12 * 12, ZoneCells(sim, 0));
        Assert.False(sim.Beachhead.Contains(0, 5 * 40 + 14));
    }

    [Fact]
    public void Placement_RefusedOutsideAndAcceptedInside_AndLosingSourceDestroysNothing()
    {
        var (r, setup) = Setup();
        var sim = new Simulation(setup);
        int hut = B(r, "test_xhut"), depotT = B(r, "test_xdepot");
        EntityId depot = sim.PlaceBuilding(0, depotT, new CellCoord(5, 5));   // bootstrap: the first building needs no zone
        Assert.False(depot.IsNone);
        Assert.Equal(PlacementResult.Ok, sim.CanPlace(0, hut, new CellCoord(9, 5)));              // x 9..10, inside (zone x 0..11)
        Assert.Equal(PlacementResult.OutsideBeachhead, sim.CanPlace(0, hut, new CellCoord(11, 5)));   // x 11..12, one column out
        Assert.Equal(PlacementResult.OutsideBeachhead, sim.CanPlace(0, hut, new CellCoord(15, 15)));
        Assert.True(sim.PlaceBuilding(0, hut, new CellCoord(15, 15)).IsNone);
        EntityId inside = sim.PlaceBuilding(0, hut, new CellCoord(9, 5));
        Assert.False(inside.IsNone);

        sim.Step(new[] { new Command(0, 0, 0, CommandKind.Sell, depot, default, default, (int)ActorKind.Building) });
        Assert.Equal(0, ZoneCells(sim, 0));
        Assert.True(sim.Buildings.Slots.IsAlive(inside));   // nothing is destroyed
        Assert.Equal(PlacementResult.OutsideBeachhead, sim.CanPlace(0, hut, new CellCoord(2, 2)));   // but new placement is blocked
    }

    [Fact]
    public void OtherFactions_AreUnaffected()
    {
        var (r, setup) = Setup();
        var sim = new Simulation(setup);
        int hut = B(r, "test_xhut");
        Assert.False(sim.PlaceBuilding(1, B(r, "test_xdepot"), new CellCoord(2, 2)).IsNone);
        Assert.Equal(PlacementResult.Ok, sim.CanPlace(1, hut, new CellCoord(14, 14)));   // far from the depot, no zone needed
        Assert.Equal(0, ZoneCells(sim, 0));
    }

    [Fact]
    public void WaterBuilding_StraddlingShoreAccepted_FullyInlandOrFullyWetRefused()
    {
        var (r, setup) = Setup();
        var sim = new Simulation(setup);
        int quay = B(r, "test_xquay");
        Assert.False(sim.PlaceBuilding(0, B(r, "test_xdepot"), new CellCoord(14, 5)).IsNone);   // zone reaches x = 20
        Assert.Equal(PlacementResult.Ok, sim.CanPlace(0, quay, new CellCoord(18, 5)));          // Coast (land) + Shallow (water)
        Assert.Equal(PlacementResult.Ok, sim.CanPlace(0, quay, new CellCoord(18, 6)));
        Assert.Equal(PlacementResult.Blocked, sim.CanPlace(0, quay, new CellCoord(16, 5)));     // fully inland: the water cell is Clear
        Assert.Equal(PlacementResult.Blocked, sim.CanPlace(0, quay, new CellCoord(19, 5)));     // land cell would be Shallow
        Assert.Equal(PlacementResult.Blocked, sim.CanPlace(0, quay, new CellCoord(20, 5)));     // fully at sea
        Assert.False(sim.PlaceBuilding(0, quay, new CellCoord(18, 5)).IsNone);
    }

    [Fact]
    public void OrdinaryStructures_NeverStandOnWaterOrShallow_ForAnyFaction()
    {
        var (r, setup) = Setup();
        var sim = new Simulation(setup);
        int hut = B(r, "test_xhut");
        foreach (byte p in new byte[] { 0, 1 })
        {
            Assert.Equal(PlacementResult.Blocked, sim.CanPlace(p, hut, new CellCoord(19, 10)));   // Shallow + Water
            Assert.Equal(PlacementResult.Blocked, sim.CanPlace(p, hut, new CellCoord(25, 10)));   // Water
            Assert.Equal(PlacementResult.Blocked, sim.CanPlace(p, hut, new CellCoord(18, 10)));   // Coast + Shallow
        }
        Assert.Equal(PlacementResult.Ok, sim.CanPlace(1, hut, new CellCoord(16, 10)));   // the land next to it is fine
    }

    [Fact]
    public void View_ShowsOwnMask()
    {
        var (r, setup) = Setup();
        var sim = new Simulation(setup);
        sim.PlaceBuilding(0, B(r, "test_xdepot"), new CellCoord(5, 5));
        Assert.True(sim.ViewFor(0).InBeachhead(new CellCoord(5, 5)));
        Assert.False(sim.ViewFor(0).InBeachhead(new CellCoord(30, 30)));
        Assert.False(sim.ViewFor(0).InBeachhead(new CellCoord(-1, 0)));
        Assert.False(sim.ViewFor(1).InBeachhead(new CellCoord(5, 5)));
        Assert.Equal(sim.Beachhead.Zone[0].Length, sim.ViewFor(0).BeachheadMask.Length);
    }

    [Fact]
    public void SaveLoad_ReproducesZoneAndHash()
    {
        var (r, setup) = Setup();
        var sim = new Simulation(setup);
        sim.PlaceBuilding(0, B(r, "test_xdepot"), new CellCoord(5, 5));
        sim.PlaceBuilding(0, B(r, "test_xbeacon"), new CellCoord(11, 5));
        for (int i = 0; i < 5; i++) sim.Step(None);
        var ms = new MemoryStream();
        sim.Save(ms);
        Simulation loaded = Simulation.Load(new MemoryStream(ms.ToArray()), setup);
        Assert.Equal(sim.ComputeStateHash(), loaded.ComputeStateHash());
        Assert.Equal(sim.Beachhead.Zone[0], loaded.Beachhead.Zone[0]);
        sim.Step(None); loaded.Step(None);
        Assert.Equal(sim.ComputeStateHash(), loaded.ComputeStateHash());
    }
}
