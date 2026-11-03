// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Sim.Core;
using Rts.Sim.Map;
using Rts.Sim.Missions;
using Rts.Sim.Rules;
using static Rts.Sim.Tests.Combat.CombatFixture;

namespace Rts.Sim.Tests.Economy;

/// <summary>Shared helpers for economy tests: the real test rules on a 32x32 map with a hand-painted resource field.</summary>
internal static class EconomyFixture
{
    public static readonly CellCoord Refinery = new(4, 4);          // 3x3, dock cell (5,7)
    public static readonly CellCoord Dock = new(5, 7);
    public static readonly CellCoord Field = new(14, 8);            // top-left of the painted 3x3 field

    public static int Harvester => Veh("test_harvester");

    /// <summary>Paints a 3x3 field of <paramref name="kind"/> at <see cref="Field"/> with the given density.</summary>
    public static MapData PaintMap(OverlayKind kind, byte density, Action<OverlayKind[], byte[], int>? extra = null)
    {
        const int w = 32, n = w * w;
        var overlay = new OverlayKind[n];
        var dens = new byte[n];
        for (int y = 0; y < 3; y++)
            for (int x = 0; x < 3; x++) { overlay[(Field.Y + y) * w + Field.X + x] = kind; dens[(Field.Y + y) * w + Field.X + x] = density; }
        extra?.Invoke(overlay, dens, w);
        return new MapData("eco", "rust_belt", w, w, new ushort[n], new LandType[n], new byte[n], overlay, dens,
            new[] { new CellCoord(2, 2), new CellCoord(29, 29) });
    }

    public static Simulation NewSim(MapData map, SimRules? rules = null, ulong seed = 1)
    {
        SimRules r = rules ?? Loaded.Sim;
        return new Simulation(new SimSetup(seed, map, 2, r, Loaded.RulesHash) { AllowDebug = true });
    }

    /// <summary>A sim with player 0's refinery at <see cref="Refinery"/> and one harvester at (8,8).</summary>
    public static (Simulation Sim, EntityId Harvester) Basic(OverlayKind kind = OverlayKind.Ashfall, byte density = 12, bool silo = false,
        SimRules? rules = null)
    {
        var sim = NewSim(PaintMap(kind, density), rules);
        sim.PlaceBuilding(0, Bld("test_refinery"), Refinery);
        if (silo) sim.PlaceBuilding(0, Bld("test_silo"), new CellCoord(8, 2));
        return (sim, sim.SpawnVehicle(0, Harvester, new CellCoord(8, 8)));
    }

    public static int TotalDensity(Simulation sim)
    {
        int sum = 0;
        for (int y = 0; y < sim.Map.Height; y++)
            for (int x = 0; x < sim.Map.Width; x++) sum += sim.Map.DensityAt(new CellCoord(x, y));
        return sum;
    }

    public static void Run(Simulation sim, int ticks) => Combat.CombatFixture.Run(sim, ticks);

    /// <summary>Steps until the predicate holds (checked after each tick); fails after <paramref name="max"/> ticks.</summary>
    public static int RunUntil(Simulation sim, Func<bool> done, int max = 3000)
    {
        for (int t = 0; t < max; t++)
        {
            sim.Step(None);
            if (done()) return t + 1;
        }
        Assert.Fail($"condition not reached within {max} ticks (tick {sim.Tick})");
        return max;
    }

    public static Command Debug(Simulation sim, byte player, DebugOp op, EntityId subject = default, int arg2 = 0) =>
        new(sim.Tick, player, 9000, CommandKind.Debug, subject, default, default, (int)op, arg2);

    public static MissionKind MissionOf(Simulation sim, EntityId id) => sim.Units.Mission[id.Index];
}
