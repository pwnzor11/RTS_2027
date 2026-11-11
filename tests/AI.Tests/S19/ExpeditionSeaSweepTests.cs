// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Data;
using Rts.Sim;
using Rts.Sim.Core;
using Rts.Sim.Map;
using Rts.Sim.Rules;
using Xunit.Abstractions;

namespace AI.Tests.S19;

/// <summary>
/// M13-01 (S18 AI-02 / AI-03, D-205): on Mountains layout 1 every Expedition start gets its Command Ship on sea that reaches the map edge
/// (never none, never an inland lake), for every ocean setting the game offers an Expedition.
/// </summary>
public class ExpeditionSeaSweepTests(ITestOutputHelper output)
{
    private static SimRules Rules => TestWorld.Rules.Sim;

    public static IEnumerable<object[]> Cases()
    {
        foreach (MapSize size in new[] { MapSize.Small, MapSize.Medium })
            foreach (OceanEdges ocean in new[] { OceanEdges.North | OceanEdges.South, OceanEdges.North })
                for (ulong seed = 1; seed <= 12; seed++)
                    yield return new object[] { size, ocean, seed };
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public void Mountains_Layout1_Expedition_EveryStartGetsACommandShipOnEdgeConnectedSea(MapSize size, OceanEdges ocean, ulong seed)
    {
        var gen = MapGenerator.GenerateWithMeta(new MapGenParams(Biome.Mountains, size, 2, seed, null, ocean, 1));
        var sim = new Simulation(new SimSetup(seed, gen.Map, 2, Rules, TestWorld.Rules.RulesHash)
        { Factions = [Faction.Expedition, Faction.Expedition], LandingMarkers = gen.Meta.Landing });
        SkirmishStart.Apply(sim);
        bool[] edgeSea = EdgeConnectedSea(sim);
        int w = sim.Setup.Map.Width;
        for (byte p = 0; p < 2; p++)
        {
            var ships = new List<CellCoord>();
            for (int i = 1; i < sim.Units.Capacity; i++)
                if (sim.Units.Slots.IsIndexAlive(i) && sim.Units.Player[i] == p && Rules.Vehicles[sim.Units.TypeIndex[i]].Id == "exp_command_ship")
                    ships.Add(sim.Units.Position[i].ToCell());
            CellCoord s = sim.Setup.Map.StartPositions[p];
            output.WriteLine($"{size} {ocean} seed {seed} p{p} start {s}: ships {string.Join(",", ships)}");
            Assert.Single(ships);
            Assert.True(edgeSea[ships[0].Y * w + ships[0].X], $"p{p}: the Command Ship's water does not reach the map edge");
        }
    }

    /// <summary>The generator's own guarantee (no SkirmishStart fallback involved): sea that reaches the map edge lies within 16 cells of every start.</summary>
    [Theory]
    [MemberData(nameof(Cases))]
    public void Mountains_Layout1_Generator_PutsEdgeConnectedSeaWithinRing16OfEveryStart(MapSize size, OceanEdges ocean, ulong seed)
    {
        MapData m = MapGenerator.GenerateWithMeta(new MapGenParams(Biome.Mountains, size, 2, seed, null, ocean, 1)).Map;
        int w = m.Width, h = m.Height;
        var seen = new bool[w * h];
        var q = new Queue<int>();
        for (int i = 0; i < w * h; i++)
            if ((i % w == 0 || i / w == 0 || i % w == w - 1 || i / w == h - 1) && Rts.Data.MapGen.MapAnalysis.BuiltInPassable(m.Land[i], Locomotion.Naval)) { seen[i] = true; q.Enqueue(i); }
        while (q.Count > 0)
        {
            int i = q.Dequeue();
            for (int dy = -1; dy <= 1; dy++)
                for (int dx = -1; dx <= 1; dx++)
                {
                    int x = i % w + dx, y = i / w + dy;
                    if (x < 0 || y < 0 || x >= w || y >= h || seen[y * w + x] || !Rts.Data.MapGen.MapAnalysis.BuiltInPassable(m.Land[y * w + x], Locomotion.Naval)) continue;
                    seen[y * w + x] = true; q.Enqueue(y * w + x);
                }
        }
        foreach (CellCoord s in m.StartPositions)
        {
            int best = int.MaxValue;
            for (int i = 0; i < w * h; i++) if (seen[i]) best = Math.Min(best, Math.Max(Math.Abs(i % w - s.X), Math.Abs(i / w - s.Y)));
            output.WriteLine($"{size} {ocean} seed {seed} start {s}: nearest edge-connected sea {best}");
            Assert.True(best <= 16, $"start {s}: nearest edge-connected sea is {best} cells away");
        }
    }

    /// <summary>FACTION-LE-02 case c: with no edge-connected water anywhere (no ocean chosen) the Command Ship still spawns, on the nearest water.</summary>
    [Theory]
    [InlineData(1UL)]
    [InlineData(2UL)]
    [InlineData(3UL)]
    public void NoEdgeWater_CommandShipStillSpawnsOnTheBestWater(ulong seed)
    {
        var gen = MapGenerator.GenerateWithMeta(new MapGenParams(Biome.Mountains, MapSize.Medium, 2, seed, null, OceanEdges.None, 1));
        var sim = new Simulation(new SimSetup(seed, gen.Map, 2, Rules, TestWorld.Rules.RulesHash) { Factions = [Faction.Expedition, Faction.Expedition] });
        SkirmishStart.Apply(sim);
        bool anyEdge = Array.IndexOf(EdgeConnectedSea(sim), true) >= 0;
        bool anyWater = false;
        for (int y = 0; y < gen.Map.Height; y++)
            for (int x = 0; x < gen.Map.Width; x++) anyWater |= sim.Map.IsPassableTerrain(new CellCoord(x, y), Locomotion.Naval, Rules.Terrain);
        output.WriteLine($"seed {seed}: edge water {anyEdge}, any water {anyWater}");
        int ships = 0;
        for (int i = 1; i < sim.Units.Capacity; i++)
            if (sim.Units.Slots.IsIndexAlive(i) && Rules.Vehicles[sim.Units.TypeIndex[i]].Id == "exp_command_ship") ships++;
        Assert.Equal(anyWater ? 2 : 0, ships);
    }

    private static bool[] EdgeConnectedSea(Simulation sim)
    {
        MapData m = sim.Setup.Map;
        int w = m.Width, h = m.Height;
        var seen = new bool[w * h];
        var q = new Queue<CellCoord>();
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
                if ((x == 0 || y == 0 || x == w - 1 || y == h - 1) && sim.Map.IsPassableTerrain(new CellCoord(x, y), Locomotion.Naval, Rules.Terrain))
                { seen[y * w + x] = true; q.Enqueue(new CellCoord(x, y)); }
        while (q.Count > 0)
        {
            CellCoord c = q.Dequeue();
            for (int dy = -1; dy <= 1; dy++)
                for (int dx = -1; dx <= 1; dx++)
                {
                    var n = new CellCoord(c.X + dx, c.Y + dy);
                    if (n.X < 0 || n.Y < 0 || n.X >= w || n.Y >= h || seen[n.Y * w + n.X]) continue;
                    if (!sim.Map.IsPassableTerrain(n, Locomotion.Naval, Rules.Terrain)) continue;
                    seen[n.Y * w + n.X] = true; q.Enqueue(n);
                }
        }
        return seen;
    }
}
