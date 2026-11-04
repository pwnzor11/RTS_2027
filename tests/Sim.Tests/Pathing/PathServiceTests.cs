// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Sim.Core;
using Rts.Sim.Map;
using Rts.Sim.Pathing;
using Rts.Sim.Rules;

namespace Rts.Sim.Tests.Pathing;

public class PathServiceTests
{
    private static readonly EntityId Bld = new(1, 1);

    /// <summary>Synthetic terrain: water/rock/cliff impassable, rough/ash slower, road fast for wheels.</summary>
    internal static TerrainTable Table()
    {
        int[] landSpeed = { 100, 100, 50, 80, 0, 0, 0, 0 }; // Clear Road Rough Ash Water Rock Cliff Shallow (M4-01)
        var sp = new int[RuleCounts.LandTypes * RuleCounts.Locomotions];
        for (int l = 0; l < RuleCounts.LandTypes; l++)
            for (int c = 0; c < RuleCounts.Locomotions; c++)
                sp[l * RuleCounts.Locomotions + c] = landSpeed[l];
        sp[(int)LandType.Road * RuleCounts.Locomotions + (int)Locomotion.Wheeled] = 200;
        return new TerrainTable(sp, new bool[RuleCounts.LandTypes]);
    }

    internal static MapState Flat(int w, int h) => new(MapData.CreateFlat(w, h));

    internal static long Cost(MapState m, TerrainTable t, Locomotion loco, CellCoord from, IReadOnlyList<CellCoord> path)
    {
        long total = 0;
        CellCoord prev = from;
        foreach (CellCoord c in path)
        {
            Assert.Equal(1, Math.Max(Math.Abs(c.X - prev.X), Math.Abs(c.Y - prev.Y)));
            Assert.True(m.BuildingAt(c).IsNone);
            int sp = m.SpeedPercent(c, loco, t);
            Assert.True(sp > 0, $"path crosses blocked cell {c}");
            bool diag = c.X != prev.X && c.Y != prev.Y;
            total += (diag ? 14 : 10) * (10000 / sp);
            prev = c;
        }
        return total;
    }

    private static (PathStatus, IReadOnlyList<CellCoord>) Solve(PathService s, CellCoord a, CellCoord b, Locomotion loco = Locomotion.Foot)
    {
        int id = s.Request(a, b, loco);
        s.Process();
        PathStatus st = s.Poll(id, 0, out var p);
        return (st, p);
    }

    [Fact]
    public void StraightPath()
    {
        var m = Flat(16, 16); var t = Table();
        var (st, p) = Solve(new PathService(m, t), new(2, 3), new(7, 3));
        Assert.Equal(PathStatus.Found, st);
        Assert.Equal(5, p.Count);
        Assert.Equal(new CellCoord(7, 3), p[^1]);
        Assert.DoesNotContain(new CellCoord(2, 3), p);
        Assert.Equal(5 * 10 * 100, Cost(m, t, Locomotion.Foot, new(2, 3), p));
    }

    [Fact]
    public void DiagonalPath()
    {
        var m = Flat(16, 16); var t = Table();
        var (st, p) = Solve(new PathService(m, t), new(1, 1), new(6, 6));
        Assert.Equal(PathStatus.Found, st);
        Assert.Equal(5, p.Count);
        Assert.Equal(5 * 14 * 100, Cost(m, t, Locomotion.Foot, new(1, 1), p));
    }

    [Fact]
    public void SameCellIsEmptyFound()
    {
        var (st, p) = Solve(new PathService(Flat(8, 8), Table()), new(3, 3), new(3, 3));
        Assert.Equal(PathStatus.Found, st);
        Assert.Empty(p);
    }

