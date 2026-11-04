// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Data;
using Rts.Sim.Core;
using Rts.Sim.Map;
using Rts.Sim.Missions;
using Rts.Sim.Rules;
using static Rts.Sim.Tests.Combat.CombatFixture;
using static Rts.Sim.Tests.Economy.EconomyFixture;

namespace Rts.Sim.Tests.Economy;

/// <summary>
/// M4-16: the start-0 bias of 2-player generated maps (docs/balance/m4_16_start_bias.md). Two economy rules were not symmetric under
/// the map's 180-degree rotation: ashfall regrowth swept the cells in row-major order (a field near the top-left start regrew up to a
/// whole interval before its mirror), and a harvester's field search took the first of several equally rich cells in row-major order
/// (top-left cells first, so mirrored harvesters drove to non-mirrored cells). Both now act identically on a cell and its images.
/// </summary>
public class StartBiasTests
{
    private const int W = 40;

    private static int Mirror(int i) => W * W - 1 - i;

    /// <summary>A point-symmetric 40x40 map with ashfall fields scattered over it (density 5, so all can grow).</summary>
    private static MapData SymmetricAshMap()
    {
        const int n = W * W;
        var overlay = new OverlayKind[n];
        var dens = new byte[n];
        var rng = new SimRandom(7);
        for (int k = 0; k < 60; k++)
        {
            int i = rng.Next(n / 2);
            overlay[i] = overlay[Mirror(i)] = OverlayKind.Ashfall;
            dens[i] = dens[Mirror(i)] = 5;
        }
        return new MapData("sym", "rust_belt", W, W, new ushort[n], new LandType[n], new byte[n], overlay, dens,
            new[] { new CellCoord(4, 4), new CellCoord(W - 5, W - 5) });
    }

    [Fact]
    public void Regrowth_GrowsEveryCellOnTheSameTickAsItsMirror()
    {
        SimRules rules = Loaded.Sim with { Economy = Loaded.Sim.Economy with { RegrowthIntervalTicks = 50 } };
        var sim = NewSim(SymmetricAshMap(), rules);
        for (int t = 0; t < 120; t++)
        {
            sim.Step(None);
            for (int i = 0; i < W * W; i++)
            {
                var a = new CellCoord(i % W, i / W);
                int m = Mirror(i);
                var b = new CellCoord(m % W, m / W);
                Assert.True(sim.Map.DensityAt(a) == sim.Map.DensityAt(b),
                    $"tick {sim.Tick}: cell {a} density {sim.Map.DensityAt(a)} but its mirror {b} has {sim.Map.DensityAt(b)}");
            }
        }
        // every ashfall cell grew twice in 120 ticks of a 50-tick interval (ticks 0..119 hold phases 0..49 twice, 0..19 a third time)
        int grown = 0;
        for (int i = 0; i < W * W; i++)
            if (sim.Map.OverlayAt(new CellCoord(i % W, i / W)) == OverlayKind.Ashfall) { Assert.InRange(sim.Map.DensityAt(new CellCoord(i % W, i / W)), 7, 8); grown++; }
        Assert.True(grown > 0);
    }

