// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Sim.Core;
using Rts.Sim.Map;
using Rts.Sim.Missions;
using Rts.Sim.Rules;
using Rts.Sim.Tunnels;
using static Rts.Sim.Tests.M3.Burrow.BurrowWorld;

namespace Rts.Sim.Tests.M3.Burrow;

/// <summary>M3-03: dive, burrowed travel and surfacing (design 05 §1): soft ground only, never next to a structure, a 3 s
/// telegraph, no firing while underground.</summary>
public sealed class BurrowLocomotionTests
{
    [Fact]
    public void Dive_TakesDiveTicks_ThenTravelsBurrowedWithoutHoldingACell()
    {
        var sim = NewSim();
        EntityId u = sim.SpawnVehicle(0, Burrower, new CellCoord(10, 10));
        Step(sim, MoveTo(sim, u, 40, 10));
        int diving = 0;
        while (Layer(sim, u) == ActorLayer.Diving) { diving++; sim.Step(None); }
        Assert.Equal(sim.Setup.Rules.Tunnels.DiveTicks, diving);
        Assert.Equal(ActorLayer.Burrowed, Layer(sim, u));
        Assert.True(sim.IsUnderground(ActorKind.Vehicle, u.Index));
        // underground: it holds no map cell, and it really moves
        CellCoord before = CellOf(sim, u);
        Run(sim, 30);
        Assert.Equal(ActorLayer.Burrowed, Layer(sim, u));
        Assert.True(CellOf(sim, u).X > before.X);
        Assert.True(sim.Map.VehicleAt(CellOf(sim, u)).IsNone);
        // a diving unit is still visible and still holds its cell
        var sim2 = NewSim();
        EntityId v = sim2.SpawnVehicle(0, Burrower, new CellCoord(10, 10));
        Step(sim2, MoveTo(sim2, v, 40, 10));
        Assert.Equal(ActorLayer.Diving, Layer(sim2, v));
        Assert.False(sim2.IsUnderground(ActorKind.Vehicle, v.Index));
        Assert.Equal(v, sim2.Map.VehicleAt(new CellCoord(10, 10)));
    }

    [Fact]
    public void Dive_OnlyOnSoftGround_HardGroundMeansWalkingFirst()
    {
        // a road strip (hard) around the start; clear (soft) beyond it
        var sim = NewSim((land, w) => { for (int y = 0; y < 32; y++) for (int x = 8; x <= 12; x++) land[y * w + x] = LandType.Road; });
        EntityId u = sim.SpawnVehicle(0, Burrower, new CellCoord(10, 10));
        Assert.False(IsSoft(sim, new CellCoord(10, 10)));
        Step(sim, MoveTo(sim, u, 40, 10));
        int ticks = 0;
        while (Layer(sim, u) == ActorLayer.Surface && ticks++ < 500)
        {
            Assert.False(sim.IsUnderground(ActorKind.Vehicle, u.Index));
            sim.Step(None);
        }
        Assert.Equal(ActorLayer.Diving, Layer(sim, u));
        Assert.True(ticks > 1, "it had to walk off the road first");
        Assert.True(IsSoft(sim, CellOf(sim, u)), "the dive started on soft ground");
        RunUntil(sim, () => Layer(sim, u) == ActorLayer.Surface && CellOf(sim, u).X > 30);
    }

    [Fact]
    public void Dive_NeverWithinOneCellOfAStructure()
    {
        var sim = NewSim();
        sim.PlaceBuilding(1, Block, new CellCoord(11, 10));   // either side's structure counts
        EntityId u = sim.SpawnVehicle(0, Burrower, new CellCoord(10, 10));
        Step(sim, MoveTo(sim, u, 40, 10));
        int ticks = 0;
        while (Layer(sim, u) == ActorLayer.Surface && ticks++ < 500)
        {
            sim.Step(None);
        }
        Assert.Equal(ActorLayer.Diving, Layer(sim, u));
        Assert.True(DistanceToStructure(sim, CellOf(sim, u)) >= 2);
    }

    [Fact]
    public void Surface_OnlyOnSoftGround_NearestSoftCellWhenGoalIsHard()
    {
        var sim = NewSim((land, w) => { for (int y = 6; y <= 14; y++) for (int x = 28; x <= 32; x++) land[y * w + x] = LandType.Road; });
        EntityId u = sim.SpawnVehicle(0, Burrower, new CellCoord(10, 10));
        var goal = new CellCoord(30, 10);
        Assert.False(IsSoft(sim, goal));
        Step(sim, MoveTo(sim, u, goal.X, goal.Y));
        RunUntil(sim, () => Layer(sim, u) == ActorLayer.Surfacing);
        Assert.True(IsSoft(sim, CellOf(sim, u)));
        RunUntil(sim, () => Layer(sim, u) == ActorLayer.Surface);
        CellCoord end = CellOf(sim, u);
        Assert.True(IsSoft(sim, end));
        Assert.Equal(sim.Units.Slots.IdOf(u.Index), sim.Map.VehicleAt(end));
        Assert.True(Cheb(end, goal) <= 4, $"nearest valid cell, got {end}");
    }

