// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Data;
using Rts.Scripting;
using Rts.Sim;
using Rts.Sim.Core;

namespace Rts.Scripting.Tests;

/// <summary>Shared helpers: where the repo data is, how to build a tiny mission around a Lua snippet, how to run ticks.</summary>
internal static class TestData
{
    public static readonly IReadOnlyList<Command> NoCommands = Array.Empty<Command>();

    public static string DataDir
    {
        get
        {
            string rules = RulesLocator.FindRulesDir(AppContext.BaseDirectory) ?? throw new InvalidOperationException("data/rules not found");
            return Path.GetFullPath(Path.Combine(rules, ".."));
        }
    }

    public static void Run(Simulation sim, int ticks)
    {
        for (int i = 0; i < ticks; i++) sim.Step(NoCommands);
    }

    /// <summary>A mission over the real map and rules with one human (0), one enemy (1) and the given Lua script, no pre-placed actors.</summary>
    public static (Simulation Sim, MissionHost Host) Tiny(string lua, string? tech = null)
    {
        MissionDef real = MissionLoader.Load(MissionLoader.FolderOf(DataDir, "auth_01"));
        var def = new MissionDef
        {
            Id = "test", Title = "t", MapFile = real.MapFile, ScriptFile = "x.lua", BriefingFile = "x.md", Seed = 7,
            Players = new[]
            {
                new MissionPlayer(0, "a", "human", 0, 5000, tech?.Split(',')),
                new MissionPlayer(1, "b", "ai", 1, 5000, null),
            },
            Zones = new[] { new KeyValuePair<string, MissionZone>("z", new MissionZone(10, 10, 14, 14)) },
            Actors = Array.Empty<MissionActor>(),
        };
        RuleSet rules = RulesLoader.Load(Path.Combine(DataDir, "rules"));
        var map = MapFile.Load(Path.Combine(DataDir, "maps", def.MapFile));
        var host = new MissionHost(def, lua);
        var sim = new Simulation(MissionSession.MakeSetup(def, map, rules, host));
        host.PlaceInitialActors(sim);
        return (sim, host);
    }
}
