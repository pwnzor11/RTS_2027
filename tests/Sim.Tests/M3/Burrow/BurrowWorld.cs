// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Sim.Core;
using Rts.Sim.Map;
using Rts.Sim.Rules;
using Rts.Sim.Tests.Determinism;
using Rts.Sim.Tests.Movement;
using Rts.Sim.Tunnels;

namespace Rts.Sim.Tests.M3.Burrow;

/// <summary>M3-03 fixtures: Directorate-style burrowers, a Bore, a land-train, a Hatch and a Bore Head, all built in code.</summary>
internal static class BurrowWorld
{
    public static readonly Command[] None = Array.Empty<Command>();

    // building type indexes (sorted by id)
    public const int Block = 0, BoreHead = 1, Hatch = 2, House = 3, Shaft = 4;
    // vehicle type indexes (sorted by id)
    public const int Bore = 0, Burrower = 1, Enemy = 2, Gunner = 3, Train = 4;

    public static SimRules Rules()
    {
        VehicleType tank = ScriptedGame.Vehicle("x", 40, 100);
        var vehicles = new VehicleType[]
        {
            tank with { Id = "bore", Locomotion = Locomotion.Burrow, MaxHealth = 300, DeploysInto = Hatch, Faction = Faction.Directorate },
            tank with { Id = "burrower", Locomotion = Locomotion.Burrow, Faction = Faction.Directorate },
            tank with { Id = "enemy", MaxHealth = 500 },
            tank with { Id = "gunner", Locomotion = Locomotion.Burrow, PrimaryWeapon = 0, Faction = Faction.Directorate },
            tank with { Id = "train", MaxHealth = 400, DeploysInto = Shaft, Faction = Faction.Directorate },
        };
        BuildingType Bld(string id, int w, int h, int hp, int cost = 0) =>
            new(id, w, h, hp, ArmorClass.Structure, 5, -1, cost, 0, Array.Empty<string>(), Array.Empty<string>());
        var buildings = new BuildingType[]
        {
            Bld("block", 1, 1, 100),
            Bld("borehead", 1, 1, 600, 400) with { Entrance = TunnelSize.M, TicksPerPoint = 13, Faction = Faction.Directorate },
            Bld("hatch", 1, 1, 200, 100) with { Entrance = TunnelSize.S, Faction = Faction.Directorate, MorphsInto = BoreHead },
            Bld("house", 2, 2, 300),
            Bld("shaft", 3, 3, 1000, 800) with { Entrance = TunnelSize.M, Faction = Faction.Directorate },
        };
        var weapons = new[] { new WeaponType("gun", 10, 5 * 256, 0, 10, 0, 0) };
        var warheads = new[] { new WarheadType("bullet", new[] { 100, 100, 100, 100, 100 }, 0, false) };
        return new SimRules(vehicles, new[] { ScriptedGame.Infantry("rifle", 20, 30) }, buildings, weapons, warheads,
            MoveWorld.Terrain(), Array.Empty<EffectDef>());
    }

    /// <summary>Clear map with an optional painter (see <see cref="MoveWorld.Grid"/>).</summary>
    public static SimSetup Setup(int w = 48, int h = 32, Action<LandType[], int>? paint = null, ulong seed = 1) =>
        new(seed, MoveWorld.Grid(w, h, paint), 2, Rules(), 1) { AllowDebug = true };

    public static Simulation NewSim(Action<LandType[], int>? paint = null) => new(Setup(paint: paint));

    public static Command Cmd(Simulation sim, CommandKind kind, EntityId subject, CellCoord cell = default, int arg = 0, byte player = 0) =>
        new(sim.Tick, player, 0, kind, subject, default, cell, arg);

    public static Command MoveTo(Simulation sim, EntityId unit, int x, int y, byte player = 0) =>
        Cmd(sim, CommandKind.Move, unit, new CellCoord(x, y), (int)ActorKind.Vehicle, player);

    public static Command DeployAt(Simulation sim, EntityId unit, int x, int y, byte player = 0) =>
        Cmd(sim, CommandKind.Deploy, unit, new CellCoord(x, y), (int)ActorKind.Vehicle, player);

    public static void Step(Simulation sim, params Command[] cmds) => sim.Step(cmds);

    public static void Run(Simulation sim, int ticks) { for (int t = 0; t < ticks; t++) sim.Step(None); }

    /// <summary>Steps (no commands) until <paramref name="done"/> or <paramref name="max"/> ticks; fails the test on timeout.</summary>
    public static int RunUntil(Simulation sim, Func<bool> done, int max = 2000)
    {
        for (int t = 0; t < max; t++)
        {
            if (done()) return t;
            sim.Step(None);
        }
        Assert.Fail($"condition not reached within {max} ticks (tick {sim.Tick})");
        return max;
    }

    public static ActorLayer Layer(Simulation sim, EntityId id) => sim.Tunnels.VehicleLayer[id.Index];

    public static CellCoord CellOf(Simulation sim, EntityId id) => sim.Units.Position[id.Index].ToCell();

    public static int Cheb(CellCoord a, CellCoord b) => Math.Max(Math.Abs(a.X - b.X), Math.Abs(a.Y - b.Y));

    /// <summary>Smallest Chebyshev distance from the cell to any building footprint cell on the map.</summary>
    public static int DistanceToStructure(Simulation sim, CellCoord c)
    {
        int best = int.MaxValue;
        for (int y = 0; y < sim.Map.Height; y++)
            for (int x = 0; x < sim.Map.Width; x++)
                if (!sim.Map.BuildingAt(new CellCoord(x, y)).IsNone) best = Math.Min(best, Cheb(c, new CellCoord(x, y)));
        return best;
    }

    public static bool IsSoft(Simulation sim, CellCoord c) => sim.Setup.Rules.Terrain.Soft(sim.Map.LandAt(c));

    public static Telegraph? UnitTelegraph(Simulation sim)
    {
        foreach (Telegraph t in sim.Tunnels.Telegraphs) if (t.Kind == TelegraphKind.Unit) return t;
        return null;
    }

    public static Telegraph? EntranceTelegraph(Simulation sim)
    {
        foreach (Telegraph t in sim.Tunnels.Telegraphs) if (t.Kind == TelegraphKind.Entrance) return t;
        return null;
    }
}