    [Fact]
    public void AvoidsRockWaterCliffAndBuildings()
    {
        var m = Flat(12, 12); var t = Table();
        // Wall at x = 5 with a single gap at y = 10, made of mixed blockers.
        for (int y = 0; y < 10; y++)
        {
            if (y % 3 == 0) m.SetLand(new(5, y), LandType.Rock);
            else if (y % 3 == 1) m.SetLand(new(5, y), LandType.Water);
            else m.SetLand(new(5, y), LandType.Cliff);
        }
        var (st, p) = Solve(new PathService(m, t), new(1, 1), new(9, 1));
        Assert.Equal(PathStatus.Found, st);
        Assert.Contains(new CellCoord(5, 10), p);
        Cost(m, t, Locomotion.Foot, new(1, 1), p);

        // A building in the gap closes (5,10); open (5,11) instead.
        m.SetBuilding(new(5, 10), Bld);
        var (st2, p2) = Solve(new PathService(m, t), new(1, 1), new(9, 1));
        Assert.Equal(PathStatus.Found, st2);
        Assert.Contains(new CellCoord(5, 11), p2);
        Assert.DoesNotContain(new CellCoord(5, 10), p2);
    }

    [Fact]
    public void WheeledPrefersCheaperRoad()
    {
        var m = Flat(16, 5); var t = Table();
        for (int x = 0; x < 16; x++) m.SetLand(new(x, 0), LandType.Road);
        for (int x = 0; x < 16; x++) m.SetLand(new(x, 1), LandType.Rough);
        var (st, p) = Solve(new PathService(m, t), new(0, 2), new(15, 2), Locomotion.Wheeled);
        Assert.Equal(PathStatus.Found, st);
        // Detour onto the road row is cheaper than the straight clear row.
        Assert.Contains(p, c => c.Y == 0);
        long straight = 15 * 10 * 100;
        Assert.True(Cost(m, t, Locomotion.Wheeled, new(0, 2), p) < straight);
    }

    [Fact]
    public void CornerCuttingForbidden()
    {
        var m = Flat(6, 6); var t = Table();
        m.SetLand(new(3, 2), LandType.Rock); // diagonal (2,2)->(3,3) would squeeze past (3,2)
        var (st, p) = Solve(new PathService(m, t), new(2, 2), new(3, 3));
        Assert.Equal(PathStatus.Found, st);
        Assert.Equal(2, p.Count);
        Assert.Equal(new CellCoord(2, 3), p[0]);
        // Two blockers on both orthogonals: no diagonal at all.
        m.SetBuilding(new(2, 3), Bld);
        var (st2, p2) = Solve(new PathService(m, t), new(2, 2), new(3, 3));
        Assert.Equal(PathStatus.Found, st2);
        Assert.True(p2.Count > 2);
    }

    [Fact]
    public void PartialWhenGoalEnclosed()
    {
        var m = Flat(12, 12); var t = Table();
        for (int x = 7; x <= 9; x++)
            for (int y = 7; y <= 9; y++)
                if (x != 8 || y != 8) m.SetLand(new(x, y), LandType.Rock);
        var (st, p) = Solve(new PathService(m, t), new(1, 8), new(8, 8));
        Assert.Equal(PathStatus.Partial, st);
        CellCoord end = p[^1];
        Assert.Equal(6, end.X); // closest reachable by Chebyshev is column 6 (distance 2)
        Assert.Equal(2, Math.Max(Math.Abs(end.X - 8), Math.Abs(end.Y - 8)));
        Assert.Equal(new CellCoord(6, 6), end); // tie by lowest cell index among distance-2 cells
        Cost(m, t, Locomotion.Foot, new(1, 8), p);
    }

    [Fact]
    public void BlockedGoalAndOutsideMap()
    {
        var m = Flat(8, 8); var t = Table();
        m.SetBuilding(new(5, 5), Bld);
        var (st, p) = Solve(new PathService(m, t), new(1, 1), new(5, 5));
        Assert.Equal(PathStatus.Partial, st);
        Assert.Equal(1, Math.Max(Math.Abs(p[^1].X - 5), Math.Abs(p[^1].Y - 5)));
        Assert.Equal(PathStatus.NotFound, Solve(new PathService(m, t), new(1, 1), new(50, 5)).Item1);
    }

