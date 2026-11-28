// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Data;
using Rts.Sim.Core;
using Rts.Sim.Map;
using Rts.Sim.Numerics;
using Rts.Sim.Rules;
using Rts.Sim.World;

namespace Rts.Sim.Tests.S48;

/// <summary>M16 M-28b: the live-position index returns what a brute-force walk returns (same slots, same order) while actors move,
/// spawn and die, and the scans that use it change no game.</summary>
public class S48LiveIndexTests(Xunit.Abstractions.ITestOutputHelper output)
{
    // ---- the index against a brute-force walk ------------------------------------------------------

    [Theory]
    [InlineData(101UL, 64, 64)]
    [InlineData(102UL, 100, 37)]
    [InlineData(103UL, 8, 8)]
    public void Store_Query_EqualsBruteForce_OverMovesSpawnsAndDeaths(ulong seed, int w, int h)
    {
        const int cap = 600;
        var rng = new SimRandom(seed);
        var store = new UnitStore(cap);
        var live = new LiveSpatialIndex(cap);
        int queries = 0;
        LeptonCoord RandomPos() => new(rng.Next(w * 256 + 1024) - 512, rng.Next(h * 256 + 1024) - 512);   // some outside the map on purpose
        for (int n = 0; n < 250; n++) store.Add(0, 0, RandomPos(), 100);
        for (int tick = 0; tick < 500; tick++)
        {
            // moves: a few walk a little, a few teleport (no tick-start snapshot involved at all)
            for (int m = 0; m < 40; m++)
            {
                int i = 1 + rng.Next(cap - 1);
                if (!store.Slots.IsIndexAlive(i)) continue;
                store.Position[i] = rng.Next(5) == 0 ? RandomPos() : new LeptonCoord(store.Position[i].X + rng.Next(129) - 64, store.Position[i].Y + rng.Next(129) - 64);
            }
            for (int s = 0; s < rng.Next(4); s++) store.Add(0, 0, RandomPos(), 100);                                   // spawns (reuse freed slots)
            for (int d = 0; d < rng.Next(4); d++)                                                                         // deaths
            {
                int i = 1 + rng.Next(cap - 1);
                if (store.Slots.IsIndexAlive(i)) store.Remove(store.Slots.IdOf(i));
            }
            for (int q = 0; q < 3; q++)
            {
                var cell = new CellCoord(rng.Next(w + 4) - 2, rng.Next(h + 4) - 2);
                int r = rng.Next(0, 12);
                ReadOnlySpan<int> span = live.Query(store, w, h, LeptonCoord.CenterOf(cell), (r << 8) + 128);
                for (int k = 1; k < span.Length; k++) Assert.True(span[k - 1] < span[k], "candidates must be strictly ascending");
                var expected = new List<int>();
                for (int i = 1; i < cap; i++)
                    if (store.Slots.IsIndexAlive(i) && LeptonMath.ChebyshevCells(cell, store.Position[i].ToCell()) <= r) expected.Add(i);
                var got = new List<int>();
                foreach (int i in span)
                    if (store.Slots.IsIndexAlive(i) && LeptonMath.ChebyshevCells(cell, store.Position[i].ToCell()) <= r) got.Add(i);
                Assert.Equal(expected, got);
                queries++;
            }
        }
        Assert.Equal(1500, queries);
        Assert.True(live.Rebuilds > 0);
    }

    // ---- a real crowd -------------------------------------------------------------------------------

    private sealed record Kit(int Tank, int Jeep, int Artillery, int Ghost, int Sonar, int Rifle, int Sneak);