    [Fact]
    public void Surface_NeverWithinOneCellOfAStructure()
    {
        var sim = NewSim();
        sim.PlaceBuilding(1, House, new CellCoord(30, 10));   // 2x2 at (30..31, 10..11)
        EntityId u = sim.SpawnVehicle(0, Burrower, new CellCoord(10, 10));
        Step(sim, MoveTo(sim, u, 29, 10));                    // right against the house
        RunUntil(sim, () => Layer(sim, u) == ActorLayer.Surface && sim.Tick > 5);
        Assert.True(DistanceToStructure(sim, CellOf(sim, u)) >= 2);
        Assert.True(Cheb(CellOf(sim, u), new CellCoord(29, 10)) <= 3);
    }

    [Fact]
    public void Surface_SkipsHardenedAndOccupiedCells()
    {
        var sim = NewSim();
        EntityId u = sim.SpawnVehicle(0, Burrower, new CellCoord(10, 10));
        var goal = new CellCoord(30, 10);
        sim.Ground.Hardened[sim.Ground.IndexOf(goal)] = 1;                     // hardened by someone's wall
        sim.SpawnVehicle(1, Enemy, new CellCoord(29, 10));                     // an enemy parked next to it
        Step(sim, MoveTo(sim, u, goal.X, goal.Y));
        RunUntil(sim, () => Layer(sim, u) == ActorLayer.Surface && sim.Tick > 5);
        CellCoord end = CellOf(sim, u);
        Assert.NotEqual(goal, end);
        Assert.NotEqual(new CellCoord(29, 10), end);
        Assert.False(sim.Ground.IsHardened(end));
        Assert.True(Cheb(end, goal) <= 3);
    }

    [Fact]
    public void Telegraph_ShowsForThreeSecondsBeforeTheUnitAppears()
    {
        var sim = NewSim();
        EntityId u = sim.SpawnVehicle(0, Burrower, new CellCoord(10, 10));
        Step(sim, MoveTo(sim, u, 25, 10));
        RunUntil(sim, () => UnitTelegraph(sim) != null);
        Telegraph first = UnitTelegraph(sim)!.Value;
        Assert.Equal(sim.Setup.Rules.Tunnels.TelegraphTicks, first.TicksLeft);
        Assert.Equal(60, first.TicksLeft);                      // 3 s at 20 Hz
        Assert.Equal(0, first.Owner);
        Assert.Equal(new ActorRef(ActorKind.Vehicle, u), first.Subject);
        int shown = 0;
        while (UnitTelegraph(sim) is { } t)
        {
            shown++;
            Assert.Equal(first.Cell, t.Cell);
            Assert.Equal(ActorLayer.Surfacing, Layer(sim, u));
            Assert.True(sim.IsUnderground(ActorKind.Vehicle, u.Index));                 // not on the surface yet
            Assert.True(sim.Map.VehicleAt(first.Cell).IsNone);
            Assert.Equal(first.Cell, CellOf(sim, u));
            // the view carries it (omniscient and for its owner)
            Assert.Contains(sim.Snapshot().Telegraphs, v => v.Cell == first.Cell && v.Kind == TelegraphKind.Unit && v.TicksLeft == t.TicksLeft);
            Assert.Contains(sim.Snapshot(0).Telegraphs, v => v.Cell == first.Cell);
            sim.Step(None);
        }
        Assert.Equal(60, shown);
        Assert.Equal(ActorLayer.Surface, Layer(sim, u));        // it appears the tick the telegraph ends
        Assert.False(sim.IsUnderground(ActorKind.Vehicle, u.Index));
        Assert.Equal(u, sim.Map.VehicleAt(first.Cell));
        Assert.Empty(sim.Tunnels.Telegraphs);
        Assert.Equal(MissionKind.Guard, sim.Units.NextMission[u.Index] == MissionKind.None ? sim.Units.Mission[u.Index] : sim.Units.NextMission[u.Index]);
    }

    [Fact]
    public void Telegraph_RestartsWhenTheCellIsHardenedMeanwhile()
    {
        var sim = NewSim();
        EntityId u = sim.SpawnVehicle(0, Burrower, new CellCoord(10, 10));
        Step(sim, MoveTo(sim, u, 25, 10));
        RunUntil(sim, () => UnitTelegraph(sim) != null);
        CellCoord cell = UnitTelegraph(sim)!.Value.Cell;
        Run(sim, 30);
        sim.Ground.Hardened[sim.Ground.IndexOf(cell)] = 1;      // an enclosure closes over the cell
        RunUntil(sim, () => Layer(sim, u) == ActorLayer.Surface);
        Assert.NotEqual(cell, CellOf(sim, u));
        Assert.False(sim.Ground.IsHardened(CellOf(sim, u)));
    }

