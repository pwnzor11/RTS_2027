// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Data;
using Rts.Sim;
using Rts.Sim.Core;
using Rts.Sim.Map;
using Rts.Sim.Rules;
using Xunit.Abstractions;

namespace AI.Tests.S18;

/// <summary>
/// S18 audit: the skirmish start on the real generator and rules. AI-02 (no Command Ship when the sea is beyond the ring-16 fallback),
/// AI-03 (Command Ship on an inland lake), FACTION-LE-02 (an Expedition player that never had a building is never defeated) and
/// DATA-TOOLS-01 / AI-08 item 1 (start units placed by one fixed scan order, so start 1 gets a translation of start 0's layout, not its
/// image). Maps are built the way <c>ai match</c> builds them (an Expedition side gets ocean on the north and south edges).
/// </summary>
public class StartPlacementAuditTests(ITestOutputHelper output)
{
    private static SimRules Rules => TestWorld.Rules.Sim;

    private static Simulation Start(Biome biome, int layout, ulong seed, Faction faction, bool debug = false)
    {
        OceanEdges ocean = faction == Faction.Expedition ? OceanEdges.North | OceanEdges.South : OceanEdges.None;
        var gen = MapGenerator.GenerateWithMeta(new MapGenParams(biome, MapSize.Medium, 2, seed, null, ocean, layout));
        var sim = new Simulation(new SimSetup(seed, gen.Map, 2, Rules, TestWorld.Rules.RulesHash)
        { Factions = [faction, faction], LandingMarkers = gen.Meta.Landing, AllowDebug = debug, NoRebuildDefeat = true });
        SkirmishStart.Apply(sim);
        return sim;
    }

    private static List<(int Slot, CellCoord Cell, bool Naval, string Id)> Vehicles(Simulation sim, byte p)
    {
        var list = new List<(int, CellCoord, bool, string)>();
        for (int i = 1; i < sim.Units.Capacity; i++)
            if (sim.Units.Slots.IsIndexAlive(i) && sim.Units.Player[i] == p)
                list.Add((i, sim.Units.Position[i].ToCell(), Rules.Vehicles[sim.Units.TypeIndex[i]].Locomotion == Locomotion.Naval, Rules.Vehicles[sim.Units.TypeIndex[i]].Id));
        return list;
    }

    private static int Infantry(Simulation sim, byte p)
    {
        int n = 0;
        for (int i = 1; i < sim.Infantry.Capacity; i++) if (sim.Infantry.Slots.IsIndexAlive(i) && sim.Infantry.Player[i] == p) n++;
        return n;
    }

    /// <summary>Chebyshev distance from <paramref name="from"/> to the nearest naval-passable cell (any, or only cells whose naval region touches the map edge).</summary>
    private static int NearestSea(Simulation sim, CellCoord from, bool edgeOnly)
    {
        MapData m = sim.Setup.Map;
        bool[] edgeSea = EdgeConnectedSea(sim);
        int best = int.MaxValue;
        for (int y = 0; y < m.Height; y++)
            for (int x = 0; x < m.Width; x++)
            {
                var c = new CellCoord(x, y);
                if (!sim.Map.IsPassableTerrain(c, Locomotion.Naval, Rules.Terrain) || (edgeOnly && !edgeSea[y * m.Width + x])) continue;
                best = Math.Min(best, Math.Max(Math.Abs(x - from.X), Math.Abs(y - from.Y)));
            }
        return best;
    }

    /// <summary>Naval-passable cells connected (8-way) to a naval-passable cell on the map edge.</summary>
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

    // ---- AI-02 ------------------------------------------------------------------------------------

    // M13-01: the Audit_ pin of AI-02 was removed when the fix landed (D-205); the twin below is the live test.
    [Theory]
    [InlineData(1UL)]
    [InlineData(5UL)]
    [InlineData(6UL)]
    public void Expedition_MountainsLayout1_Medium_GetsItsCommandShip(ulong seed)
    {
        Simulation sim = Start(Biome.Mountains, 1, seed, Faction.Expedition);
        for (byte p = 0; p < 2; p++) Assert.Contains(Vehicles(sim, p), v => v.Naval);
    }

    // ---- FACTION-LE-02 ----------------------------------------------------------------------------

    // S18 audit: pins current behaviour of FACTION-LE-02; flip the assertion when fixed.
    [Fact]
    public void Audit_Expedition_WithNoBuildingEver_IsNeverDefeated_EvenWithNothingLeft()
    {
        Simulation sim = Start(Biome.Mountains, 1, 1UL, Faction.Expedition, debug: true);   // formerly the AI-02 case (no Command Ship); the kill list covers whatever vehicles exist
        var kills = new List<Command>();
        ushort seq = 1;
        foreach (var v in Vehicles(sim, 0))
            kills.Add(new Command(0, 0, seq++, CommandKind.Debug, sim.Units.Slots.IdOf(v.Slot), default, default, (int)DebugOp.Kill, (int)ActorKind.Vehicle));
        for (int i = 1; i < sim.Infantry.Capacity; i++)
            if (sim.Infantry.Slots.IsIndexAlive(i) && sim.Infantry.Player[i] == 0)
                kills.Add(new Command(0, 0, seq++, CommandKind.Debug, sim.Infantry.Slots.IdOf(i), default, default, (int)DebugOp.Kill, (int)ActorKind.Infantry));
        sim.Step(kills);
        for (int t = 0; t < 3000; t++) sim.Step(Array.Empty<Command>());
        Assert.Empty(Vehicles(sim, 0));
        Assert.Equal(0, Infantry(sim, 0));
        Assert.False(sim.Players.Defeated[0], "player 0 owns nothing at all yet is not defeated");
    }