    [Fact]
    public void PollReleasesAndCancelWorks()
    {
        var s = new PathService(Flat(8, 8), Table());
        int a = s.Request(new(0, 0), new(5, 5), Locomotion.Foot);
        int b = s.Request(new(0, 0), new(6, 6), Locomotion.Foot);
        Assert.True(b > a);
        Assert.Equal(PathStatus.Pending, s.Poll(a, 0, out _));
        s.Cancel(b);
        Assert.Equal(PathStatus.Unknown, s.Poll(b, 0, out _));
        s.Process();
        Assert.Equal(PathStatus.Found, s.Poll(a, 0, out _));
        Assert.Equal(PathStatus.Unknown, s.Poll(a, 0, out _));
        Assert.Equal(PathStatus.Unknown, s.Poll(999, 0, out _));
    }

    private static CellCoord[] Corners() => new[] { new CellCoord(0, 0) };

    [Fact]
    public void RequestsQueuedTogetherAreSolvedTogether_ThenDebtDelaysTheNextBatch()
    {
        // M2-01: everything queued when Process runs is solved in that call (so the first and the last asker of a tick
        // are served alike); the overdraft is carried as debt and delays the NEXT batch.
        var m = Flat(64, 64); var t = Table();
        // A group search to the far corner settles nearly all 4096 cells: more than 2500, less than 5000.
        var s = new PathService(m, t, 2500);
        int a = s.RequestGroup(Corners(), new(63, 63), Locomotion.Foot);
        int b = s.RequestGroup(Corners(), new(63, 63), Locomotion.Foot);
        s.Process();
        Assert.Equal(PathStatus.Found, s.Poll(a, 0, out _));
        Assert.Equal(PathStatus.Found, s.Poll(b, 0, out _));
        int c = s.Request(new(0, 0), new(5, 0), Locomotion.Foot);
        s.Process();   // two searches of ~4000 each against a budget of 2500: debt of about 5500
        Assert.Equal(PathStatus.Pending, s.Poll(c, 0, out _));
        int ticks = 1;
        while (s.Poll(c, 0, out _) == PathStatus.Pending && ticks < 100) { s.Process(); ticks++; }
        Assert.InRange(ticks, 2, 4);
    }

    [Fact]
    public void LargeDebtSpansSeveralTicks()
    {
        var m = Flat(64, 64); var t = Table();
        var s = new PathService(m, t, 500);
        int a = s.RequestGroup(Corners(), new(63, 63), Locomotion.Foot);
        s.Process();
        Assert.Equal(PathStatus.Found, s.Poll(a, 0, out _));
        int b = s.Request(new(0, 0), new(5, 0), Locomotion.Foot);
        int ticks = 1;
        s.Process();
        while (s.Poll(b, 0, out _) == PathStatus.Pending && ticks < 100) { s.Process(); ticks++; }
        Assert.InRange(ticks, 6, 10); // about 4000 cells of debt repaid at 500 per tick
    }

    [Fact]
    public void CacheHitChargesSameCost()
    {
        var m = Flat(48, 48); var t = Table();
        var rnd = new Lcg(7);
        var reqs = new List<(CellCoord, CellCoord)>();
        for (int i = 0; i < 40; i++)
        {
            var a = new CellCoord(rnd.Next(48), rnd.Next(48));
            var b = new CellCoord(rnd.Next(48), rnd.Next(48));
            reqs.Add((a, b)); reqs.Add((a, b)); // duplicates become cache hits
        }
        List<string> Run(bool cache)
        {
            var s = new PathService(m, t, 300) { CacheEnabled = cache };
            var ids = reqs.Select(r => s.Request(r.Item1, r.Item2, Locomotion.Foot)).ToList();
            var log = new List<string>();
            for (int tick = 0; tick < 400; tick++)
            {
                s.Process();
                for (int i = 0; i < ids.Count; i++)
                {
                    if (ids[i] < 0) continue;
                    var st = s.Poll(ids[i], 0, out var p);
                    if (st == PathStatus.Pending) continue;
                    log.Add($"{tick}:{i}:{st}:{p.Count}");
                    ids[i] = -1;
                }
            }
            return log;
        }
        var on = Run(true); var off = Run(false);
        Assert.Equal(reqs.Count, on.Count);
        Assert.Equal(off, on);
    }