    private static (Simulation Sim, Kit Kit) NewSim(bool enabled, ulong seed, int count)
    {
        RuleSet rules = RulesLoader.Load(TestData.RulesDir);
        MapData map = MapFile.Load(Path.Combine(TestData.MapsDir, "test_64.map.toml"));
        var vehicles = new List<VehicleType>(rules.Sim.Vehicles);
        var infantry = new List<InfantryType>(rules.Sim.Infantry);
        int tank = SimRules.IndexOf(rules.Sim.Vehicles, "test_tank", v => v.Id);
        int jeep = SimRules.IndexOf(rules.Sim.Vehicles, "test_jeep", v => v.Id);
        int art = SimRules.IndexOf(rules.Sim.Vehicles, "test_artillery_truck", v => v.Id);
        int rifle = SimRules.IndexOf(rules.Sim.Infantry, "test_rifleman", i => i.Id);
        vehicles.Add(vehicles[jeep] with { Id = "ghost", Stealth = true });
        vehicles.Add(vehicles[jeep] with { Id = "sonar_boat", SonarRadius = 5 });
        infantry.Add(infantry[rifle] with { Id = "sneak", Stealth = true });
        var kit = new Kit(tank, jeep, art, vehicles.Count - 2, vehicles.Count - 1, rifle, infantry.Count - 1);
        SimRules sr = rules.Sim with { Vehicles = vehicles, Infantry = infantry };
        var sim = new Simulation(new SimSetup(seed, map, 2, sr, rules.RulesHash) { AllowDebug = true }) { LiveIndexEnabled = enabled };
        var rng = new SimRandom(seed);
        for (int n = 0; n < count; n++)
        {
            byte p = (byte)(n & 1);
            var cell = new CellCoord(2 + rng.Next(14) + (p == 1 ? 22 : 0), 2 + rng.Next(14) + (p == 1 ? 22 : 0));
            switch (n % 7)
            {
                case 0: sim.SpawnVehicle(p, kit.Tank, cell); break;
                case 1: sim.SpawnVehicle(p, kit.Artillery, cell); break;
                case 2: sim.SpawnVehicle(p, kit.Jeep, cell); break;
                case 3: sim.SpawnVehicle(p, kit.Ghost, cell); break;
                case 4: sim.SpawnVehicle(p, kit.Sonar, cell); break;
                case 5: sim.SpawnInfantry(p, kit.Rifle, cell, rng.Next(5)); break;
                default: sim.SpawnInfantry(p, kit.Sneak, cell, rng.Next(5)); break;
            }
        }
        return (sim, kit);
    }

    private static List<Command> Orders(Simulation sim, ref ushort seq, SimRandom rng, bool all, int execTick = -1)
    {
        var list = new List<Command>();
        foreach (ActorKind kind in new[] { ActorKind.Vehicle, ActorKind.Infantry })
            for (int i = 1; i < sim.CapacityOf(kind); i++)
                if (sim.IsLive(kind, i) && (all || rng.Next(6) == 0))
                {
                    byte p = sim.PlayerAt(kind, i);
                    var dest = all ? (p == 0 ? new CellCoord(30, 30) : new CellCoord(10, 10)) : new CellCoord(4 + rng.Next(56), 4 + rng.Next(56));
                    list.Add(new Command(execTick < 0 ? sim.Tick : execTick, p, seq++, rng.Next(3) == 0 ? CommandKind.Move : CommandKind.AttackMove, sim.IdAt(kind, i), EntityId.None, dest, (int)kind));
                }
        return list;
    }

