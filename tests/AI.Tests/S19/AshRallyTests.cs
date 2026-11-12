// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.AI;
using Rts.AI.Recruit;
using Rts.Data;
using Rts.Sim;
using Rts.Sim.Core;
using Rts.Sim.Map;
using Rts.Sim.Rules;
using Rts.Sim.View;

namespace AI.Tests.S19;

/// <summary>S19 M13-12 (owner option a): the AI never parks its soldiers on an ashfall / pitchblende field (they hurt, D-207).</summary>
public class AshRallyTests
{
    private static Simulation WithField(int x0, int y0, int x1, int y1, OverlayKind kind = OverlayKind.Ashfall)
    {
        Simulation sim = TestWorld.FlatSim(2, 32);
        for (int y = y0; y <= y1; y++)
            for (int x = x0; x <= x1; x++)
                sim.Map.SetOverlay(new CellCoord(x, y), kind, 5);
        return sim;
    }

    [Fact]
    public void A_cell_off_any_field_is_kept()
    {
        Simulation sim = WithField(10, 10, 14, 14);
        Assert.Equal(new CellCoord(5, 5), AshCells.OffField(sim.ViewFor(0), new CellCoord(5, 5)));
    }

    [Theory]
    [InlineData(OverlayKind.Ashfall)]
    [InlineData(OverlayKind.Pitchblende)]
    public void A_field_cell_moves_to_the_nearest_clean_cell(OverlayKind kind)
    {
        Simulation sim = WithField(10, 10, 14, 14, kind);
        PlayerView view = sim.ViewFor(0);
        CellCoord c = AshCells.OffField(view, new CellCoord(12, 12));   // centre of a 5x5 field: 3 rings out
        Assert.False(AshCells.IsField(view, c));
        Assert.Equal(3, Math.Max(Math.Abs(c.X - 12), Math.Abs(c.Y - 12)));
        Assert.Equal(new CellCoord(9, 9), c);   // fixed scan order: the first cell of the ring, row by row
        Assert.Equal(new CellCoord(10, 9), AshCells.OffField(view, new CellCoord(11, 10)));   // edge cell: one ring out
    }

    [Fact]
    public void A_field_too_big_to_leave_keeps_the_cell()
    {
        Simulation sim = WithField(0, 0, 31, 31);
        Assert.Equal(new CellCoord(16, 16), AshCells.OffField(sim.ViewFor(0), new CellCoord(16, 16)));
    }

    /// <summary>Records every rally the AI sets.</summary>
    private sealed class RallyLog(IAiPlayer inner) : IAiPlayer
    {
        public readonly List<CellCoord> Rallies = new();
        public string Id => inner.Id;
        public void Start(AiContext ctx) => inner.Start(ctx);
        public void Think(PlayerView view, ICommandSink sink) => inner.Think(view, new Sink(this, sink));

        private sealed class Sink(RallyLog owner, ICommandSink real) : ICommandSink
        {
            public void Issue(CommandKind kind, EntityId subject, ActorKind subjectKind, CellCoord cell = default,
                ActorRef target = default, int arg2 = 0) => real.Issue(kind, subject, subjectKind, cell, target, arg2);
            public void Build(ProductionCategory category, int typeIndex) => real.Build(category, typeIndex);
            public void Place(int buildingType, CellCoord topLeft) => real.Place(buildingType, topLeft);
            public void Rally(EntityId factory, CellCoord cell) { owner.Rallies.Add(cell); real.Rally(factory, cell); }
        }
    }

    [Fact]
    public void The_recruit_rally_point_avoids_an_ash_field_in_front_of_the_base()
    {
        var sim = new Simulation(new SimSetup(1, MapData.CreateFlat(64, 64), 2, TestWorld.Rules.Sim, TestWorld.Rules.RulesHash));
        SkirmishStart.Apply(sim);
        PlayerView v0 = sim.ViewFor(0);
        var own = new List<ViewActor>();
        v0.CopyOwn(own);
        CellCoord yard = own.First(a => a.Kind == ActorKind.Building).Cell;
        // A 5x5 field on every cell the rally search would try (2..7 cells from the yard toward the map centre).
        int cx = yard.X + 1, cy = yard.Y + 1, dx = Math.Sign(32 - cx), dy = Math.Sign(32 - cy);
        for (int d = 2; d <= 7; d++)
            for (int oy = -2; oy <= 2; oy++)
                for (int ox = -2; ox <= 2; ox++)
                {
                    var c = new CellCoord(cx + dx * d + ox, cy + dy * d + oy);
                    if (v0.InMap(c) && sim.Map.BuildingAt(c).IsNone) sim.Map.SetOverlay(c, OverlayKind.Ashfall, 5);
                }

        DifficultyProfile recruit = TestWorld.Tiers[0];
        var log = new RallyLog(new RecruitAi(recruit));
        var driver = new AiDriver(1);
        driver.Add(0, log, recruit);
        var pending = new List<Command>();
        for (int t = 0; t < 4000 && log.Rallies.Count == 0; t++)
        {
            driver.Collect(sim, pending);
            sim.Step(pending);
            pending.RemoveAll(c => c.ExecTick < sim.Tick);
        }
        Assert.NotEmpty(log.Rallies);
        Assert.All(log.Rallies, c => Assert.False(AshCells.IsField(sim.ViewFor(0), c), $"rally {c} on a field"));
    }
}