    [Fact]
    public void CacheInvalidatedByMapVersion()
    {
        var m = Flat(16, 16); var t = Table();
        var s = new PathService(m, t);
        var (_, p1) = Solve(s, new(0, 5), new(15, 5));
        for (int y = 0; y < 15; y++) m.SetBuilding(new(8, y), Bld);
        var (st, p2) = Solve(s, new(0, 5), new(15, 5));
        Assert.Equal(PathStatus.Found, st);
        Assert.NotEqual(p1.Count, p2.Count);
        Assert.Contains(new CellCoord(8, 15), p2);
    }

    [Fact]
    public void SaveLoadMidQueueGivesSameResults()
    {
        var m = Flat(40, 40); var t = Table();
        m.SetLand(new(20, 10), LandType.Rock);
        var s = new PathService(m, t, 100);
        var ids = new List<int>();
        for (int i = 0; i < 6; i++) ids.Add(s.Request(new(i, 0), new(39 - i, 39), Locomotion.Foot));
        ids.Add(s.RequestGroup(new[] { new CellCoord(1, 1), new CellCoord(3, 2) }, new(30, 30), Locomotion.Foot));
        s.Process();
        s.Process();

        var ms = new MemoryStream();
        using (var w = new BinaryWriter(ms, System.Text.Encoding.UTF8, true)) s.Write(w);
        var s2 = new PathService(m, t, 100);
        ms.Position = 0;
        using (var r = new BinaryReader(ms)) s2.Read(r);

        string Drain(PathService x)
        {
            var log = new List<string>();
            for (int tick = 0; tick < 200; tick++)
            {
                x.Process();
                foreach (int id in ids)
                    for (int idx = 0; idx < 2; idx++)
                    {
                        var st = x.Poll(id, idx, out var p);
                        if (st is PathStatus.Pending or PathStatus.Unknown) continue;
                        log.Add($"{tick}:{id}:{idx}:{st}:" + string.Join(",", p.Select(c => c.X + "." + c.Y)));
                    }
            }
            return string.Join("|", log);
        }
        string a = Drain(s), b = Drain(s2);
        Assert.NotEqual("", a);
        Assert.Equal(a, b);

        // Saved bytes themselves round-trip.
        var s3 = new PathService(m, t, 100);
        s3.Request(new(0, 0), new(5, 5), Locomotion.Foot);
        var m1 = new MemoryStream();
        using (var w = new BinaryWriter(m1, System.Text.Encoding.UTF8, true)) s3.Write(w);
        var s4 = new PathService(m, t, 100);
        m1.Position = 0;
        using (var r = new BinaryReader(m1, System.Text.Encoding.UTF8, true)) s4.Read(r);
        var m2 = new MemoryStream();
        using (var w = new BinaryWriter(m2, System.Text.Encoding.UTF8, true)) s4.Write(w);
        Assert.Equal(m1.ToArray(), m2.ToArray());
        Assert.Equal("paths", s4.Name);
    }

    [Fact]
    public void GroupLengthsAreOptimal()
    {
        var m = Flat(40, 40); var t = Table();
        for (int y = 5; y < 30; y++) m.SetLand(new(20, y), LandType.Rock);
        for (int x = 0; x < 40; x++) m.SetLand(new(x, 33), LandType.Road);
        var froms = new List<CellCoord> { new(2, 2), new(5, 20), new(10, 35), new(15, 15), new(2, 2) };
        var to = new CellCoord(30, 15);
        foreach (var loco in new[] { Locomotion.Foot, Locomotion.Wheeled })
        {
            var s = new PathService(m, t);
            int g = s.RequestGroup(froms, to, loco);
            s.Process();
            for (int i = 0; i < froms.Count; i++)
            {
                Assert.Equal(PathStatus.Found, s.Poll(g, i, out var gp));
                Assert.Equal(to, gp[^1]);
                var single = Solve(new PathService(m, t), froms[i], to, loco);
                Assert.Equal(PathStatus.Found, single.Item1);
                Assert.Equal(Cost(m, t, loco, froms[i], single.Item2), Cost(m, t, loco, froms[i], gp));
            }
        }
    }

