// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Sim.Core;
using Rts.Sim.Rules;
using static Rts.Sim.Tests.Combat.CombatFixture;
using static Rts.Sim.Tests.Economy.EconomyFixture;

namespace Rts.Sim.Tests.Economy;

public class PowerRegrowthSaveTests
{
    [Fact]
    public void PowerTotals_FollowTheLiveBuildings()
    {
        var sim = NewSim(PaintMap(OverlayKind.None, 0));
        sim.PlaceBuilding(0, Bld("test_hq"), new CellCoord(2, 2));            // +20
        EntityId plant = sim.PlaceBuilding(0, Bld("test_power"), new CellCoord(8, 2));   // +100
        sim.PlaceBuilding(0, Bld("test_refinery"), Refinery.X == 4 ? new CellCoord(14, 2) : Refinery);   // -20
        sim.PlaceBuilding(0, Bld("test_barracks"), new CellCoord(2, 10));     // -10
        sim.PlaceBuilding(1, Bld("test_factory"), new CellCoord(20, 20));     // -30 for player 1
        sim.Step(None);
        Assert.Equal(120, sim.Players.PowerOut[0]);
        Assert.Equal(30, sim.Players.PowerUse[0]);
        Assert.Equal(0, sim.Players.PowerOut[1]);
        Assert.Equal(30, sim.Players.PowerUse[1]);
        Assert.Equal(0, sim.PowerBand(0));
        Assert.Equal(3, sim.PowerBand(1));

        sim.Step(new[] { Debug(sim, 0, DebugOp.Kill, plant, (int)ActorKind.Building) });
        sim.Step(None);
        Assert.Equal(20, sim.Players.PowerOut[0]);
        Assert.Equal(30, sim.Players.PowerUse[0]);
        Assert.Equal(2, sim.PowerBand(0));        // 20/30 = 66 %
        Assert.Equal(20, sim.HudFor(0).PowerOut);
    }

    [Fact]
    public void Regrowth_GrowsAshfallUpToTwelve_NotPitchblendeNorEmptyCells()
    {
        SimRules rules = Loaded.Sim with { Economy = Loaded.Sim.Economy with { RegrowthIntervalTicks = 10 } };
        var map = PaintMap(OverlayKind.Ashfall, 5, (o, d, w) =>
        {
            o[20 * w + 20] = OverlayKind.Pitchblende; d[20 * w + 20] = 5;
            o[20 * w + 22] = OverlayKind.Ashfall; d[20 * w + 22] = 12;
        });
        var sim = NewSim(map, rules);
        var ash = new CellCoord(Field.X, Field.Y);
        var empty = new CellCoord(25, 25);
        sim.Step(None);
        Run(sim, 9);                                   // one full interval has passed (ticks 0..9)
        Assert.Equal(6, sim.Map.DensityAt(ash));
        Assert.Equal(5, sim.Map.DensityAt(new CellCoord(20, 20)));
        Assert.Equal(12, sim.Map.DensityAt(new CellCoord(22, 20)));
        Assert.Equal(0, sim.Map.DensityAt(empty));
        Run(sim, 10 * 10);
        Assert.Equal(12, sim.Map.DensityAt(ash));      // capped
        Assert.Equal(OverlayKind.None, sim.Map.OverlayAt(empty));
        Assert.Equal(5, sim.Map.DensityAt(new CellCoord(20, 20)));
    }

    [Fact]
    public void SaveAndLoadMidHarvest_ReproducesTheSameHashes()
    {
        var (sim, h) = Basic(silo: true);
        EntityId b = sim.SpawnVehicle(0, Harvester, new CellCoord(8, 11));
        var saves = new List<(int Tick, byte[] Bytes)>();
        var hashes = new Dictionary<int, ulong> { [0] = sim.ComputeStateHash() };
        const int total = 1400;
        for (int t = 0; t < total; t++)
        {
            if (t % 97 == 0)
            {
                using var ms = new MemoryStream();
                sim.Save(ms);
                saves.Add((t, ms.ToArray()));
            }
            sim.Step(None);
            hashes[sim.Tick] = sim.ComputeStateHash();
        }
        Assert.True(sim.Players.Stored[0] > 0 || sim.Economy.Cargo[h.Index] + sim.Economy.Cargo[b.Index] > 0);

        foreach ((int saveTick, byte[] bytes) in saves)
        {
            var loaded = Simulation.Load(new MemoryStream(bytes), sim.Setup);
            Assert.Equal(hashes[saveTick], loaded.ComputeStateHash());
            for (int k = 0; k < 120 && loaded.Tick < total; k++)
            {
                loaded.Step(None);
                if (hashes[loaded.Tick] != loaded.ComputeStateHash())
                    Assert.Fail($"load at tick {saveTick} diverged at tick {loaded.Tick}");
            }
        }
    }
}
