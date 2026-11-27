// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Data;
using Rts.Sim.Core;
using Rts.Sim.Map;
using Rts.Sim.Numerics;
using Rts.Sim.Rules;
using Rts.Sim.World;

namespace Rts.Sim.Tests.S47;

/// <summary>M16 M-28: the spatial index returns what a brute-force walk returns, in slot order, and changes no game.</summary>
public class S47SpatialTests(Xunit.Abstractions.ITestOutputHelper output)
{
    [Theory]
    [InlineData(1UL, 64, 64)]
    [InlineData(2UL, 100, 37)]
    [InlineData(3UL, 8, 8)]
    public void Query_ReturnsExactlyTheBruteForceSlotsInAscendingOrder(ulong seed, int w, int h)
    {
        var rng = new SimRandom(seed);
        var slots = new SlotArray(400);
        var pos = new LeptonCoord[400];
        for (int n = 0; n < 300; n++)
        {
            slots.TryAllocate(out EntityId id);
            // some positions fall outside the map on purpose (clamped to the edge buckets)
            pos[id.Index] = new LeptonCoord(rng.Next(w * 256 + 1024) - 512, rng.Next(h * 256 + 1024) - 512);
        }
        for (int i = 1; i < 400; i += 7) slots.Free(slots.IdOf(i));   // holes
        var idx = new SpatialIndex(400);
        idx.Build(slots, pos, w, h);
        for (int q = 0; q < 300; q++)
        {
            var from = new LeptonCoord(rng.Next(w * 256 + 1024) - 512, rng.Next(h * 256 + 1024) - 512);
            int radius = rng.Next(0, 6000);
            long r2 = (long)radius * radius;
            var expected = new List<int>();
            for (int i = 1; i < 400; i++)
                if (slots.IsIndexAlive(i) && LeptonMath.DistanceSquared(from, pos[i]) <= r2) expected.Add(i);
            var got = new List<int>();
            var span = idx.Query(from, radius);
            for (int k = 1; k < span.Length; k++) Assert.True(span[k - 1] < span[k]);
            foreach (int i in span) if (LeptonMath.DistanceSquared(from, pos[i]) <= r2) got.Add(i);
            Assert.Equal(expected, got);
        }
    }

    private static (Simulation Sim, int[] Ids) NewSim(bool enabled, ulong seed, int count = 40)
    {
        RuleSet rules = RulesLoader.Load(TestData.RulesDir);
        MapData map = MapFile.Load(Path.Combine(TestData.MapsDir, "test_32.map.toml"));
        var sim = new Simulation(new SimSetup(seed, map, 2, rules.Sim, rules.RulesHash)) { SpatialIndexEnabled = enabled };
        int tank = SimRules.IndexOf(rules.Sim.Vehicles, "test_tank", v => v.Id);
        int jeep = SimRules.IndexOf(rules.Sim.Vehicles, "test_jeep", v => v.Id);
        int rifle = SimRules.IndexOf(rules.Sim.Infantry, "test_rifleman", i => i.Id);
        var rng = new SimRandom(seed);
        for (int n = 0; n < count; n++)
        {
            byte p = (byte)(n & 1);
            var cell = new CellCoord(2 + rng.Next(8) + (p == 1 ? 18 : 0), 2 + rng.Next(8) + (p == 1 ? 18 : 0));
            if (n % 3 == 2) sim.SpawnInfantry(p, rifle, cell, rng.Next(5));
            else sim.SpawnVehicle(p, n % 3 == 0 ? tank : jeep, cell);
        }
        return (sim, Array.Empty<int>());
    }

    private static List<Command> Orders(Simulation sim)
    {
        var list = new List<Command>();
        ushort seq = 0;
        for (int i = 1; i < sim.CapacityOf(ActorKind.Vehicle); i++)
            if (sim.IsLive(ActorKind.Vehicle, i))
            {
                byte p = sim.PlayerAt(ActorKind.Vehicle, i);
                list.Add(new Command(5, p, seq++, CommandKind.AttackMove, sim.IdAt(ActorKind.Vehicle, i), EntityId.None,
                    p == 0 ? new CellCoord(24, 24) : new CellCoord(6, 6), (int)ActorKind.Vehicle));
            }
        for (int i = 1; i < sim.CapacityOf(ActorKind.Infantry); i++)
            if (sim.IsLive(ActorKind.Infantry, i))
            {
                byte p = sim.PlayerAt(ActorKind.Infantry, i);
                list.Add(new Command(5, p, seq++, CommandKind.AttackMove, sim.IdAt(ActorKind.Infantry, i), EntityId.None,
                    p == 0 ? new CellCoord(24, 24) : new CellCoord(6, 6), (int)ActorKind.Infantry));
            }
        return list;
    }

    [Fact]
    public void Skirmish_IsIdenticalWithAndWithoutTheIndex_AndAfterALoad()
    {
        var (on, _) = NewSim(true, 11UL);
        var (off, _) = NewSim(false, 11UL);
        var orders = Orders(on);
        Simulation? loaded = null;
        for (int t = 0; t < 400; t++)
        {
            var cmds = on.Tick == 5 ? orders : new List<Command>();
            on.Step(cmds); off.Step(cmds);
            Assert.Equal(off.ComputeStateHash(), on.ComputeStateHash());
            if (t == 150)
            {
                var ms = new MemoryStream(); on.Save(ms); ms.Position = 0;
                loaded = Simulation.Load(ms, on.Setup);   // a load starts with no index; the next tick builds it
            }
            if (loaded != null && t > 150)
            {
                loaded.Step(cmds);
                Assert.Equal(on.ComputeStateHash(), loaded.ComputeStateHash());
            }
        }
        Assert.NotNull(loaded);
    }

    /// <summary>Timing is logged, never asserted. Same crowded fight with the index off and on; the hashes must agree.</summary>
    [Fact]
    public void Timing_CrowdedFight_IndexOffVersusOn()
    {
        ulong hOff = 0, hOn = 0;
        foreach (bool enabled in new[] { false, true, false, true })
        {
            var (sim, _) = NewSim(enabled, 21UL, 900);
            var orders = Orders(sim);
            var sw = System.Diagnostics.Stopwatch.StartNew();
            for (int t = 0; t < 150; t++) sim.Step(sim.Tick == 5 ? orders : new List<Command>());
            sw.Stop();
            if (enabled) hOn = sim.ComputeStateHash(); else hOff = sim.ComputeStateHash();
            output.WriteLine($"index {(enabled ? "on " : "off")}: 150 ticks, {sw.ElapsedMilliseconds} ms");
        }
        Assert.Equal(hOff, hOn);
    }
}
