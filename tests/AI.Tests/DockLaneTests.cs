// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using AI.Tests.Tiers;
using Rts.Data;
using Rts.Sim.Core;
using Rts.Sim.Rules;
using Xunit;

namespace AI.Tests;

/// <summary>D-157 (M7-19): in a cramped corner base the placement fallback used to put a structure on the refinery's dock cell, which
/// left every harvester in Return for good (no income, then the AI sold its own base). No tier of the search may cover a dock or exit cell.</summary>
public sealed class DockLaneTests
{
    [Theory]
    [InlineData(Faction.LeftBehind)]
    [InlineData(Faction.Authority)]
    public void NoStructureStandsOnARefineryDockOrFactoryExit(Faction faction)
    {
        var rig = new TierRig(faction, TestWorld.Tiers[4]);
        rig.Run(15 * 60 * 20);
        var sim = rig.Sim;
        var b = sim.Buildings;
        var rules = sim.RulesOf(0);
        for (int i = 1; i < b.Capacity; i++)
        {
            if (!b.Slots.IsIndexAlive(i) || b.Player[i] != 0) continue;
            BuildingType bt = rules.Buildings[b.TypeIndex[i]];
            var cells = new List<(int X, int Y, string What)>();
            if (bt.Refinery) cells.Add((b.Cell[i].X + bt.Dock.X, b.Cell[i].Y + bt.Dock.Y, "dock of " + bt.Id));
            if (bt.Produces != null) cells.Add((b.Cell[i].X + bt.Exit.X, b.Cell[i].Y + bt.Exit.Y, "exit of " + bt.Id));
            foreach (var (x, y, what) in cells)
                for (int j = 1; j < b.Capacity; j++)
                {
                    if (!b.Slots.IsIndexAlive(j) || b.Player[j] != 0 || j == i) continue;
                    BuildingType o = rules.Buildings[b.TypeIndex[j]];
                    bool inside = x >= b.Cell[j].X && x < b.Cell[j].X + o.FootprintW && y >= b.Cell[j].Y && y < b.Cell[j].Y + o.FootprintH;
                    Assert.False(inside, $"{o.Id} at {b.Cell[j].X},{b.Cell[j].Y} covers the {what} cell {x},{y}");
                }
        }
    }
}
