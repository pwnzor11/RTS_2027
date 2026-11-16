// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Sim.Core;
using Rts.Sim.Map;
using Rts.Sim.Missions;
using Rts.Sim.Rules;
using Rts.Sim.Tests.Movement;
using static Rts.Sim.Tests.M12_16.PropFixture;

namespace Rts.Sim.Tests.M12_16;

/// <summary>M12-16 step 2 (D-264 (3)): heavy ground vehicles crush Tree and Snag by driving into the cell; infantry, wheeled vehicles, air and
/// tracked non-crushers never do.</summary>
public sealed class PropCrushTests
{
    private const int W = 30, H = 9, WallX = 15;

    /// <summary>A sealed wall of props down column 15 (no gap), so only a crusher can get across.</summary>
    private static Simulation Walled(OverlayKind kind = OverlayKind.Tree)
    {
        var props = new List<(CellCoord, OverlayKind)>();
        for (int y = 0; y < H; y++) props.Add((new CellCoord(WallX, y), kind));
        return PropFixture.Sim(Map(W, H, props.ToArray()));
    }

    private static MapData Map(int w, int h, params (CellCoord Cell, OverlayKind Kind)[] props)
    {
        var ovl = new OverlayKind[w * h];
        foreach (var (cell, kind) in props) ovl[cell.Y * w + cell.X] = kind;
        return new MapData("props", "rust_belt", w, h, new ushort[w * h], new LandType[w * h], new byte[w * h], ovl, new byte[w * h],
            new[] { new CellCoord(1, 1), new CellCoord(w - 2, h - 2) });
    }

    private static int Trees(Simulation sim) { int n = 0; for (int y = 0; y < H; y++) if (sim.Map.OverlayAt(new CellCoord(WallX, y)) != OverlayKind.None) n++; return n; }

    private static int RunMove(Simulation sim, EntityId id, int toX, int toY, int ticks = 1500)
    {
        sim.Step(new[] { MoveWorld.Move(id, ActorKind.Vehicle, toX, toY, sim.Tick) });
        int fx = sim.FxEvents.Count(f => f.Kind == FxKind.PropDestroyed);
        for (int t = 0; t < ticks && sim.Units.Mission[id.Index] != MissionKind.Guard; t++)
        {
            sim.Step(MoveWorld.None);
            fx += sim.FxEvents.Count(f => f.Kind == FxKind.PropDestroyed);
        }
        return fx;
    }

    [Theory]
    [InlineData(OverlayKind.Tree)]
    [InlineData(OverlayKind.Snag)]
    public void AHeavyTank_CrushesThroughASealedLine_AndLeavesRough(OverlayKind kind)
    {
        Simulation sim = Walled(kind);
        EntityId tank = sim.SpawnVehicle(0, Tank, new CellCoord(5, 4));
        int fx = RunMove(sim, tank, 25, 4);
        Assert.Equal(new CellCoord(25, 4), sim.Units.Position[tank.Index].ToCell());   // it got across
        Assert.Equal(H - 1, Trees(sim));                                               // exactly one prop on its line was crushed
        Assert.Equal(1, fx);                                                           // one PropDestroyed event for it
        int crushedY = Enumerable.Range(0, H).Single(y => sim.Map.OverlayAt(new CellCoord(WallX, y)) == OverlayKind.None);
        Assert.Equal(LandType.Rough, sim.Map.LandAt(new CellCoord(WallX, crushedY)));
        Assert.Equal(0, sim.Props.HealthAt(new CellCoord(WallX, crushedY)));
        Assert.Equal(H - 1, sim.Props.Count);
    }

    [Fact]
    public void ACrushedCell_StaysOpen_ForEveryoneAfterwards()
    {
        Simulation sim = Walled();
        EntityId tank = sim.SpawnVehicle(0, Tank, new CellCoord(5, 4));
        EntityId jeep = sim.SpawnVehicle(0, Jeep, new CellCoord(5, 4 + 2));
        RunMove(sim, tank, 25, 4);
        RunMove(sim, jeep, 25, 4);
        Assert.Equal(new CellCoord(25, 4), sim.Units.Position[jeep.Index].ToCell());   // through the gap the tank made
        Assert.Equal(H - 1, Trees(sim));
    }

    [Fact]
    public void TheOthers_NeverCrush_AndCannotCrossASealedLine()
    {
        Simulation sim = Walled();
        EntityId jeep = sim.SpawnVehicle(0, Jeep, new CellCoord(5, 2));
        EntityId hauler = sim.SpawnVehicle(0, Hauler, new CellCoord(5, 4));            // tracked and heavy, but not a crusher
        EntityId truck = sim.SpawnVehicle(0, Truck, new CellCoord(5, 6));              // crusher flag on wheels: still no
        EntityId rifle = sim.SpawnInfantry(0, 0, new CellCoord(5, 8), 0);
        sim.Step(new[]
        {
            MoveWorld.Move(jeep, ActorKind.Vehicle, 25, 2, 0, 0, 0), MoveWorld.Move(hauler, ActorKind.Vehicle, 25, 4, 0, 0, 1),
            MoveWorld.Move(truck, ActorKind.Vehicle, 25, 6, 0, 0, 2), MoveWorld.Move(rifle, ActorKind.Infantry, 25, 8, 0, 0, 3),
        });
        for (int t = 0; t < 1200; t++) sim.Step(MoveWorld.None);
        Assert.True(sim.Units.Position[jeep.Index].ToCell().X < WallX);
        Assert.True(sim.Units.Position[hauler.Index].ToCell().X < WallX);
        Assert.True(sim.Units.Position[truck.Index].ToCell().X < WallX);
        Assert.True(sim.Infantry.SlotCell[rifle.Index].X < WallX);
        Assert.Equal(H, Trees(sim));
        Assert.Equal(H, sim.Props.Count);
    }

    [Fact]
    public void ABlock_StopsEvenAHeavyTank()
    {
        Simulation sim = Walled(OverlayKind.Block);
        EntityId tank = sim.SpawnVehicle(0, Tank, new CellCoord(5, 4));
        RunMove(sim, tank, 25, 4, 800);
        Assert.True(sim.Units.Position[tank.Index].ToCell().X < WallX);
        Assert.Equal(H, Trees(sim));
    }

    [Fact]
    public void AWreckIsNotCrushable_ATankStopsAtIt()
    {
        Simulation sim = Walled(OverlayKind.Wreck);
        EntityId tank = sim.SpawnVehicle(0, Tank, new CellCoord(5, 4));
        RunMove(sim, tank, 25, 4, 800);
        Assert.True(sim.Units.Position[tank.Index].ToCell().X < WallX);
        Assert.Equal(H, Trees(sim));
    }

    [Fact]
    public void Aircraft_FlyOver_AndLeaveTheLineAlone()
    {
        Simulation sim = Walled();
        EntityId plane = sim.SpawnVehicle(0, Plane, new CellCoord(5, 4));
        RunMove(sim, plane, 25, 4, 600);
        Assert.Equal(H, Trees(sim));
    }
}
