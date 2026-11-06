// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Data;
using Rts.Sim;
using Rts.Sim.Core;
using Rts.Sim.Map;
using Rts.Sim.Rules;

namespace Rts.Scripting.Tests.M8_04;

/// <summary>Builds small mission worlds for the M8-04 tests: a 40x30 map with sea in the west columns (x 0..9) and land elsewhere,
/// the real rules, and a chosen faction for player 0.</summary>
internal static class World
{
    public const int W = 40, H = 30, SeaWidth = 10;

    public static MapData Map(bool sea)
    {
        var land = new LandType[W * H];
        for (int y = 0; y < H; y++)
            for (int x = 0; x < W; x++)
                land[y * W + x] = sea && x < SeaWidth ? LandType.Water : LandType.Clear;
        return new MapData("m804", "rust_belt", W, H, new ushort[W * H], land, new byte[W * H], new OverlayKind[W * H], new byte[W * H],
            new[] { new CellCoord(20, 5), new CellCoord(30, 25) });
    }

    public static MissionDef Def(string lua) => new()
    {
        Id = "m804", Title = "t", MapFile = "x.map.toml", ScriptFile = "x.lua", BriefingFile = "x.md", Seed = 11,
        Players = new[]
        {
            new MissionPlayer(0, "a", "human", 0, 5000, null),
            new MissionPlayer(1, "b", "ai", 1, 5000, null),
        },
        Zones = new[]
        {
            new KeyValuePair<string, MissionZone>("land", new MissionZone(15, 5, 19, 9)),
            new KeyValuePair<string, MissionZone>("drop", new MissionZone(20, 14, 24, 18)),
        },
        Actors = Array.Empty<MissionActor>(),
    };

    public static SimSetup Setup(MissionDef def, MissionHost host, Faction f0, bool sea)
    {
        RuleSet rules = RulesLoader.Load(Path.Combine(TestData.DataDir, "rules"));
        return MissionSession.MakeSetup(def, Map(sea), rules, host) with { Factions = new[] { f0, Faction.Directorate } };
    }

    public static (Simulation Sim, MissionHost Host, MissionDef Def) Build(string lua, Faction f0 = Faction.Authority, bool sea = false)
    {
        MissionDef def = Def(lua);
        var host = new MissionHost(def, lua);
        var sim = new Simulation(Setup(def, host, f0, sea));
        host.PlaceInitialActors(sim);
        return (sim, host, def);
    }

    /// <summary>Saves <paramref name="sim"/> and loads it into a fresh host built from the same script.</summary>
    public static (Simulation Sim, MissionHost Host) Reload(Simulation sim, MissionDef def, string lua, Faction f0, bool sea)
    {
        using var ms = new MemoryStream();
        sim.Save(ms);
        ms.Position = 0;
        var host = new MissionHost(def, lua);
        return (Simulation.Load(ms, Setup(def, host, f0, sea)), host);
    }

    public static int Count(Simulation sim, int player, string typeId)
    {
        int n = 0;
        int v = SimRules.IndexOf(sim.Setup.Rules.Vehicles, typeId, t => t.Id);
        if (v >= 0)
            for (int i = 1; i < sim.Units.Capacity; i++)
                if (sim.Units.Slots.IsIndexAlive(i) && sim.Units.Health[i] > 0 && sim.Units.Player[i] == player && sim.Units.TypeIndex[i] == v) n++;
        int f = sim.Setup.Rules.Infantry == null ? -1 : SimRules.IndexOf(sim.Setup.Rules.Infantry, typeId, t => t.Id);
        if (f >= 0)
            for (int i = 1; i < sim.Infantry.Capacity; i++)
                if (sim.Infantry.Slots.IsIndexAlive(i) && sim.Infantry.Health[i] > 0 && sim.Infantry.Player[i] == player && sim.Infantry.TypeIndex[i] == f) n++;
        return n;
    }
}