    [Theory]
    [InlineData(40, 40)]
    [InlineData(37, 37)]
    [InlineData(40, 24)]
    [InlineData(1, 1)]
    public void RegrowthSchedule_VisitsEveryCellOnceAndAnOrbitTogether(int w, int h)
    {
        var s = new Rts.Sim.Economy.RegrowthSchedule(w, h);
        const int interval = 7;
        var phaseOf = new int[w * h];
        Array.Fill(phaseOf, -1);
        for (int p = 0; p < interval; p++)
            foreach (int k in s.Slice(p, interval)) { Assert.Equal(-1, phaseOf[k]); phaseOf[k] = p; }
        Assert.DoesNotContain(-1, phaseOf);
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                int p = phaseOf[y * w + x];
                Assert.Equal(p, phaseOf[(h - 1 - y) * w + (w - 1 - x)]);   // rot2
                Assert.Equal(p, phaseOf[y * w + (w - 1 - x)]);             // mirror x
                Assert.Equal(p, phaseOf[(h - 1 - y) * w + x]);             // mirror y
                if (w == h)
                {
                    Assert.Equal(p, phaseOf[x * w + (w - 1 - y)]);         // rot90 image (w-1-y, x)
                    Assert.Equal(p, phaseOf[x * w + y]);                   // diagonal
                }
            }
    }

    /// <summary>Two harvesters, each next to the point mirror of the other's field, on a map whose fields have several equally rich
    /// cells. They must harvest mirrored cells and stay exact mirror images (position, cargo, field) until one turns to deliver (the
    /// refinery dock is a fixed cell south of the footprint, so the trip home is not a mirror image by design).</summary>
    [Fact]
    public void MirroredHarvesters_PickMirroredCellsAmongEquallyRichOnes()
    {
        const int n = W * W;
        var overlay = new OverlayKind[n];
        var dens = new byte[n];
        // a 3x3 field of equal density next to the north-west refinery, and its point mirror
        for (int y = 0; y < 3; y++)
            for (int x = 0; x < 3; x++)
            {
                int i = (12 + y) * W + 12 + x;
                overlay[i] = overlay[Mirror(i)] = OverlayKind.Ashfall;
                dens[i] = dens[Mirror(i)] = 6;
            }
        var map = new MapData("pair", "rust_belt", W, W, new ushort[n], new LandType[n], new byte[n], overlay, dens,
            new[] { new CellCoord(4, 4), new CellCoord(W - 5, W - 5) });
        var sim = NewSim(map);
        int refinery = Bld("test_refinery");
        BuildingType rt = Loaded.Sim.Buildings[refinery];
        sim.PlaceBuilding(0, refinery, new CellCoord(4, 4));
        sim.PlaceBuilding(1, refinery, new CellCoord(W - rt.FootprintW - 4, W - rt.FootprintH - 4));
        EntityId a = sim.SpawnVehicle(0, Harvester, new CellCoord(9, 9));
        EntityId b = sim.SpawnVehicle(1, Harvester, new CellCoord(W - 10, W - 10));
        int wl = W * 256;
        int loads = 0;
        for (int t = 0; t < 400; t++)
        {
            sim.Step(None);
            if (sim.Units.Mission[a.Index] == MissionKind.Return || sim.Units.Mission[b.Index] == MissionKind.Return) break;
            loads = sim.Economy.Cargo[a.Index];
            LeptonCoord pa = sim.Units.Position[a.Index], pb = sim.Units.Position[b.Index];
            CellCoord ga = sim.Economy.Goal[a.Index], gb = sim.Economy.Goal[b.Index];
            Assert.True(pa.X == wl - pb.X && pa.Y == wl - pb.Y, $"tick {sim.Tick}: positions {pa} and {pb} are not mirrored");
            Assert.True(ga == default && gb == default || ga.X == W - 1 - gb.X && ga.Y == W - 1 - gb.Y,
                $"tick {sim.Tick}: goals {ga} and {gb} are not mirrored");
            Assert.Equal(sim.Economy.Cargo[a.Index], sim.Economy.Cargo[b.Index]);
        }
        Assert.True(loads >= 10, $"only {loads} units loaded before the first return");
    }

    /// <summary>The generated 2-player map gives both starts the same ashfall over a game: same total density near each start after
    /// regrowth runs for a few intervals with nobody harvesting.</summary>
    [Fact]
    public void GeneratedMap_RegrowsTheSameAmountNearBothStarts()
    {
        MapData map = MapGenerator.Generate(56, 1);
        SimRules rules = Loaded.Sim with { Economy = Loaded.Sim.Economy with { RegrowthIntervalTicks = 1200 } };
        var sim = NewSim(map, rules);
        CellCoord s0 = map.StartPositions[0], s1 = map.StartPositions[1];
        for (int t = 0; t < 3000; t++)
        {
            sim.Step(None);
            if (t % 100 != 99) continue;
            Assert.Equal(Near(sim, s0), Near(sim, s1));
        }
    }

    /// <summary>M4-16: a factory exit and a refinery dock are authored for a building in the north half (facing south, toward the
    /// centre). A building in the south half uses them turned by 180 degrees inside its footprint, so a base and its point mirror
    /// produce units and dock harvesters on mirrored cells.</summary>
    [Fact]
    public void ExitsAndDocks_OfPointMirroredBuildings_AreMirrored()
    {
        var sim = NewSim(MapData.CreateFlat(W, W));
        BuildingType f = Loaded.Sim.Buildings[Bld("test_factory")], r = Loaded.Sim.Buildings[Bld("test_refinery")];
        var tlN = new CellCoord(6, 5);
        var tlS = new CellCoord(W - f.FootprintW - 6, W - f.FootprintH - 5);   // point mirror of tlN for a 3x3 footprint
        CellCoord exitN = sim.OrientedOffset(f.Exit, f.FootprintW, f.FootprintH, tlN);
        CellCoord exitS = sim.OrientedOffset(f.Exit, f.FootprintW, f.FootprintH, tlS);
        Assert.Equal(f.Exit, exitN);
        Assert.Equal(new CellCoord(W - 1 - (tlN.X + exitN.X), W - 1 - (tlN.Y + exitN.Y)), new CellCoord(tlS.X + exitS.X, tlS.Y + exitS.Y));
        CellCoord dockN = sim.OrientedOffset(r.Dock, r.FootprintW, r.FootprintH, tlN);
        CellCoord dockS = sim.OrientedOffset(r.Dock, r.FootprintW, r.FootprintH, tlS);
        Assert.Equal(new CellCoord(W - 1 - (tlN.X + dockN.X), W - 1 - (tlN.Y + dockN.Y)), new CellCoord(tlS.X + dockS.X, tlS.Y + dockS.Y));

        // and in play: two mirrored factories each produce a tank, which appears on mirrored cells
        var sim2 = NewSim(MapData.CreateFlat(W, W));
        sim2.PlaceBuilding(0, Bld("test_hq"), new CellCoord(1, 1));
        sim2.PlaceBuilding(1, Bld("test_hq"), new CellCoord(W - 4, W - 4));
        sim2.PlaceBuilding(0, Bld("test_factory"), tlN);
        sim2.PlaceBuilding(1, Bld("test_factory"), tlS);
        int tank = Veh("test_tank");
        sim2.Step(new[]
        {
            new Command(0, 0, 0, CommandKind.Build, EntityId.None, EntityId.None, default, (int)ProductionCategory.Vehicle, tank),
            new Command(0, 1, 0, CommandKind.Build, EntityId.None, EntityId.None, default, (int)ProductionCategory.Vehicle, tank),
        });
        for (int t = 0; t < 3000 && sim2.Units.Slots.Count < 2; t++) sim2.Step(None);
        Assert.Equal(2, sim2.Units.Slots.Count);
        CellCoord a = default, b = default;
        for (int i = 1; i < sim2.Units.Capacity; i++)
            if (sim2.Units.Slots.IsIndexAlive(i)) { if (sim2.Units.Player[i] == 0) a = sim2.Units.Position[i].ToCell(); else b = sim2.Units.Position[i].ToCell(); }
        Assert.Equal(new CellCoord(W - 1 - a.X, W - 1 - a.Y), b);
    }

    /// <summary>M4-16: a soldier spawned into a cell whose wanted sub-slot is taken gets the free sub-slot nearest the map centre (it
    /// used to be the first free one, NW before SE), so point-mirrored spawns take point-mirrored corners.</summary>
    [Fact]
    public void CrowdedSpawns_TakeMirroredSubSlots()
    {
        var sim = NewSim(MapData.CreateFlat(W, W));
        int rifle = SimRules.IndexOf(Loaded.Sim.Infantry!, "test_rifleman", t => t.Id);
        var ids = new List<(EntityId A, EntityId B)>();
        foreach (var a in new[] { new CellCoord(6, 8), new CellCoord(9, 9) })   // (9,9) lies on the diagonal: equally near corners
        {
            var b = new CellCoord(W - 1 - a.X, W - 1 - a.Y);
            for (int k = 0; k < 4; k++) ids.Add((sim.SpawnInfantry(0, rifle, a), sim.SpawnInfantry(1, rifle, b)));
        }
        int wl = W * 256;
        foreach (var (ia, ib) in ids)
        {
            LeptonCoord pa = sim.Infantry.Position[ia.Index], pb = sim.Infantry.Position[ib.Index];
            Assert.True(pa.X == wl - pb.X && pa.Y == wl - pb.Y, $"{pa} and {pb} are not mirrored");
        }
    }

    private static int Near(Simulation sim, CellCoord s)
    {
        int sum = 0;
        for (int dy = -16; dy <= 16; dy++)
            for (int dx = -16; dx <= 16; dx++)
            {
                var c = new CellCoord(s.X + dx, s.Y + dy);
                if (sim.Setup.Map.Contains(c) && sim.Map.OverlayAt(c) == OverlayKind.Ashfall) sum += sim.Map.DensityAt(c);
            }
        return sum;
    }
}
