// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Sim.Core;
using Rts.Sim.Map;
using Rts.Sim.Rules;
using Rts.Sim.Tests.Determinism;
using Xunit.Abstractions;

namespace Rts.Sim.Tests.S18;

/// <summary>
/// S18 audit AI-01: supply tenders spawn on the map-edge sea list in cell-index order (top row first), starting at
/// <c>NextTenderId mod count</c>, a game-wide counter. On a point-symmetric map with one ring of sea the player whose dock is near the
/// north-west corner gets its tenders a few cells away; the mirrored player's tenders cross the whole map.
/// </summary>
public sealed class ShipmentsSpawnAuditTests(ITestOutputHelper output)
{
    private const int N = 40, Sea = 4;   // 40 x 40 map, a 4-cell ring of sea round the edge (one naval region)
    private const int Dock = 0, Tender = 0;

    private static TerrainTable Terrain()
    {
        var speed = new int[RuleCounts.LandTypes * RuleCounts.Locomotions];
        Array.Fill(speed, 100);
        for (int l = 0; l < RuleCounts.Locomotions; l++) speed[(int)LandType.Water * RuleCounts.Locomotions + l] = 0;
        for (int land = 0; land < RuleCounts.LandTypes; land++) speed[land * RuleCounts.Locomotions + (int)Locomotion.Naval] = 0;
        speed[(int)LandType.Water * RuleCounts.Locomotions + (int)Locomotion.Naval] = 100;
        var buildable = new bool[RuleCounts.LandTypes];
        buildable[(int)LandType.Clear] = true;
        return new TerrainTable(speed, buildable);
    }

    private static Simulation NewSim(CellCoord dock0, CellCoord dock1)
    {
        var land = new LandType[N * N];
        for (int y = 0; y < N; y++)
            for (int x = 0; x < N; x++)
                if (x < Sea || y < Sea || x >= N - Sea || y >= N - Sea) land[y * N + x] = LandType.Water;
        var map = new MapData("ring", "rust_belt", N, N, new ushort[N * N], land, new byte[N * N], new OverlayKind[N * N], new byte[N * N],
            new[] { new CellCoord(10, 10), new CellCoord(N - 1 - 10, N - 1 - 10) });
        var tender = new VehicleType("tender", Locomotion.Naval, 128, 8, 100, ArmorClass.Heavy, 5, -1, false, 0, false, 0, Array.Empty<string>());
        BuildingType[] buildings = { ScriptedGame.Building("dock", 2, 2, 300) with { ShipmentDock = true } };
        var rules = new SimRules(new[] { tender }, Array.Empty<InfantryType>(), buildings, Array.Empty<WeaponType>(),
            Array.Empty<WarheadType>(), Terrain(), Array.Empty<EffectDef>())
        {
            Shipments = new ShipmentRules { Enabled = true, TenderType = "tender", IntervalTicks = 100, Value = 1000, DecayPermille = 0, FloorPermille = 1000, CharterTicks = 0, TendersMax = 1 },
        };
        var sim = new Simulation(new SimSetup(1, map, 2, rules, 0xA1UL));
        Assert.False(sim.PlaceBuilding(0, Dock, dock0).IsNone);
        Assert.False(sim.PlaceBuilding(1, Dock, dock1).IsNone);
        return sim;
    }

    /// <summary>Runs 1 200 ticks; per player the tick of the first paid shipment and the spawn cell of its first tender.</summary>
    private (int[] FirstPaid, CellCoord[] FirstSpawn) Run(Simulation sim)
    {
        var paid = new[] { -1, -1 };
        var spawn = new CellCoord?[2];
        for (int t = 0; t < 1200; t++)
        {
            sim.Step(Array.Empty<Command>());
            foreach (var tn in sim.Shipments.Tenders) spawn[tn.Player] ??= new CellCoord(tn.CellX, tn.CellY);
            for (byte p = 0; p < 2; p++) if (paid[p] < 0 && sim.StatsOf(p).ShipmentIncome > 0) paid[p] = sim.Tick;
        }
        output.WriteLine($"first paid tick p0 {paid[0]} p1 {paid[1]}; first spawn p0 {spawn[0]} p1 {spawn[1]}; paid p0 {sim.StatsOf(0).ShipmentIncome} p1 {sim.StatsOf(1).ShipmentIncome}");
        return (paid, new[] { spawn[0]!.Value, spawn[1]!.Value });
    }

    // Docks are exact half-turn images: (4,4) and (40-4-2, 40-4-2) = (34,34); both touch the ring of sea.
    private static readonly CellCoord NorthWest = new(Sea, Sea), SouthEast = new(N - Sea - 2, N - Sea - 2);

    [Fact]
    public void SwappedDocks_AreAlsoSuppliedAtTheSameTick_TheAdvantageNoLongerFollowsTheCorner()
    {
        (int[] paid, _) = Run(NewSim(SouthEast, NorthWest));
        AssertSameTick(paid);
    }

    // The two tenders sail mirrored routes of equal length, but one heads down and one up the map and the sim reads a position as the cell
    // it floors to, so on this fixture's whole-cell tie the arrival can differ by one tick. Anything under a few ticks is "the same tick";
    // before M13-03 the gap was a full shipment interval (136 ticks).
    private static void AssertSameTick(int[] paid) => Assert.True(Math.Abs(paid[0] - paid[1]) <= 1, $"paid at {paid[0]} v {paid[1]}");

    [Fact]
    public void MirroredDocks_TendersSpawnOnTheEdgeNearestTheirDock()
    {
        (_, CellCoord[] spawn) = Run(NewSim(NorthWest, SouthEast));
        Assert.True(spawn[0].X < N / 2 && spawn[0].Y < N / 2, $"north-west dock, spawn {spawn[0]}");
        Assert.True(spawn[1].X >= N / 2 && spawn[1].Y >= N / 2, $"south-east dock, spawn {spawn[1]}");
    }

    [Fact]
    public void MirroredDocks_AreSuppliedAtTheSameTick()
    {
        (int[] paid, _) = Run(NewSim(NorthWest, SouthEast));
        AssertSameTick(paid);
    }
}