    [Fact]
    public void Sim_Candidates_EqualBruteForce_Over500Ticks_WithMovesSpawnsAndDeaths()
    {
        var (sim, kit) = NewSim(true, 7UL, 140);
        var rng = new SimRandom(77);
        ushort seq = 0;
        int checks = 0;
        for (int t = 0; t < 500; t++)
        {
            var cmds = new List<Command>();
            if (t == 3) cmds.AddRange(Orders(sim, ref seq, rng, true));
            else if (t % 40 == 0) cmds.AddRange(Orders(sim, ref seq, rng, false));
            if (t % 7 == 0)   // spawns, mixed kinds and owners (the debug command spawns at once, inside the tick)
                cmds.Add(new Command(sim.Tick, (byte)rng.Next(2), seq++, CommandKind.Debug, EntityId.None, default, new CellCoord(2 + rng.Next(60), 2 + rng.Next(60)),
                    rng.Next(2) == 0 ? (int)DebugOp.SpawnVehicle : (int)DebugOp.SpawnInfantry, rng.Next(2) == 0 ? kit.Tank : kit.Artillery));
            if (t % 5 == 0)   // deaths
            {
                ActorKind kind = rng.Next(2) == 0 ? ActorKind.Vehicle : ActorKind.Infantry;
                int i = 1 + rng.Next(sim.CapacityOf(kind) - 1);
                if (sim.IsLive(kind, i)) cmds.Add(new Command(sim.Tick, 0, seq++, CommandKind.Debug, sim.IdAt(kind, i), default, default, (int)DebugOp.Kill, (int)kind));
            }
            sim.Step(cmds);
            for (int q = 0; q < 6; q++)
            {
                ActorKind kind = q % 2 == 0 ? ActorKind.Vehicle : ActorKind.Infantry;
                var cell = new CellCoord(rng.Next(68) - 2, rng.Next(68) - 2);
                int r = rng.Next(0, 10);
                Assert.True(sim.TryLiveCandidates(kind, cell, r, out ReadOnlySpan<int> cand));
                for (int k = 1; k < cand.Length; k++) Assert.True(cand[k - 1] < cand[k]);
                var expected = new List<int>();
                for (int i = 1; i < sim.CapacityOf(kind); i++)
                    if (sim.IsLive(kind, i) && LeptonMath.ChebyshevCells(cell, sim.PositionAt(kind, i).ToCell()) <= r) expected.Add(i);
                var got = new List<int>();
                foreach (int i in cand)
                    if (sim.IsLive(kind, i) && LeptonMath.ChebyshevCells(cell, sim.PositionAt(kind, i).ToCell()) <= r) got.Add(i);
                Assert.Equal(expected, got);
                checks++;
            }
        }
        Assert.Equal(3000, checks);
    }

    [Fact]
    public void Scans_AreIdenticalWithAndWithoutTheIndex_HashesAndYesNoAnswers_AndAfterALoad()
    {
        var (on, _) = NewSim(true, 11UL, 200);
        var (off, _) = NewSim(false, 11UL, 200);
        var ordRng = new SimRandom(5);
        ushort seq = 0;
        var orders = Orders(on, ref seq, ordRng, true, 3);
        var probe = new SimRandom(99);
        Simulation? loaded = null;
        for (int t = 0; t < 400; t++)
        {
            var cmds = t == 3 ? orders : new List<Command>();
            on.Step(cmds); off.Step(cmds);
            Assert.Equal(off.ComputeStateHash(), on.ComputeStateHash());
            for (int q = 0; q < 8; q++)   // the yes/no scans, asked of the same sim both ways
            {
                byte viewer = (byte)probe.Next(2);
                var cell = new CellCoord(probe.Next(64), probe.Next(64));
                on.LiveIndexEnabled = true;
                bool a1 = on.IsStealthRevealed(viewer, cell), a2 = on.CanSeeThroughBlackout(viewer, cell), a3 = on.IsSonarRevealed(viewer, cell);
                on.LiveIndexEnabled = false;
                bool b1 = on.IsStealthRevealed(viewer, cell), b2 = on.CanSeeThroughBlackout(viewer, cell), b3 = on.IsSonarRevealed(viewer, cell);
                on.LiveIndexEnabled = true;
                Assert.Equal((b1, b2, b3), (a1, a2, a3));
            }
            if (t == 150)
            {
                var ms = new MemoryStream(); on.Save(ms); ms.Position = 0;
                loaded = Simulation.Load(ms, on.Setup);   // a load starts with no index; the first query builds it
            }
            if (loaded != null && t > 150)
            {
                loaded.Step(cmds);
                Assert.Equal(on.ComputeStateHash(), loaded.ComputeStateHash());
            }
        }
        Assert.NotNull(loaded);
        Assert.True(on.LiveIndexRebuilds > 0);
    }

    private static long CpuMs() => (long)System.Diagnostics.Process.GetCurrentProcess().TotalProcessorTime.TotalMilliseconds;