    [Fact]
    public void GroupSolvesAllStartsInOneProcess()
    {
        var m = Flat(40, 40); var t = Table();
        var froms = new List<CellCoord>();
        for (int i = 0; i < 8; i++) froms.Add(new(2 + i % 4, 2 + i / 4));
        var s = new PathService(m, t, 1);
        int g = s.RequestGroup(froms, new(35, 35), Locomotion.Foot);
        s.Process();
        Assert.Equal(PathStatus.Found, s.Poll(g, 7, out _));
    }

    [Fact]
    public void DeterministicAcrossInstances()
    {
        var m = Flat(32, 32); var t = Table();
        var rnd = new Lcg(99);
        for (int i = 0; i < 80; i++) m.SetLand(new(rnd.Next(32), rnd.Next(32)), LandType.Rock);
        string Run()
        {
            var s = new PathService(m, t, 500);
            var rr = new Lcg(5);
            var ids = new List<int>();
            for (int i = 0; i < 30; i++)
                ids.Add(s.Request(new(rr.Next(32), rr.Next(32)), new(rr.Next(32), rr.Next(32)), (Locomotion)rr.Next(3)));
            var log = new List<string>();
            for (int tick = 0; tick < 100; tick++)
            {
                s.Process();
                foreach (int id in ids)
                {
                    var st = s.Poll(id, 0, out var p);
                    if (st is PathStatus.Pending or PathStatus.Unknown) continue;
                    log.Add($"{tick}:{id}:{st}:" + string.Join(",", p.Select(c => c.X + "." + c.Y)));
                }
            }
            return string.Join("|", log);
        }
        Assert.Equal(Run(), Run());
    }

    [Fact]
    public void PerfSanityOnGeneratedLikeMap()
    {
        var m = Flat(64, 64); var t = Table();
        var rnd = new Lcg(2027);
        for (int b = 0; b < 14; b++)
        {
            int cx = rnd.Next(64), cy = rnd.Next(64), r = 2 + rnd.Next(4);
            for (int y = cy - r; y <= cy + r; y++)
                for (int x = cx - r; x <= cx + r; x++)
                    if (x >= 0 && y >= 0 && x < 64 && y < 64 && (x - cx) * (x - cx) + (y - cy) * (y - cy) <= r * r)
                        m.SetLand(new(x, y), LandType.Rock);
        }
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var s = new PathService(m, t, 4000);
        var ids = new List<int>();
        for (int i = 0; i < 100; i++)
            ids.Add(s.Request(new(rnd.Next(64), rnd.Next(64)), new(rnd.Next(64), rnd.Next(64)), Locomotion.Foot));
        int done = 0, ticks = 0;
        while (done < ids.Count && ticks < 500)
        {
            s.Process(); ticks++;
            for (int i = 0; i < ids.Count; i++)
            {
                if (ids[i] < 0) continue;
                var st = s.Poll(ids[i], 0, out _);
                if (st == PathStatus.Pending) continue;
                Assert.NotEqual(PathStatus.Unknown, st);
                ids[i] = -1; done++;
            }
        }
        Assert.Equal(100, done);
        Assert.True(sw.ElapsedMilliseconds < 2000, $"took {sw.ElapsedMilliseconds} ms");
    }
}

/// <summary>Tiny fixed-seed generator for test setup only.</summary>
internal sealed class Lcg
{
    private uint _s;
    public Lcg(uint seed) { _s = seed * 2654435761u + 1; }
    public int Next(int n) { _s = _s * 1664525u + 1013904223u; return (int)((_s >> 8) % (uint)n); }
}
