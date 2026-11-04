// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Data;
using Rts.Sim;
using Rts.Sim.Core;
using Rts.Sim.Map;

namespace Llm.Tests;

/// <summary>Real rules plus small simulation builders for the summary tests (same pattern as tests/AI.Tests/TestWorld).</summary>
internal static class SummaryRig
{
    public static readonly string RulesDir = RulesLocator.FindRulesDir(AppContext.BaseDirectory)!;
    public static readonly RuleSet Rules = RulesLoader.Load(RulesDir);
    private static ushort _seq = 50000;

    public static int Vehicle(string id) => Array.FindIndex(Rules.Sim.Vehicles.ToArray(), v => v.Id == id);
    public static int Infantry(string id) => Array.FindIndex(Rules.Sim.Infantry.ToArray(), v => v.Id == id);
    public static int Building(string id) => Array.FindIndex(Rules.Sim.Buildings.ToArray(), v => v.Id == id);

    public static Simulation Sim(int players = 2, int size = 64) =>
        new(new SimSetup(1, MapData.CreateFlat(size, size), players, Rules.Sim, Rules.RulesHash) { AllowDebug = true });

    public static EntityId LateInfantry(Simulation sim, byte player, int type, CellCoord cell)
    {
        int slot = 1;
        while (sim.Infantry.Slots.IsIndexAlive(slot)) slot++;
        sim.Step(new[] { new Command(sim.Tick, player, _seq++, CommandKind.Debug, default, default, cell, (int)DebugOp.SpawnInfantry, type) });
        return sim.Infantry.Slots.IdOf(slot);
    }

    public static EntityId LateVehicle(Simulation sim, byte player, int type, CellCoord cell)
    {
        int slot = 1;
        while (sim.Units.Slots.IsIndexAlive(slot)) slot++;
        sim.Step(new[] { new Command(sim.Tick, player, _seq++, CommandKind.Debug, default, default, cell, (int)DebugOp.SpawnVehicle, type) });
        return sim.Units.Slots.IdOf(slot);
    }

    public static void Kill(Simulation sim, byte player, EntityId id, ActorKind kind) =>
        sim.Step(new[] { new Command(sim.Tick, player, _seq++, CommandKind.Debug, id, default, default, (int)DebugOp.Kill, (int)kind) });

    public static void Run(Simulation sim, int ticks)
    {
        for (int i = 0; i < ticks; i++) sim.Step(Array.Empty<Command>());
    }

    /// <summary>Directory tests/Llm.Tests/Golden in the source tree (found by walking up from the test binary).</summary>
    public static string GoldenDir()
    {
        string? d = AppContext.BaseDirectory;
        while (d != null && !Directory.Exists(Path.Combine(d, "tests", "Llm.Tests", "Golden"))) d = Path.GetDirectoryName(d);
        return Path.Combine(d ?? throw new DirectoryNotFoundException("tests/Llm.Tests/Golden"), "tests", "Llm.Tests", "Golden");
    }
}
