// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Data;
using Rts.Sim;
using Rts.Sim.Core;
using Rts.Sim.Map;

namespace Rts.Scripting;

/// <summary>
/// A mission ready to run: rules, map, the <see cref="Simulation"/> with its <see cref="MissionHost"/> attached, pre-placed actors and
/// starting cash set. Built from <c>data/missions/&lt;id&gt;</c> by <see cref="Create"/>. The AI for the enemy slot is attached by the caller
/// (the Game project owns the AI reference).
/// </summary>
public sealed class MissionSession
{
    private MissionSession(MissionDef def, MissionHost host, Simulation sim, MapData map, RuleSet rules, string briefing, string scriptSource)
    {
        Def = def; Host = host; Sim = sim; Map = map; Rules = rules; Briefing = briefing; ScriptSource = scriptSource;
    }

    /// <summary>The parsed mission.toml.</summary>
    public MissionDef Def { get; }

    /// <summary>The script host (objectives, messages, errors).</summary>
    public MissionHost Host { get; }

    /// <summary>The simulation, set up and ready for its first Step.</summary>
    public Simulation Sim { get; }

    /// <summary>The mission map.</summary>
    public MapData Map { get; }

    /// <summary>The loaded rules.</summary>
    public RuleSet Rules { get; }

    /// <summary>The briefing text.</summary>
    public string Briefing { get; }

    /// <summary>The Lua source (used to build a second host for a load).</summary>
    public string ScriptSource { get; }

    /// <summary>Slot of the human player (the first player whose role is "human").</summary>
    public byte HumanSlot => (byte)Def.Players.First(p => p.Role == "human").Slot;

    /// <summary>Slots whose role is "ai".</summary>
    public IEnumerable<byte> AiSlots => Def.Players.Where(p => p.Role == "ai").Select(p => (byte)p.Slot);

    /// <summary>The setup a mission uses: seed, map, one player per mission player with the mission's teams, and the given extension.</summary>
    public static SimSetup MakeSetup(MissionDef def, MapData map, RuleSet rules, MissionHost host) =>
        new(def.Seed, map, def.Players.Count, rules.Sim, rules.RulesHash)
        {
            AllowDebug = false,
            Teams = def.Players.Select(p => p.Team).ToArray(),
            Extensions = new[] { host },
        };

    /// <summary>Loads <c>data/missions/&lt;missionId&gt;</c> under <paramref name="dataDir"/> (the folder holding rules, maps and missions) and builds a new game.</summary>
    /// <exception cref="InvalidDataException">The mission files are invalid.</exception>
    /// <exception cref="RulesException">The rules data is missing or invalid.</exception>
    /// <param name="dataDir">The folder holding rules, maps and missions.</param>
    /// <param name="missionId">Folder name under data/missions.</param>
    /// <param name="adjustDef">Optional hook that returns the mission definition to run (M8-07: the campaign swaps in the computed tech allow-list); null = as loaded.</param>
    public static MissionSession Create(string dataDir, string missionId, Func<MissionDef, MissionDef>? adjustDef = null)
    {
        string folder = MissionLoader.FolderOf(dataDir, missionId);
        MissionDef def = MissionLoader.Load(folder);
        if (adjustDef != null) def = adjustDef(def);
        string script = File.ReadAllText(Path.Combine(folder, def.ScriptFile));
        string briefing = File.ReadAllText(Path.Combine(folder, def.BriefingFile));
        RuleSet rules = RulesLoader.Load(Path.Combine(dataDir, "rules"));
        MapData map = MapFile.Load(Path.Combine(dataDir, "maps", def.MapFile));
        var host = new MissionHost(def, script);
        var sim = new Simulation(MakeSetup(def, map, rules, host));
        host.PlaceInitialActors(sim);
        return new MissionSession(def, host, sim, map, rules, briefing, script);
    }
}
