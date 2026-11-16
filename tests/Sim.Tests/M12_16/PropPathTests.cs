// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Sim.Core;
using Rts.Sim.Map;
using Rts.Sim.Pathing;
using Rts.Sim.Rules;
using static Rts.Sim.Tests.M12_16.PropFixture;

namespace Rts.Sim.Tests.M12_16;

/// <summary>M12-16 step 3 (D-264 (3)): a crusher paths through Tree and Snag cells at a small extra cost, everyone else still sees them as
/// blocked, and a cleared cell refreshes every cached path and region label.</summary>
public sealed class PropPathTests
{
    private const int W = 40, H = 21, WallX = 20;

    /// <summary>A line of props down column 20 from y = 0 to <paramref name="lastY"/> (the rest of the column is open).</summary>
    private static Simulation Line(int lastY, OverlayKind kind = OverlayKind.Tree)
    {
        var props = new List<(CellCoord, OverlayKind)>();
        for (int y = 0; y <= lastY; y++) props.Add((new CellCoord(WallX, y), kind));
        return PropFixture.Sim(Map(props.ToArray()));
    }

    private static MapData Map(params (CellCoord Cell, OverlayKind Kind)[] props)
    {
        var ovl = new OverlayKind[W * H];
        foreach (var (cell, kind) in props) ovl[cell.Y * W + cell.X] = kind;
        return new MapData("props", "rust_belt", W, H, new ushort[W * H], new LandType[W * H], new byte[W * H], ovl, new byte[W * H],
            new[] { new CellCoord(1, 1), new CellCoord(W - 2, H - 2) });
    }

    private static (PathStatus Status, CellCoord[] Path) Route(Simulation sim, CellCoord from, CellCoord to, Locomotion loco)
    {
        int id = sim.Paths.Request(from, to, loco);
        sim.Paths.Process();
        PathStatus s = sim.Paths.Poll(id, 0, out IReadOnlyList<CellCoord> path);
        return (s, path.ToArray());
    }

    private static readonly CellCoord From = new(5, 10), To = new(35, 10);

    [Fact]
    public void ACrusher_PathsThroughTheLine_WhereANonCrusherGoesRoundTheEnd()
    {
        Simulation sim = Line(18);   // open at y = 19 and 20: the way round is about 20 cells of detour each way
        var (cs, crusher) = Route(sim, From, To, MoveClass.TrackedCrusher);
        Assert.Equal(PathStatus.Found, cs);
        Assert.Contains(crusher, c => sim.Map.OverlayAt(c) == OverlayKind.Tree);   // straight through
        Assert.True(crusher.Length <= 31, $"the crusher's route should be about the straight 30 cells, was {crusher.Length}");

        foreach (Locomotion loco in new[] { Locomotion.Tracked, Locomotion.Wheeled, Locomotion.Foot })
        {
            var (s, path) = Route(sim, From, To, loco);
            Assert.Equal(PathStatus.Found, s);
            Assert.DoesNotContain(path, c => sim.Map.OverlayAt(c) != OverlayKind.None);
            Assert.Contains(path, c => c.Y >= 19);                                  // round the end of the line
        }
    }

    [Fact]
    public void ACrusher_TakesAShortWayRound_NotTheTree()
    {
        // a gap one cell off the straight line: going round costs 2 extra steps, a tree costs 3 times a cell
        Simulation sim = Line(20);
        sim.Map.SetOverlay(new CellCoord(WallX, 11), OverlayKind.None, 0);
        var (s, path) = Route(sim, From, To, MoveClass.TrackedCrusher);
        Assert.Equal(PathStatus.Found, s);
        Assert.DoesNotContain(path, c => sim.Map.OverlayAt(c) != OverlayKind.None);
        Assert.Contains(path, c => c == new CellCoord(WallX, 11));
    }

    [Fact]
    public void ACrusher_StillGoesThrough_WhenTheGapIsFarAndTheTreeIsCheaper()
    {
        Simulation sim = Line(20);
        sim.Map.SetOverlay(new CellCoord(WallX, 0), OverlayKind.None, 0);           // the only gap is 10 cells off
        var (s, path) = Route(sim, From, To, MoveClass.TrackedCrusher);
        Assert.Equal(PathStatus.Found, s);
        Assert.Contains(path, c => sim.Map.OverlayAt(c) == OverlayKind.Tree);
    }

    [Fact]
    public void ASealedLine_IsClosedToEveryoneButACrusher_AndAWreckOrBlockToNobody()
    {
        Simulation sealedTrees = Line(20);
        Assert.Equal(PathStatus.Found, Route(sealedTrees, From, To, MoveClass.TrackedCrusher).Status);
        foreach (Locomotion loco in new[] { Locomotion.Tracked, Locomotion.Wheeled, Locomotion.Foot })
            Assert.NotEqual(PathStatus.Found, Route(sealedTrees, From, To, loco).Status);

        Simulation snags = Line(20, OverlayKind.Snag);
        Assert.Equal(PathStatus.Found, Route(snags, From, To, MoveClass.TrackedCrusher).Status);

        foreach (OverlayKind hard in new[] { OverlayKind.Wreck, OverlayKind.Block, OverlayKind.Pylon })
            Assert.NotEqual(PathStatus.Found, Route(Line(20, hard), From, To, MoveClass.TrackedCrusher).Status);
    }

