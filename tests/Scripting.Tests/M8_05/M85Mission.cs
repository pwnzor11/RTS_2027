// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Data;
using Rts.Sim;
using Rts.Sim.Core;

namespace Rts.Scripting.Tests;

/// <summary>M8-05 helper: the same tiny mission as <see cref="TestData.Tiny"/>, with a locale attached to the definition.</summary>
internal static class M85Mission
{
    public const string SampleLocale = """
        "m1.warning" = "Commander, the convoy is late."
        "m1.line2" = "Hold the junction."
        [voice]
        "m1.warning" = "voice/strand/m1.warning.ogg"
        """;

    public static (Simulation Sim, MissionHost Host) Build(string lua, MissionLocale? locale = null)
    {
        MissionDef real = MissionLoader.Load(MissionLoader.FolderOf(TestData.DataDir, "auth_01"));
        var def = new MissionDef
        {
            Id = "test", Title = "t", MapFile = real.MapFile, ScriptFile = "x.lua", BriefingFile = "x.md", Seed = 7,
            Players = new[]
            {
                new MissionPlayer(0, "a", "human", 0, 5000, null),
                new MissionPlayer(1, "b", "ai", 1, 5000, null),
            },
            Zones = new[] { new KeyValuePair<string, MissionZone>("z", new MissionZone(10, 10, 14, 14)) },
            Actors = Array.Empty<MissionActor>(),
            Locale = locale ?? MissionLocale.Empty,
        };
        RuleSet rules = RulesLoader.Load(Path.Combine(TestData.DataDir, "rules"));
        var map = MapFile.Load(Path.Combine(TestData.DataDir, "maps", def.MapFile));
        var host = new MissionHost(def, lua);
        var sim = new Simulation(MissionSession.MakeSetup(def, map, rules, host));
        host.PlaceInitialActors(sim);
        return (sim, host);
    }

    public static MissionLocale Sample() => MissionLocale.Parse(SampleLocale, "locale.toml");
}