    /// <summary>Timing is logged, never asserted (CPU time, best of three, because the box is shared). Same crowded fight with the live index
    /// off and on; the hashes must agree.</summary>
    [Fact]
    public void Timing_CrowdedFight_LiveIndexOffVersusOn()
    {
        ulong hOff = 0, hOn = 0;
        long bestOff = long.MaxValue, bestOn = long.MaxValue;
        for (int rep = 0; rep < 3; rep++)
            foreach (bool enabled in new[] { false, true })
            {
                var (sim, _) = NewSim(enabled, 21UL, 900);
                var rng = new SimRandom(1);
                ushort seq = 0;
                var orders = Orders(sim, ref seq, rng, true, 3);
                long c0 = CpuMs();
                for (int t = 0; t < 150; t++) sim.Step(t == 3 ? orders : new List<Command>());
                long ms = CpuMs() - c0;
                if (enabled) { hOn = sim.ComputeStateHash(); bestOn = Math.Min(bestOn, ms); } else { hOff = sim.ComputeStateHash(); bestOff = Math.Min(bestOff, ms); }
                output.WriteLine($"fight, live index {(enabled ? "on " : "off")}: 150 ticks, {ms} ms CPU, rebuilds {sim.LiveIndexRebuilds}");
            }
        output.WriteLine($"fight best of 3: off {bestOff} ms ({150000.0 / Math.Max(1, bestOff):F0} ticks/s), on {bestOn} ms ({150000.0 / Math.Max(1, bestOn):F0} ticks/s)");
        Assert.Equal(hOff, hOn);
    }

    /// <summary>Timing per scan (logged, not asserted): splash detonations, and the stealth / blackout / sonar probes, in the middle of a crowded fight.
    /// Also the cost of one aura recompute, which stays on the full walk.</summary>
    [Fact]
    public void Timing_PerScan_LiveIndexOffVersusOn()
    {
        var (sim, _) = NewSim(true, 21UL, 900);
        var rng = new SimRandom(1);
        ushort seq = 0;
        var orders = Orders(sim, ref seq, rng, true, 3);
        for (int t = 0; t < 70; t++) sim.Step(t == 3 ? orders : new List<Command>());
        int he = 0;
        for (int w = 0; w < sim.Setup.Rules.Warheads.Count; w++) if (sim.Setup.Rules.Warheads[w].Splash >= 1) { he = w; break; }
        var cells = new List<CellCoord>();
        var pr = new SimRandom(3);
        for (int n = 0; n < 400; n++) cells.Add(new CellCoord(2 + pr.Next(60), 2 + pr.Next(60)));
        string[] names = { "splash x400", "stealth reveal x1600", "blackout sight x1600", "sonar x1600" };
        var best = new long[2, 4];
        for (int a = 0; a < 2; a++) for (int s = 0; s < 4; s++) best[a, s] = long.MaxValue;
        for (int rep = 0; rep < 3; rep++)
            for (int a = 0; a < 2; a++)
            {
                sim.LiveIndexEnabled = a == 1;
                long c0 = CpuMs();
                foreach (var c in cells) sim.Detonate(0, 40, he, LeptonCoord.CenterOf(c), default, false);
                best[a, 0] = Math.Min(best[a, 0], CpuMs() - c0);
                for (int s = 1; s < 4; s++)
                {
                    c0 = CpuMs();
                    int hits = 0;
                    for (int r = 0; r < 4; r++)
                        foreach (var c in cells)
                            hits += s == 1 ? (sim.IsStealthRevealed((byte)(r & 1), c) ? 1 : 0)
                                  : s == 2 ? (sim.CanSeeThroughBlackout((byte)(r & 1), c) ? 1 : 0)
                                  : (sim.IsSonarRevealed((byte)(r & 1), c) ? 1 : 0);
                    best[a, s] = Math.Min(best[a, s], CpuMs() - c0);
                }
            }
        for (int s = 0; s < 4; s++) output.WriteLine($"{names[s]}: off {best[0, s]} ms, on {best[1, s]} ms");
        var aura = typeof(Simulation).GetMethod("RunAuras", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
        while (sim.Tick % 10 != 0) sim.Step(Array.Empty<Command>());
        long a0 = CpuMs();
        for (int n = 0; n < 200; n++) aura.Invoke(sim, null);
        output.WriteLine($"aura recompute (full walk): {(CpuMs() - a0) / 200.0:F3} ms per recompute (runs once per 10 ticks)");
    }
}