    // ---- AI-03 ------------------------------------------------------------------------------------

    // M13-01: the Audit_ pin of AI-03 was removed when the fix landed (D-205); the test below is the live one.
    [Fact]
    public void Expedition_CommandShip_SpawnsOnSeaThatReachesTheMapEdge()
    {
        Simulation sim = Start(Biome.Mountains, 1, 2UL, Faction.Expedition);
        bool[] edgeSea = EdgeConnectedSea(sim);
        for (byte p = 0; p < 2; p++)
        {
            var ship = Vehicles(sim, p).Single(v => v.Id == "exp_command_ship");
            Assert.True(edgeSea[ship.Cell.Y * sim.Setup.Map.Width + ship.Cell.X]);
        }
    }

    // ---- DATA-TOOLS-01 / AI-08 (1) ----------------------------------------------------------------

    /// <summary>Each player's start kit as offsets from its start: buildings by top-left, units by cell, sorted. Player 1's list is
    /// mapped back through the half turn (offset (dx,dy) becomes (-dx,-dy); a building's top-left also shifts by its footprint).</summary>
    private static List<string> Layout(Simulation sim, byte p, bool mapBack)
    {
        CellCoord s = sim.Setup.Map.StartPositions[p];
        var items = new List<string>();
        for (int i = 1; i < sim.Buildings.Capacity; i++)
            if (sim.Buildings.Slots.IsIndexAlive(i) && sim.Buildings.Player[i] == p)
            {
                BuildingType bt = Rules.Buildings[sim.Buildings.TypeIndex[i]];
                int dx = sim.Buildings.Cell[i].X - s.X, dy = sim.Buildings.Cell[i].Y - s.Y;
                if (mapBack) { dx = -dx - bt.FootprintW + 1; dy = -dy - bt.FootprintH + 1; }
                items.Add($"B {bt.Id} {dx},{dy}");
            }
        for (int i = 1; i < sim.Units.Capacity; i++)
            if (sim.Units.Slots.IsIndexAlive(i) && sim.Units.Player[i] == p)
            {
                CellCoord c = sim.Units.Position[i].ToCell();
                int dx = c.X - s.X, dy = c.Y - s.Y;
                if (mapBack) { dx = -dx; dy = -dy; }
                items.Add($"V {Rules.Vehicles[sim.Units.TypeIndex[i]].Id} {dx},{dy}");
            }
        for (int i = 1; i < sim.Infantry.Capacity; i++)
            if (sim.Infantry.Slots.IsIndexAlive(i) && sim.Infantry.Player[i] == p)
            {
                CellCoord c = sim.Infantry.SlotCell[i];
                int dx = c.X - s.X, dy = c.Y - s.Y;
                if (mapBack) { dx = -dx; dy = -dy; }
                items.Add($"I {Rules.Infantry[sim.Infantry.TypeIndex[i]].Id} {dx},{dy}");
            }
        items.Sort(StringComparer.Ordinal);
        return items;
    }

    private (List<string> P0, List<string> P1Back) Layouts(Faction faction, ulong seed)
    {
        Simulation sim = Start(Biome.Mountains, 2, seed, faction);
        MapData m = sim.Setup.Map;
        int n = m.Width, t = n * n - 1;
        for (int i = 0; i < n * n; i++) Assert.Equal(m.Land[i], m.Land[t - i]);   // precondition: the map is an exact half turn
        Assert.Equal(n - 1 - m.StartPositions[1].X, m.StartPositions[0].X);
        Assert.Equal(n - 1 - m.StartPositions[1].Y, m.StartPositions[0].Y);
        List<string> a = Layout(sim, 0, false), b = Layout(sim, 1, true);
        output.WriteLine($"{faction} seed {seed}\n  start 0:            {string.Join("; ", a)}\n  start 1 mapped back: {string.Join("; ", b)}");
        return (a, b);
    }

    // The Authority kit is one 3x3 yard centred on the start: the image comes out right when the centre spot is free.
    [Fact]
    public void Authority_StartYard_IsTheHalfTurnImage_OnMountainsLayout2()
    {
        (List<string> a, List<string> b) = Layouts(Faction.Authority, 3UL);
        Assert.Equal(a, b);
    }

    // M13-10 (D-205 item 11): the start kit is the half-turn image on a half-turn map.
    [Theory]
    [InlineData(Faction.Directorate)]
    [InlineData(Faction.LeftBehind)]
    [InlineData(Faction.Expedition)]
    public void StartKit_IsTheHalfTurnImage_OnMountainsLayout2(Faction faction)
    {
        (List<string> a, List<string> b) = Layouts(faction, 3UL);
        Assert.Equal(a, b);
    }
}