    [Fact]
    public void AClearedCell_RefreshesTheCachedPathsAndTheRegionLabels()
    {
        Simulation sim = Line(20);
        var (before, _) = Route(sim, From, To, Locomotion.Tracked);                 // fills the path cache and the region labels: closed
        Assert.NotEqual(PathStatus.Found, before);
        var (crusherBefore, _) = Route(sim, From, To, MoveClass.TrackedCrusher);
        Assert.Equal(PathStatus.Found, crusherBefore);

        sim.Detonate(0, 1000, SplashWarhead, LeptonCoord.CenterOf(new CellCoord(WallX, 10)), default, false);   // the shell clears 3 cells (9..11)
        Assert.Equal(OverlayKind.None, sim.Map.OverlayAt(new CellCoord(WallX, 10)));
        foreach (Locomotion loco in new[] { Locomotion.Tracked, Locomotion.Wheeled, Locomotion.Foot })
        {
            var (s, path) = Route(sim, From, To, loco);
            Assert.Equal(PathStatus.Found, s);                                      // the same request now succeeds: no stale answer
            Assert.Contains(path, c => c.X == WallX && c.Y is >= 9 and <= 11);
            Assert.Contains(path, c => sim.Map.LandAt(c) == LandType.Rough);        // and the Rough cell is in it
        }
        var (after, crusherPath) = Route(sim, From, To, MoveClass.TrackedCrusher);
        Assert.Equal(PathStatus.Found, after);
        Assert.DoesNotContain(crusherPath, c => sim.Map.OverlayAt(c) != OverlayKind.None);   // it no longer needs to crush
    }

    [Fact]
    public void ACrusherEnclosedByTrees_CanLeave_AnotherVehicleCannot()
    {
        var props = new List<(CellCoord, OverlayKind)>();
        for (int dx = -1; dx <= 1; dx++) for (int dy = -1; dy <= 1; dy++) if (dx != 0 || dy != 0) props.Add((new CellCoord(10 + dx, 10 + dy), OverlayKind.Tree));
        Simulation sim = PropFixture.Sim(Map(props.ToArray()));
        Assert.Equal(PathStatus.Found, Route(sim, new CellCoord(10, 10), new CellCoord(30, 10), MoveClass.TrackedCrusher).Status);
        Assert.NotEqual(PathStatus.Found, Route(sim, new CellCoord(10, 10), new CellCoord(30, 10), Locomotion.Tracked).Status);
    }

    [Fact]
    public void TheMoveClass_IsChosenFromTheRulesData_NotFromTheTypeName()
    {
        VehicleType[] v = PropFixture.Vehicles();
        Assert.Equal(MoveClass.TrackedCrusher, MoveClass.Mover(v[Tank]));            // tracked and crusher = true
        Assert.Equal(Locomotion.Wheeled, MoveClass.Mover(v[Jeep]));
        Assert.Equal(Locomotion.Tracked, MoveClass.Mover(v[Hauler]));                // tracked but not a crusher
        Assert.Equal(Locomotion.Air, MoveClass.Mover(v[Plane]));
        Assert.Equal(Locomotion.Wheeled, MoveClass.Mover(v[Truck]));                 // the crusher flag on wheels does not count
        Assert.Equal(Locomotion.Tracked, MoveClass.Table(MoveClass.TrackedCrusher));
        Assert.Equal(Locomotion.Tracked, MoveClass.Of(v[Tank]));                     // spawn and exit searches never see the crush class
        Assert.False(MoveClass.IsNaval(MoveClass.TrackedCrusher));
    }

    [Fact]
    public void TheShippedCrushers_AreTheHeavyTrackedTanks()
    {
        string? dir = Rts.Data.RulesLocator.FindRulesDir(AppContext.BaseDirectory);
        Assert.NotNull(dir);
        SimRules rules = Rts.Data.RulesLoader.Load(dir!).Sim;
        string[] crushers = rules.Vehicles.Where(t => MoveClass.Mover(t) == MoveClass.TrackedCrusher).Select(t => t.Id).OrderBy(i => i, StringComparer.Ordinal).ToArray();
        Assert.Equal(new[] { "auth_bulwark", "auth_tank", "dir_landtrain", "dir_reactor_crawler" }, crushers);
        Assert.All(rules.Vehicles.Where(t => MoveClass.Mover(t) == MoveClass.TrackedCrusher), t => Assert.Equal(ArmorClass.Heavy, t.Armor));
    }
}
