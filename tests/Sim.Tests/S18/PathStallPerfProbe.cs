// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using System.Diagnostics;
using System.Reflection;
using Rts.Data;
using Rts.Sim.Core;
using Rts.Sim.Map;
using Rts.Sim.Pathing;
using Xunit.Abstractions;
using static Rts.Sim.Tests.Combat.CombatFixture;

namespace Rts.Sim.Tests.S18;

/// <summary>S18 measurements for SP-01 (whole-map A* for a blocked goal, unbounded batch, then a stall) and SP-09 (cost of the state
/// hash on a Large map). Prints numbers; asserts nothing about time. Skipped unless RTS_PERF=1:
/// <c>RTS_PERF=1 dotnet test tests/Sim.Tests -c Release --filter "FullyQualifiedName~S18.PathStallPerfProbe"</c>.</summary>
[Trait("Category", "Perf")]
public sealed class PathStallPerfProbe
{
    private const int Budget = 4000;   // PathService's default expansion budget per tick
    private readonly ITestOutputHelper _out;

    public PathStallPerfProbe(ITestOutputHelper output) => _out = output;

    private static bool Enabled => Environment.GetEnvironmentVariable("RTS_PERF") == "1";

    private static int Debt(PathService p) =>
        (int)typeof(PathService).GetField("_debt", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(p)!;

    private static int Queued(PathService p) =>
        ((System.Collections.ICollection)typeof(PathService).GetField("_queue", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(p)!).Count;

    private void Line(string s) { _out.WriteLine(s); Console.WriteLine("[PERF] " + s); }

    /// <summary>20 tanks at one corner of a flat 128x128 map are ordered to Attack a building at the other corner (or, as the control, to
    /// Move next to it). A bystander tank of the other player gets a 6-cell Move 5 ticks later.</summary>
    private void Scenario(string name, bool attack, int tanks = 20)
    {
        var sim = new Simulation(new SimSetup(1, MapData.CreateFlat(128, 128), 2, Loaded.Sim, Loaded.RulesHash) { AllowDebug = true });
        EntityId hq = sim.PlaceBuilding(1, Bld("test_hq"), new CellCoord(110, 110));
        var army = new EntityId[tanks];
        for (int k = 0; k < tanks; k++) army[k] = sim.SpawnVehicle(0, Veh("test_tank"), new CellCoord(8 + 2 * (k % 5), 8 + 2 * (k / 5)));
        EntityId bystander = sim.SpawnVehicle(1, Veh("test_jeep"), new CellCoord(60, 20));
        CellCoord start = sim.Units.Position[bystander.Index].ToCell();
        for (int t = 0; t < 3; t++) sim.Step(None);

        long maxStepTicks = 0, orderStepTicks = 0;
        int stalled = 0, firstStall = -1, lastStall = -1, bystanderMoved = -1, solveTicks = 0;
        long totalExp = 0, maxExp = 0;
        int orderTick = sim.Tick;
        const int Watch = 1200;
        for (int t = 0; t < Watch; t++)
        {
            Command[] cmds = None;
            if (t == 0)
                cmds = army.Select(a => attack ? Attack(sim, 0, ActorKind.Vehicle, a, ActorKind.Building, hq) : Move(sim, 0, ActorKind.Vehicle, a, 106, 106)).ToArray();
            else if (t == 5) cmds = new[] { Move(sim, 1, ActorKind.Vehicle, bystander, start.X + 6, start.Y) };
            int debtBefore = Debt(sim.Paths);
            long a0 = Stopwatch.GetTimestamp();
            sim.Step(cmds);
            long dt = Stopwatch.GetTimestamp() - a0;
            int debtAfter = Debt(sim.Paths);
            if (t == 0) orderStepTicks = dt;
            maxStepTicks = Math.Max(maxStepTicks, dt);
            if (debtBefore >= Budget)   // budget <= 0: Process solved nothing this tick, for any player
            {
                stalled++;
                if (firstStall < 0) firstStall = t;
                lastStall = t;
            }
            else if (debtAfter > 0)     // overdrawn: the batch cost exactly this many expansions
            {
                long exp = (long)debtAfter + Budget - debtBefore;
                totalExp += exp; maxExp = Math.Max(maxExp, exp); solveTicks++;
            }
            if (bystanderMoved < 0 && t >= 5 && sim.Units.Position[bystander.Index] != LeptonCoord.CenterOf(start)) bystanderMoved = t - 5;
        }
        double ms(long ticks) => ticks * 1000.0 / Stopwatch.Frequency;
        Line($"{name}: {tanks} tanks, flat 128x128, watched {Watch} ticks after the order at tick {orderTick}");
        Line($"  order tick: {ms(orderStepTicks):F1} ms wall; slowest tick {ms(maxStepTicks):F1} ms");
        Line($"  overdrawn batches: {solveTicks}, expansions total {totalExp}, largest single tick {maxExp} (budget {Budget}/tick)");
        Line($"  ticks in which no path was solved for anyone: {stalled} (first at +{firstStall}, last at +{lastStall}) = {stalled / 20.0:F1} s at 20 ticks/s");
        Line($"  bystander ordered at +5 started moving {bystanderMoved} ticks later; queue length at the end {Queued(sim.Paths)}");
    }

    [Fact]
    public void Sp01_TwentyTanks_AttackABuilding_AcrossA128Map()
    {
        if (!Enabled) return;
        Scenario("warm-up (discarded)", attack: false, tanks: 2);
        Scenario("CONTROL Move next to the building", attack: false);
        Scenario("ATTACK the building", attack: true);
        Scenario("ATTACK the building, 1 tank", attack: true, tanks: 1);
    }

    [Fact]
    public void Sp09_StateHash_Cost()
    {
        if (!Enabled) return;
        void Time(string name, Simulation sim)
        {
            for (int k = 0; k < 20; k++) sim.ComputeStateHash();   // warm up
            const int N = 200;
            long a = Stopwatch.GetTimestamp();
            ulong h = 0;
            for (int k = 0; k < N; k++) h ^= sim.ComputeStateHash();
            double hashMs = (Stopwatch.GetTimestamp() - a) * 1000.0 / Stopwatch.Frequency / N;
            for (int k = 0; k < 64; k++) sim.Step(None);
            a = Stopwatch.GetTimestamp();
            for (int k = 0; k < 320; k++) sim.Step(None);          // 20 of these ticks hash
            double stepMs = (Stopwatch.GetTimestamp() - a) * 1000.0 / Stopwatch.Frequency / 320;
            var ms = new MemoryStream();
            sim.Save(ms);
            Line($"{name}: ComputeStateHash {hashMs:F3} ms per call (avg of {N}); idle Step avg {stepMs:F3} ms incl. 1 hash per 16 ticks " +
                 $"=> hash share {hashMs / 16 / Math.Max(stepMs, 1e-9) * 100:F0}% of an idle tick; save size {ms.Length / 1024} KB; x{h & 1}");
        }
        MapData large = MapGenerator.Generate(new MapGenParams(Biome.RustBelt, MapSize.Large, 4, 5));
        Line($"generated map: {large.Width}x{large.Height}");
        Time("Large generated, 4 players, no units", new Simulation(new SimSetup(1, large, 4, Loaded.Sim, Loaded.RulesHash)));
        Time("Large generated, 2 players, no units", new Simulation(new SimSetup(1, large, 2, Loaded.Sim, Loaded.RulesHash)));
        var flat = new Simulation(new SimSetup(1, MapData.CreateFlat(128, 128), 4, Loaded.Sim, Loaded.RulesHash));
        for (int p = 0; p < 4; p++)
            for (int k = 0; k < 150; k++) flat.SpawnVehicle((byte)p, Veh("test_tank"), new CellCoord(4 + (p % 2) * 64 + 2 * (k % 25), 4 + (p / 2) * 64 + 2 * (k / 25)));
        Time("flat 128x128, 4 players, 600 tanks", flat);
        Time("flat 64x64, 2 players, no units", new Simulation(new SimSetup(1, MapData.CreateFlat(64, 64), 2, Loaded.Sim, Loaded.RulesHash)));
    }
}
