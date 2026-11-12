// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Sim.Core;
using Rts.Sim.Map;
using Rts.Sim.Pathing;
using Rts.Sim.Rules;
using Rts.Sim.Tests.Pathing;

namespace Rts.Sim.Tests.S19;

/// <summary>S19 M13-12 (owner option a): soldiers path around ashfall / pitchblende fields when the detour is short, still cross when
/// it is long or there is no way round, may be ordered onto a field, and vehicles ignore fields.</summary>
public class AshPathTests
{
    private static MapState FieldColumn(int w, int h, int x0, int x1, int y0, int y1, OverlayKind kind = OverlayKind.Ashfall)
    {
        MapState m = PathServiceTests.Flat(w, h);
        for (int y = y0; y <= y1; y++)
            for (int x = x0; x <= x1; x++)
                m.SetOverlay(new CellCoord(x, y), kind, 5);
        return m;
    }

    private static PathService Service(MapState m, int penalty = RadiationRules.DefaultAshPathPenalty) =>
        new(m, PathServiceTests.Table()) { AshPathPenalty = penalty };

    private static (PathStatus, IReadOnlyList<CellCoord>) Solve(PathService s, CellCoord a, CellCoord b, Locomotion loco = Locomotion.Foot)
    {
        int id = s.Request(a, b, loco);
        s.Process();
        PathStatus st = s.Poll(id, 0, out var p);
        return (st, p);
    }

    private static int FieldCells(MapState m, IReadOnlyList<CellCoord> path) => path.Count(c => OverlayKinds.IsResource(m.OverlayAt(c)));

    [Theory]
    [InlineData(OverlayKind.Ashfall)]
    [InlineData(OverlayKind.Pitchblende)]
    public void Soldiers_walk_round_a_short_field(OverlayKind kind)
    {
        MapState m = FieldColumn(15, 11, 6, 8, 2, 8, kind);
        var (st, path) = Solve(Service(m), new(1, 5), new(13, 5));
        Assert.Equal(PathStatus.Found, st);
        Assert.Equal(0, FieldCells(m, path));
        Assert.Equal(new CellCoord(13, 5), path[^1]);
    }

    [Fact]
    public void Without_the_penalty_soldiers_walk_straight_through()
    {
        MapState m = FieldColumn(15, 11, 6, 8, 2, 8);
        var (_, path) = Solve(Service(m, 1), new(1, 5), new(13, 5));
        Assert.Equal(3, FieldCells(m, path));
    }

    [Fact]
    public void Soldiers_cross_when_the_detour_is_long()
    {
        // A 3-wide field 37 cells tall: going round costs far more than three ash cells at 4x.
        MapState m = FieldColumn(15, 41, 6, 8, 2, 38);
        var (st, path) = Solve(Service(m), new(1, 20), new(13, 20));
        Assert.Equal(PathStatus.Found, st);
        Assert.Equal(3, FieldCells(m, path));
    }

    [Fact]
    public void Soldiers_cross_when_there_is_no_way_round()
    {
        MapState m = FieldColumn(15, 11, 6, 8, 0, 10);
        var (st, path) = Solve(Service(m), new(1, 5), new(13, 5));
        Assert.Equal(PathStatus.Found, st);
        Assert.Equal(3, FieldCells(m, path));
    }

    [Fact]
    public void A_soldier_ordered_onto_a_field_still_goes_there()
    {
        MapState m = FieldColumn(15, 11, 6, 8, 2, 8);
        var (st, path) = Solve(Service(m), new(1, 5), new(7, 5));
        Assert.Equal(PathStatus.Found, st);
        Assert.Equal(new CellCoord(7, 5), path[^1]);
    }

    [Theory]
    [InlineData(Locomotion.Wheeled)]
    [InlineData(Locomotion.Tracked)]
    public void Vehicles_ignore_fields(Locomotion loco)
    {
        MapState m = FieldColumn(15, 11, 6, 8, 2, 8);
        var (_, path) = Solve(Service(m), new(1, 5), new(13, 5), loco);
        Assert.Equal(3, FieldCells(m, path));
    }

    [Fact]
    public void A_group_of_soldiers_walks_round_a_short_field()
    {
        MapState m = FieldColumn(15, 11, 6, 8, 2, 8);
        PathService s = Service(m);
        int id = s.RequestGroup(new[] { new CellCoord(1, 4), new CellCoord(1, 6) }, new(13, 5), Locomotion.Foot);
        s.Process();
        for (int k = 0; k < 2; k++)
        {
            Assert.Equal(PathStatus.Found, s.Poll(id, k, out var p));
            Assert.Equal(0, FieldCells(m, p));
        }
    }

    [Fact]
    public void A_field_appearing_or_running_dry_invalidates_cached_paths()
    {
        MapState m = PathServiceTests.Flat(8, 8);
        int v = m.Version;
        m.SetOverlay(new CellCoord(3, 3), OverlayKind.Ashfall, 4);
        Assert.NotEqual(v, m.Version);
        v = m.Version;
        m.SetOverlay(new CellCoord(3, 3), OverlayKind.Ashfall, 3);   // density only: same cost, no bump
        Assert.Equal(v, m.Version);
        m.SetOverlay(new CellCoord(3, 3), OverlayKind.None, 0);
        Assert.NotEqual(v, m.Version);
    }

    [Fact]
    public void Cached_and_fresh_solves_agree_after_a_field_runs_dry()
    {
        MapState m = FieldColumn(15, 11, 6, 8, 2, 8);
        PathService cached = Service(m);
        Solve(cached, new(1, 5), new(13, 5));
        for (int y = 2; y <= 8; y++)
            for (int x = 6; x <= 8; x++)
                m.SetOverlay(new CellCoord(x, y), OverlayKind.None, 0);
        var (_, a) = Solve(cached, new(1, 5), new(13, 5));
        PathService fresh = Service(m);
        fresh.CacheEnabled = false;
        var (_, b) = Solve(fresh, new(1, 5), new(13, 5));
        Assert.Equal(b, a);
    }

    [Fact]
    public void The_rules_default_is_four_and_reaches_the_path_service()
    {
        Assert.Equal(4, RadiationRules.Default.AshPathPenalty);
    }
}
