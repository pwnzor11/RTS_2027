// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Sim.Core;
using static Rts.Sim.Tests.M3.Hardening.HardeningWorld;

namespace Rts.Sim.Tests.M3.Hardening;

/// <summary>M3-04: Foundation Grouting enclosure (design 05 §2, D-076).</summary>
public sealed class HardeningTests
{
    private static readonly Command[] None = Array.Empty<Command>();

    [Fact]
    public void ClosedRingWithGate_HardensInterior_OnlyWithUpgrade()
    {
        var sim = new Simulation(Setup());
        BuildRing(sim);
        for (int i = 0; i < 3; i++) sim.Step(None);
        Assert.Equal(0, HardenedCount(sim));
        Assert.False(sim.Ground.HardeningDirty);

        GiveGrouting(sim, 0);
        sim.Step(None);
        for (int y = 0; y < 24; y++)
            for (int x = 0; x < 24; x++)
            {
                var c = new CellCoord(x, y);
                Assert.Equal(IsInterior(c), sim.Ground.IsHardenedBy(0, c));
                Assert.False(sim.Ground.IsHardenedBy(1, c));
            }
        Assert.Equal(7 * 7, HardenedCount(sim));
        Assert.True(sim.IsHardenedAgainst(1, Inside));    // enemy cannot surface / break through
        Assert.False(sim.IsHardenedAgainst(0, Inside));   // own team is not kept out
        Assert.False(sim.IsHardenedAgainst(1, new CellCoord(2, 2)));
        Assert.False(sim.IsHardenedAgainst(1, new CellCoord(-1, 3)));
    }

    [Fact]
    public void EnemyUpgrade_DoesNotHardenMyRing()
    {
        var sim = new Simulation(Setup());
        BuildRing(sim, player: 0);
        GiveGrouting(sim, 1);
        sim.Step(None);
        Assert.Equal(0, HardenedCount(sim));
    }

    [Fact]
    public void GapInRing_NoHardening()
    {
        var sim = new Simulation(Setup());
        BuildRing(sim, gap: new CellCoord(Hi, 9));
        GiveGrouting(sim, 0);
        for (int i = 0; i < 3; i++) sim.Step(None);
        Assert.Equal(0, HardenedCount(sim));
    }

    [Fact]
    public void DestroyingAWallPiece_ClearsHardening()
    {
        var sim = new Simulation(Setup());
        var ring = BuildRing(sim);
        GiveGrouting(sim, 0);
        sim.Step(None);
        Assert.Equal(49, HardenedCount(sim));

        sim.Step(new[] { Kill(ring[new CellCoord(Lo, 9)], (int)sim.Tick) });
        sim.Step(None);   // removal sets the dirty flag in cleanup; the next RunGround recomputes
        Assert.False(sim.Buildings.Slots.IsAlive(ring[new CellCoord(Lo, 9)]));
        Assert.Equal(0, HardenedCount(sim));
        Assert.False(sim.Ground.HardeningDirty);
    }

    [Fact]
    public void DestroyingTheGate_ClearsHardening()
    {
        var sim = new Simulation(Setup());
        var ring = BuildRing(sim);
        GiveGrouting(sim, 0);
        sim.Step(None);
        sim.Step(new[] { Kill(ring[GateCell], (int)sim.Tick) });
        sim.Step(None);
        Assert.Equal(0, HardenedCount(sim));
    }

    [Fact]
    public void StructureClosesTheRing_AndAllyBuildingsCount_AndEveryMemberGetsTheBit()
    {
        // Players 0 and 1 are a team, 2 is the enemy. Player 1's HQ closes the gap in player 0's ring; only player 0 has grouting.
        var sim = new Simulation(Setup(players: 3, teams: new byte[] { 0, 0, 1 }));
        var gap = new CellCoord(Hi, 9);
        BuildRing(sim, gap: gap);
        Assert.False(sim.PlaceBuilding(1, Wall, gap).IsNone);
        GiveGrouting(sim, 0);
        sim.Step(None);
        Assert.Equal(49, HardenedCount(sim));
        Assert.True(sim.Ground.IsHardenedBy(0, Inside));
        Assert.True(sim.Ground.IsHardenedBy(1, Inside));
        Assert.False(sim.Ground.IsHardenedBy(2, Inside));
        Assert.False(sim.IsHardenedAgainst(1, Inside));
        Assert.True(sim.IsHardenedAgainst(2, Inside));
    }

    [Fact]
    public void StructureFootprint_IsPartOfTheSeal()
    {
        // Replace the three east-wall cells (Hi, 8..10) with an HQ whose footprint covers them (x 13..15).
        var sim = new Simulation(Setup());
        foreach (CellCoord c in RingCells())
        {
            if (c.X == Hi && c.Y >= 8 && c.Y <= 10) continue;
            Assert.False(sim.PlaceBuilding(0, c == GateCell ? Gate : Wall, c).IsNone);
        }
        Assert.False(sim.PlaceBuilding(0, Hq, new CellCoord(Hi, 8)).IsNone);
        GiveGrouting(sim, 0);
        sim.Step(None);
        Assert.Equal(49, HardenedCount(sim));
    }

    [Fact]
    public void HardSoilInside_IsNotMarked()
    {
        var setup = Setup() with { Map = Movement.MoveWorld.Grid(24, 24, (land, w) => land[9 * w + 9] = Rts.Sim.Rules.LandType.Road) };
        var sim = new Simulation(setup);
        BuildRing(sim);
        GiveGrouting(sim, 0);
        sim.Step(None);
        Assert.Equal(48, HardenedCount(sim));
        Assert.False(sim.Ground.IsHardened(Inside));
    }
}
