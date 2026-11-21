// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.AI;
using Rts.AI.Recruit;
using Rts.Data;
using Rts.Sim;
using Rts.Sim.Core;
using Rts.Sim.Map;
using Rts.Sim.Rules;
using Xunit;

namespace AI.Tests.S37;

/// <summary>M13-34: the sim turns a south-half building's dock and exit by 180 degrees; the AI's placement lanes and mouths must follow the
/// same rule (one shared function, <see cref="BuildingOrientation"/>), or the AI walls in the real dock.</summary>
public sealed class DockOrientationTests
{
    private const int Size = 64;

    [Fact]
    public void SouthHalfRefinery_KeepsTheTurnedLaneFree_AndNeverBuildsOnTheTurnedDock()
    {
        SimRules rules = TestWorld.Rules.Sim;
        int yard = SimRules.IndexOf(rules.Buildings, "auth_conyard", b => b.Id), power = SimRules.IndexOf(rules.Buildings, "auth_power", b => b.Id);
        int refinery = SimRules.IndexOf(rules.Buildings, "auth_refinery", b => b.Id);
        var sim = new Simulation(new SimSetup(1, MapData.CreateFlat(Size, Size), 2, rules, TestWorld.Rules.RulesHash));
        var yardAt = new CellCoord(20, 44);   // south half: every footprint here is turned
        Assert.False(sim.PlaceBuilding(0, yard, yardAt).IsNone);
        Assert.False(sim.PlaceBuilding(1, yard, new CellCoord(Size - 23, 17)).IsNone);
        var refAt = new CellCoord(yardAt.X + 5, yardAt.Y);
        Assert.False(sim.PlaceBuilding(0, refinery, refAt).IsNone);
        BuildingType rt = rules.Buildings[refinery];
        CellCoord rel = sim.ViewFor(0).OrientedOffset(rt.Dock, rt.FootprintW, rt.FootprintH, refAt);
        Assert.Equal(new CellCoord(rt.FootprintW - 1 - rt.Dock.X, rt.FootprintH - 1 - rt.Dock.Y), rel);   // the sim turned it
        var dock = new CellCoord(refAt.X + rel.X, refAt.Y + rel.Y);

        var covered = new HashSet<(int, int)>();
        var search = new PlacementSearch();
        BuildingType pt = rules.Buildings[power];
        for (int i = 0; i < 30; i++)
        {
            var k = new RecruitKnowledge();
            k.Refresh(sim.ViewFor(0), "auth_conyard");
            Assert.True(search.Find(k, power, false, out CellCoord c));
            Assert.False(sim.PlaceBuilding(0, power, c).IsNone);
            for (int y = c.Y; y < c.Y + pt.FootprintH; y++)
                for (int x = c.X; x < c.X + pt.FootprintW; x++) covered.Add((x, y));
        }
        // the real lane runs NORTH from the turned dock (3 wide, 4 deep, mouth in its last row)
        for (int y = dock.Y - 3; y <= dock.Y; y++)
            for (int x = dock.X - 1; x <= dock.X + 1; x++)
                Assert.DoesNotContain((x, y), covered);
    }

    [Fact]
    public void BuildingOrientation_MatchesTheSimOnEveryHalf()
    {
        SimRules rules = TestWorld.Rules.Sim;
        var sim = new Simulation(new SimSetup(1, MapData.CreateFlat(Size, Size), 2, rules, TestWorld.Rules.RulesHash));
        foreach (var tl in new[] { new CellCoord(5, 5), new CellCoord(5, 40), new CellCoord(50, 30), new CellCoord(50, 60) })
            Assert.Equal(sim.ViewFor(0).OrientedOffset(new CellCoord(1, 3), 3, 3, tl),
                BuildingOrientation.Oriented(new CellCoord(1, 3), 3, 3, tl, Size, Size, sim.Setup.Map.StartPositions));
    }

    /// <summary>The reproduction: Mountains layout 1 (mid-row mirror starts), Left Behind at the south-east start versus Authority, tier 3,
    /// seed 3 (the game <c>ai match --size 96 --layout 1 --biome mountains --seeds 4</c> plays as seed 3, game 1). Before the fix the Left Behind
    /// side walled its real dock and had harvested ~1 850 credits by 3 min / ~3 650 by 5 min; with the shared rule it is at the Rust Belt level (~20 000 at 5 min).</summary>
    [Fact]
    public void LeftBehindOnMountainsLayout1_NoLongerStarvesBehindAWalledDock()
    {
        var map = MapGenerator.GenerateWithMeta(new MapGenParams(Biome.Mountains, MapSize.Medium, 2, 3, null, OceanEdges.None, 1)).Map;
        DifficultyProfile p = TestWorld.Tiers[2];
        var a = new AiSide(AiRegistry.TieredId, p, Faction.Authority);
        var b = new AiSide(AiRegistry.TieredId, p, Faction.LeftBehind);
        int lb3 = 0, lb5 = 0;
        AiMatchRunner.Play(map, TestWorld.Rules.Sim, TestWorld.Rules.RulesHash, 3 * 16, a, b, true, 6000, swapStarts: false, start: AiStartMode.Economy,
            observe: (sim, x, y, sa, sb) =>
            {
                if (sim.Tick != 3600 && sim.Tick != 6000) return;
                var ps = sim.StatsOf(sb);
                if (sim.Tick == 3600) lb3 = ps.CreditsHarvested + ps.ShipmentIncome; else lb5 = ps.CreditsHarvested + ps.ShipmentIncome;
            });
        Assert.True(lb5 > 12000, $"Left Behind harvested {lb3} by 3 min and {lb5} by 5 min");
    }
}
