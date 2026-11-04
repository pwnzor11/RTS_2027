// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Sim.Core;
using Rts.Sim.Pathing;
using Rts.Sim.Rules;
using Rts.Sim.Tests.Movement;
using static Rts.Sim.Tests.M3.Hardening.HardeningWorld;

namespace Rts.Sim.Tests.M3.Hardening;

/// <summary>M3-04: gates are passable for the owner's team and a wall for everyone else (D-076).</summary>
public sealed class GateTests
{
    private static readonly Command[] None = Array.Empty<Command>();

    [Fact]
    public void OwnersTank_DrivesInThroughTheGate()
    {
        var sim = new Simulation(Setup());
        BuildRing(sim);
        EntityId tank = sim.SpawnVehicle(0, Tank, new CellCoord(GateX, 1));
        sim.Step(new[] { MoveWorld.Move(tank, ActorKind.Vehicle, Inside.X, Inside.Y) });
        bool usedGate = false;
        for (int t = 0; t < 400 && MoveWorld.CellOf(sim, tank) != Inside; t++)
        {
            sim.Step(None);
            usedGate |= MoveWorld.CellOf(sim, tank) == GateCell;
            MoveWorld.AssertConsistent(sim);
        }
        Assert.Equal(Inside, MoveWorld.CellOf(sim, tank));
        Assert.True(usedGate);
    }

    [Fact]
    public void OwnersInfantry_WalksOutThroughTheGate()
    {
        var sim = new Simulation(Setup());
        BuildRing(sim);
        EntityId rifle = sim.SpawnInfantry(0, Rifle, Inside);
        var outside = new CellCoord(GateX, 1);
        sim.Step(new[] { MoveWorld.Move(rifle, ActorKind.Infantry, outside.X, outside.Y) });
        for (int t = 0; t < 400 && MoveWorld.CellOfInf(sim, rifle) != outside; t++) sim.Step(None);
        Assert.Equal(outside, MoveWorld.CellOfInf(sim, rifle));
    }

    [Fact]
    public void AllyUsesTheGate_EnemyIsKeptOut()
    {
        // 0 and 1 are a team; 2 is the enemy. Player 0 owns the ring.
        var sim = new Simulation(Setup(players: 3, teams: new byte[] { 0, 0, 1 }));
        BuildRing(sim);
        EntityId ally = sim.SpawnVehicle(1, Tank, new CellCoord(GateX - 2, 1));
        EntityId enemy = sim.SpawnVehicle(2, Tank, new CellCoord(GateX + 2, 1));
        sim.Step(new[]
        {
            MoveWorld.Move(ally, ActorKind.Vehicle, Inside.X, Inside.Y, player: 1),
            MoveWorld.Move(enemy, ActorKind.Vehicle, Inside.X + 1, Inside.Y, player: 2),
        });
        for (int t = 0; t < 600; t++)
        {
            sim.Step(None);
            CellCoord e = MoveWorld.CellOf(sim, enemy);
            Assert.False(IsInterior(e), $"enemy got inside at {e}");
            Assert.NotEqual(GateCell, e);
            Assert.False(sim.Units.NextCellReserved[enemy.Index] && sim.Units.ReservedCell[enemy.Index] == GateCell);
        }
        Assert.Equal(Inside, MoveWorld.CellOf(sim, ally));
    }

    [Fact]
    public void EnemyPathsAroundAClosedGate()
    {
        // A wall line across the map with a gate and one open gap far to the east: the enemy goes round via the gap.
        var sim = new Simulation(Setup());
        for (int x = 0; x < 24; x++)
        {
            if (x == 20) continue;   // the gap
            Assert.False(sim.PlaceBuilding(0, x == 4 ? Gate : Wall, new CellCoord(x, 10)).IsNone);
        }
        EntityId mine = sim.SpawnVehicle(0, Tank, new CellCoord(4, 6));
        EntityId theirs = sim.SpawnVehicle(1, Tank, new CellCoord(5, 6));
        sim.Step(new[]
        {
            MoveWorld.Move(mine, ActorKind.Vehicle, 4, 14),
            MoveWorld.Move(theirs, ActorKind.Vehicle, 5, 14, player: 1),
        });
        int maxEnemyX = 0;
        bool mineUsedGate = false;
        for (int t = 0; t < 900; t++)
        {
            sim.Step(None);
            CellCoord e = MoveWorld.CellOf(sim, theirs);
            maxEnemyX = Math.Max(maxEnemyX, e.X);
            Assert.NotEqual(new CellCoord(4, 10), e);
            mineUsedGate |= MoveWorld.CellOf(sim, mine) == new CellCoord(4, 10);
        }
        Assert.Equal(new CellCoord(4, 14), MoveWorld.CellOf(sim, mine));
        Assert.True(mineUsedGate);
        Assert.Equal(new CellCoord(5, 14), MoveWorld.CellOf(sim, theirs));
        Assert.True(maxEnemyX >= 19, "the enemy went round through the gap");
    }

    [Fact]
    public void PathService_GateDependsOnPasser()
    {
        var sim = new Simulation(Setup());
        BuildRing(sim);
        var from = new CellCoord(GateX, 1);
        int own = sim.Paths.Request(from, Inside, Locomotion.Tracked, 0);
        int foe = sim.Paths.Request(from, Inside, Locomotion.Tracked, 1);
        int none = sim.Paths.Request(from, Inside, Locomotion.Tracked);
        sim.Paths.Process();
        Assert.Equal(PathStatus.Found, sim.Paths.Poll(own, 0, out var path));
        Assert.Contains(GateCell, path);
        Assert.Equal(PathStatus.Partial, sim.Paths.Poll(foe, 0, out _));
        Assert.Equal(PathStatus.Partial, sim.Paths.Poll(none, 0, out _));
    }

    [Fact]
    public void QueuedRequestWithPasser_RoundTripsInThePathsPart()
    {
        var sim = new Simulation(Setup());
        BuildRing(sim);
        sim.Paths.Request(new CellCoord(1, 1), Inside, Locomotion.Tracked, 0);
        sim.Paths.Request(new CellCoord(2, 1), Inside, Locomotion.Foot);
        byte[] a = Bytes(sim.Paths);
        var copy = new Simulation(Setup());
        using (var r = new BinaryReader(new MemoryStream(a))) copy.Paths.Read(r);
        Assert.Equal(a, Bytes(copy.Paths));
        copy.Paths.Process();
        Assert.Equal(PathStatus.Found, copy.Paths.Poll(1, 0, out _));   // passer 0 survived the round trip
    }

    private static byte[] Bytes(ISimStatePart part)
    {
        var ms = new MemoryStream();
        using (var w = new BinaryWriter(ms, System.Text.Encoding.UTF8, leaveOpen: true)) part.Write(w);
        return ms.ToArray();
    }
}
