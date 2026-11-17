// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Sim.Core;
using Rts.Sim.Map;
using Rts.Sim.Missions;
using Rts.Sim.Rules;
using Rts.Sim.Tests.Movement;
using static Rts.Sim.Tests.M12_16.PropFixture;

namespace Rts.Sim.Tests.M12_16;

/// <summary>M12-16 (D-264 (3)): the prop state is hashed and saved, a crush and a splash change it, and the whole thing is deterministic.</summary>
public sealed class PropDeterminismTests
{
    private const int W = 36, H = 15;

    private static SimSetup Setup(ulong seed)
    {
        var ovl = new OverlayKind[W * H];
        for (int y = 0; y < H; y++) { ovl[y * W + 14] = OverlayKind.Tree; ovl[y * W + 22] = y % 2 == 0 ? OverlayKind.Snag : OverlayKind.Wreck; }
        ovl[7 * W + 30] = OverlayKind.Block;
        var map = new MapData("props", "rust_belt", W, H, new ushort[W * H], new LandType[W * H], new byte[W * H], ovl, new byte[W * H],
            new[] { new CellCoord(1, 1), new CellCoord(W - 2, H - 2) });
        return new SimSetup(seed, map, 2, PropFixture.Rules(), 1) { AllowDebug = true };
    }

    private static (Simulation Sim, EntityId[] Tanks) NewGame(ulong seed)
    {
        var sim = new Simulation(Setup(seed));
        var tanks = new[]
        {
            sim.SpawnVehicle(0, Tank, new CellCoord(3, 3)), sim.SpawnVehicle(0, Tank, new CellCoord(3, 7)),
            sim.SpawnVehicle(0, Hauler, new CellCoord(3, 11)), sim.SpawnVehicle(1, Jeep, new CellCoord(33, 5)),
        };
        return (sim, tanks);
    }

    private static Command[] Orders(Simulation sim, EntityId[] t) => new[]
    {
        MoveWorld.Move(t[0], ActorKind.Vehicle, 30, 3, sim.Tick, 0, 0), MoveWorld.Move(t[1], ActorKind.Vehicle, 30, 9, sim.Tick, 0, 1),
        MoveWorld.Move(t[2], ActorKind.Vehicle, 30, 12, sim.Tick, 0, 2), MoveWorld.Move(t[3], ActorKind.Vehicle, 4, 5, sim.Tick, 1, 3),
    };

    /// <summary>One run: orders at tick 0, two shells at fixed ticks, then 600 ticks; returns (tick, hash) every 25 ticks.</summary>
    private static List<(int Tick, ulong Hash)> Run(Simulation sim, EntityId[] tanks, int from = 0, int to = 600)
    {
        var log = new List<(int, ulong)>();
        for (int t = from; t < to; t++)
        {
            sim.Step(t == 0 ? Orders(sim, tanks) : Array.Empty<Command>());
            if (t == 200) sim.Detonate(0, 60, SplashWarhead, LeptonCoord.CenterOf(new CellCoord(22, 4)), default, false);
            if (t == 260) sim.Detonate(0, 60, SplashWarhead, LeptonCoord.CenterOf(new CellCoord(22, 5)), default, false);
            if (t % 25 == 0) log.Add((sim.Tick, sim.ComputeStateHash()));
        }
        return log;
    }

    [Fact]
    public void SameSeedTwice_GivesTheSameHashes()
    {
        var (a, ta) = NewGame(7);
        var (b, tb) = NewGame(7);
        Assert.Equal(Run(a, ta), Run(b, tb));
        Assert.Equal(a.Props.Health, b.Props.Health);
    }

    [Fact]
    public void ACrushAndASplash_ChangeTheStateAndTheOverlay_AndEverythingStillAgrees()
    {
        var (sim, tanks) = NewGame(3);
        ulong start = sim.ComputeStateHash();
        int propsAtStart = sim.Props.Count;
        var subs0 = sim.ComputeSubHashes().ToList();
        Run(sim, tanks);
        Assert.True(sim.Props.Count < propsAtStart);                       // something was crushed or shelled
        Assert.NotEqual(start, sim.ComputeStateHash());
        var subs1 = sim.ComputeSubHashes().ToList();
        Assert.NotEqual(subs0.Single(s => s.Part == "props").Hash, subs1.Single(s => s.Part == "props").Hash);
        // the tank on the second row crossed both lines: at least one tree on column 14 is gone
        Assert.Contains(Enumerable.Range(0, H), y => sim.Map.OverlayAt(new CellCoord(14, y)) == OverlayKind.None);
        Assert.Equal(OverlayKind.Block, sim.Map.OverlayAt(new CellCoord(30, 7)));
        // the health array and the overlay agree: health exactly where a Tree, Snag or Wreck still stands
        for (int y = 0; y < H; y++)
            for (int x = 0; x < W; x++)
            {
                var c = new CellCoord(x, y);
                Assert.Equal(OverlayKinds.IsDestructible(sim.Map.OverlayAt(c)), sim.Props.HealthAt(c) > 0);
            }
    }

    [Fact]
    public void SaveAndLoad_MidGame_ContinuesIdentically()
    {
        var (a, ta) = NewGame(11);
        Run(a, ta, 0, 230);                                                // after a crush, between the two shells
        Assert.True(a.Props.Count < 18 + 15);
        var ms = new MemoryStream();
        a.Save(ms);
        ms.Position = 0;
        Simulation b = Simulation.Load(ms, a.Setup);
        Assert.Equal(a.ComputeStateHash(), b.ComputeStateHash());
        Assert.Equal(a.Props.Health, b.Props.Health);
        for (int t = 230; t < 600; t++)
        {
            a.Step(Array.Empty<Command>()); b.Step(Array.Empty<Command>());
            if (t == 260)
            {
                a.Detonate(0, 60, SplashWarhead, LeptonCoord.CenterOf(new CellCoord(22, 5)), default, false);
                b.Detonate(0, 60, SplashWarhead, LeptonCoord.CenterOf(new CellCoord(22, 5)), default, false);
            }
            if (t % 25 == 0) Assert.Equal(a.ComputeStateHash(), b.ComputeStateHash());
        }
        Assert.Equal(a.ComputeStateHash(), b.ComputeStateHash());
    }
}