    [Fact]
    public void TwoUnitsToOneCell_SurfaceOnDifferentCells()
    {
        var sim = NewSim();
        EntityId a = sim.SpawnVehicle(0, Burrower, new CellCoord(10, 10));
        EntityId b = sim.SpawnVehicle(0, Burrower, new CellCoord(10, 14));
        Step(sim, MoveTo(sim, a, 30, 12), MoveTo(sim, b, 30, 12));
        RunUntil(sim, () => Layer(sim, a) == ActorLayer.Surface && Layer(sim, b) == ActorLayer.Surface && sim.Tick > 5);
        Assert.NotEqual(CellOf(sim, a), CellOf(sim, b));
    }

    [Fact]
    public void Burrowed_UnitsCannotFire()
    {
        // control: the same gunner on the surface shoots an adjacent enemy
        var control = NewSim();
        EntityId g0 = control.SpawnVehicle(0, Gunner, new CellCoord(10, 10));
        EntityId e0 = control.SpawnVehicle(1, Enemy, new CellCoord(12, 10));
        Step(control, Cmd(control, CommandKind.Attack, g0, default, (int)ActorKind.Vehicle | ((int)ActorKind.Vehicle << 8)) with { Target = e0 });
        Run(control, 40);
        Assert.True(control.Units.Health[e0.Index] < 500, "control gunner must be able to fire");

        var sim = NewSim();
        EntityId g = sim.SpawnVehicle(0, Gunner, new CellCoord(10, 10));
        EntityId e = sim.SpawnVehicle(1, Enemy, new CellCoord(14, 10));
        Step(sim, MoveTo(sim, g, 16, 10));                       // dives next to the enemy
        RunUntil(sim, () => Layer(sim, g) == ActorLayer.Burrowed);
        Assert.Equal(500, sim.Units.Health[e.Index]);
        // an Attack order given while underground must not produce a single shot until it is back on the surface
        Step(sim, Cmd(sim, CommandKind.Attack, g, default, (int)ActorKind.Vehicle | ((int)ActorKind.Vehicle << 8)) with { Target = e });
        int underground = 0;
        while (Layer(sim, g) != ActorLayer.Surface && underground < 400)
        {
            Assert.Equal(500, sim.Units.Health[e.Index]);
            underground++;
            sim.Step(None);
        }
        Assert.True(underground > 0);
        Assert.Equal(ActorLayer.Surface, Layer(sim, g));
    }

    [Fact]
    public void Stop_WhileDiving_ClimbsBack_WhileBurrowed_SurfacesInPlace()
    {
        var sim = NewSim();
        EntityId u = sim.SpawnVehicle(0, Burrower, new CellCoord(10, 10));
        Step(sim, MoveTo(sim, u, 40, 10));
        Run(sim, 5);
        Assert.Equal(ActorLayer.Diving, Layer(sim, u));
        Step(sim, Cmd(sim, CommandKind.Stop, u, default, (int)ActorKind.Vehicle));
        Run(sim, 2);
        Assert.Equal(ActorLayer.Surface, Layer(sim, u));
        Assert.Equal(u, sim.Map.VehicleAt(CellOf(sim, u)));

        Step(sim, MoveTo(sim, u, 40, 10));
        RunUntil(sim, () => Layer(sim, u) == ActorLayer.Burrowed);
        Run(sim, 20);
        CellCoord at = CellOf(sim, u);
        Step(sim, Cmd(sim, CommandKind.Stop, u, default, (int)ActorKind.Vehicle));
        RunUntil(sim, () => Layer(sim, u) == ActorLayer.Surface);
        Assert.True(Cheb(CellOf(sim, u), at) <= 3);
        Assert.True(CellOf(sim, u).X < 30, "it surfaced where it was, not at the old goal");
    }

    [Fact]
    public void NewMoveWhileBurrowed_RetargetsWithoutSurfacing()
    {
        var sim = NewSim();
        EntityId u = sim.SpawnVehicle(0, Burrower, new CellCoord(10, 10));
        Step(sim, MoveTo(sim, u, 40, 10));
        RunUntil(sim, () => Layer(sim, u) == ActorLayer.Burrowed);
        Run(sim, 10);
        Step(sim, MoveTo(sim, u, 12, 28));
        Assert.Equal(ActorLayer.Burrowed, Layer(sim, u));
        RunUntil(sim, () => Layer(sim, u) == ActorLayer.Surface);
        Assert.True(Cheb(CellOf(sim, u), new CellCoord(12, 28)) <= 3);
    }

    [Fact]
    public void NonBurrowers_MoveAsBefore()
    {
        var sim = NewSim();
        EntityId t = sim.SpawnVehicle(1, Enemy, new CellCoord(10, 10));
        Step(sim, MoveTo(sim, t, 20, 10, 1));
        Run(sim, 10);
        Assert.Equal(ActorLayer.Surface, Layer(sim, t));
        RunUntil(sim, () => CellOf(sim, t) == new CellCoord(20, 10));
    }
}
