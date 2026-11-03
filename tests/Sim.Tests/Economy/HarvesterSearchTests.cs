// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Sim.Core;
using Rts.Sim.Map;
using Rts.Sim.Rules;
using static Rts.Sim.Tests.Combat.CombatFixture;
using static Rts.Sim.Tests.Economy.EconomyFixture;

namespace Rts.Sim.Tests.Economy;

/// <summary>M2-19: the harvester field search widens to the whole map instead of idling beyond 16 cells.</summary>
public class HarvesterSearchTests
{
    private static Simulation FarFieldSim(CellCoord field, CellCoord harvester)
    {
        const int w = 96, n = w * w;
        var overlay = new OverlayKind[n];
        var dens = new byte[n];
        for (int y = 0; y < 2; y++)
            for (int x = 0; x < 2; x++) { overlay[(field.Y + y) * w + field.X + x] = OverlayKind.Ashfall; dens[(field.Y + y) * w + field.X + x] = 12; }
        var map = new MapData("far", "rust_belt", w, w, new ushort[n], new LandType[n], new byte[n], overlay, dens,
            new[] { new CellCoord(2, 2), new CellCoord(93, 93) });
        var sim = NewSim(map);
        sim.PlaceBuilding(0, Bld("test_refinery"), new CellCoord(4, 4));
        sim.SpawnVehicle(0, Harvester, harvester);
        return sim;
    }

    [Fact]
    public void FieldFarBeyondTheNearSearch_IsFoundAndWorked()
    {
        var sim = FarFieldSim(new CellCoord(70, 60), new CellCoord(8, 8));
        RunUntil(sim, () => sim.Players.Stored[0] >= 25, 20000);
        Assert.True(sim.Players.Stored[0] >= 25);
    }

    [Fact]
    public void NearestFieldWins_OverAFurtherOne()
    {
        var sim = FarFieldSim(new CellCoord(70, 60), new CellCoord(8, 8));
        // a second, nearer (but still > 16 cells away) field
        for (int y = 0; y < 2; y++)
            for (int x = 0; x < 2; x++) sim.Map.SetOverlay(new CellCoord(30 + x, 12 + y), OverlayKind.Ashfall, 12);
        int far0 = sim.Map.DensityAt(new CellCoord(70, 60));
        RunUntil(sim, () => sim.Players.Stored[0] >= 25, 20000);
        Assert.Equal(far0, sim.Map.DensityAt(new CellCoord(70, 60)));
        Assert.True(sim.Map.DensityAt(new CellCoord(30, 12)) < 12 || sim.Map.DensityAt(new CellCoord(31, 12)) < 12
            || sim.Map.DensityAt(new CellCoord(30, 13)) < 12 || sim.Map.DensityAt(new CellCoord(31, 13)) < 12);
    }

    [Fact]
    public void EmptyMap_StaysIdleWithoutCrashing()
    {
        var sim = FarFieldSim(new CellCoord(70, 60), new CellCoord(8, 8));
        for (int y = 0; y < 2; y++)
            for (int x = 0; x < 2; x++) sim.Map.SetOverlay(new CellCoord(70 + x, 60 + y), OverlayKind.None, 0);
        Run(sim, 600);
        Assert.Equal(0, sim.Players.Stored[0]);
    }
}
