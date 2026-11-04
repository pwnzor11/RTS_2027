// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using System.Diagnostics;
using Rts.Sim.Core;
using Rts.Sim.Tests.Movement;
using Rts.Sim.World;
using Xunit.Abstractions;
using static Rts.Sim.Tests.M3.Hardening.HardeningWorld;

namespace Rts.Sim.Tests.M3.Hardening;

/// <summary>M3-04: walls may close (D-076), save/load and determinism of the enclosure and gate pathing, recompute cost.</summary>
public sealed class WallPlacementAndSaveTests(ITestOutputHelper output)
{
    private static readonly Command[] None = Array.Empty<Command>();

    [Fact]
    public void WallsAndGates_CanBePlacedAdjacent_AndCloseARing()
    {
        var sim = new Simulation(Setup());
        BuildRing(sim, gap: new CellCoord(Hi, 9));
        // The last piece seals the interior off: allowed for a wall and a gate, refused for an ordinary 1x1 structure.
        Assert.Equal(PlacementResult.Ok, sim.CanPlace(0, Wall, new CellCoord(Hi, 9)));
        Assert.Equal(PlacementResult.Ok, sim.CanPlace(0, Gate, new CellCoord(Hi, 9)));
        Assert.Equal(PlacementResult.Ok, sim.CanPlace(0, Wall, new CellCoord(Hi + 1, 9)));   // touching the ring
    }

    [Fact]
    public void Structure_MayCloseAGapBetweenWalls()
    {
        var sim = new Simulation(Setup());
        foreach (CellCoord c in RingCells())
            if (!(c.X == Hi && c.Y >= 8 && c.Y <= 10)) Assert.False(sim.PlaceBuilding(0, c == GateCell ? Gate : Wall, c).IsNone);
        // A 3x3 HQ whose footprint fills the 3-cell gap (x 13..15, y 8..10) closes the ring: the gap rule ignores wall pieces.
        Assert.Equal(PlacementResult.Ok, sim.CanPlace(0, Hq, new CellCoord(Hi, 8)));
    }

    [Fact]
    public void SaveLoad_KeepsHardening_AndContinuesIdentically()
    {
        var setup = Setup();
        var sim = new Simulation(setup);
        var ring = BuildRing(sim);
        EntityId tank = sim.SpawnVehicle(0, Tank, new CellCoord(GateX, 1));
        GiveGrouting(sim, 0);
        sim.Step(new[] { MoveWorld.Move(tank, ActorKind.Vehicle, Inside.X, Inside.Y) });
        for (int i = 0; i < 20; i++) sim.Step(None);
        Assert.Equal(49, HardenedCount(sim));

        var ms = new MemoryStream();
        sim.Save(ms);
        ms.Position = 0;
        Simulation copy = Simulation.Load(ms, setup);
        Assert.Equal(sim.ComputeStateHash(), copy.ComputeStateHash());
        Assert.Equal(sim.Ground.GroutedPlayers, copy.Ground.GroutedPlayers);
        Assert.Equal(1UL, copy.Ground.GroutedPlayers);
        Assert.Equal(sim.Ground.Hardened, copy.Ground.Hardened);

        var kill = new[] { Kill(ring[new CellCoord(Lo, 9)], (int)sim.Tick) };
        sim.Step(kill); copy.Step(kill);
        for (int i = 0; i < 200; i++)
        {
            sim.Step(None); copy.Step(None);
            Assert.Equal(sim.ComputeStateHash(), copy.ComputeStateHash());
        }
        Assert.Equal(0, HardenedCount(copy));
        Assert.Equal(Inside, MoveWorld.CellOf(copy, tank));
    }

    [Fact]
    public void Determinism_TwoRunsHashEqual()
    {
        ulong Run()
        {
            var sim = new Simulation(Setup(players: 3, teams: new byte[] { 0, 0, 1 }));
            var ring = BuildRing(sim);
            EntityId a = sim.SpawnVehicle(0, Tank, new CellCoord(GateX, 1));
            EntityId b = sim.SpawnVehicle(1, Tank, new CellCoord(GateX + 3, 1));
            EntityId c = sim.SpawnVehicle(2, Tank, new CellCoord(GateX - 3, 1));
            EntityId r = sim.SpawnInfantry(0, Rifle, Inside);
            sim.Step(new[]
            {
                MoveWorld.Move(a, ActorKind.Vehicle, 8, 8),
                MoveWorld.Move(r, ActorKind.Infantry, 2, 2, seq: 1),
                MoveWorld.Move(b, ActorKind.Vehicle, 10, 10, player: 1),
                MoveWorld.Move(c, ActorKind.Vehicle, 11, 11, player: 2),
            });
            for (int t = 0; t < 150; t++)
            {
                if (t == 20) GiveGrouting(sim, 1);
                sim.Step(t == 100 ? new[] { Kill(ring[new CellCoord(Hi, Hi)], (int)sim.Tick) } : None);
            }
            return sim.ComputeStateHash();
        }
        Assert.Equal(Run(), Run());
    }

    [Fact]
    public void RecomputeCost_IsBounded_On128Map()
    {
        // 128x128 map, a 60x60 ring of 236 wall pieces; recompute forced by toggling the dirty flag.
        var sim = new Simulation(Setup(size: 128));
        for (int y = 30; y <= 89; y++)
            for (int x = 30; x <= 89; x++)
                if (x == 30 || x == 89 || y == 30 || y == 89) Assert.False(sim.PlaceBuilding(0, Wall, new CellCoord(x, y)).IsNone);
        GiveGrouting(sim, 0);
        sim.Step(None);
        Assert.Equal(58 * 58, HardenedCount(sim));

        const int runs = 100;
        for (int i = 0; i < 20; i++) { sim.Ground.HardeningDirty = true; sim.Step(None); }   // warm-up (JIT)
        var sw = Stopwatch.StartNew();
        for (int i = 0; i < runs; i++) sim.Step(None);
        double plainMs = sw.Elapsed.TotalMilliseconds / runs;
        sw.Restart();
        for (int i = 0; i < runs; i++) { sim.Ground.HardeningDirty = true; sim.Step(None); }
        double dirtyMs = sw.Elapsed.TotalMilliseconds / runs;
        output.WriteLine($"128x128, 236 walls: tick {plainMs:F3} ms, tick with recompute {dirtyMs:F3} ms, recompute ~{dirtyMs - plainMs:F3} ms");
        Assert.True(dirtyMs < 20, $"recompute too slow: {dirtyMs} ms per tick");
        Assert.Equal(58 * 58, HardenedCount(sim));
    }
}
