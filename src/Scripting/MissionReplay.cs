// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Data;
using Rts.Sim;
using Rts.Sim.Core;
using Rts.Sim.Map;
using Rts.Sim.Replay;

namespace Rts.Scripting;

/// <summary>
/// Mission replays (M3-19, D-071). A mission replay stores the mission id and the hash of its script; playing it re-creates the
/// mission from <c>data/missions/&lt;id&gt;</c>, refuses a script that differs from the recorded one, and re-runs the script inside the
/// sim through the <see cref="MissionHost"/> extension while <see cref="ReplayPlayer"/> feeds the recorded commands. Script spawns are
/// never recorded: the script makes them again, deterministically. The Sim never references this layer; the hook is
/// <see cref="ReplayPlayer.Run(ReplayData, SimSetup, Action{Simulation})"/>.
/// </summary>
public static class MissionReplay
{
    /// <summary>What a recording of <paramref name="session"/> stores in its header.</summary>
    public static ReplayMission RefFor(MissionSession session)
    {
        ArgumentNullException.ThrowIfNull(session);
        return new ReplayMission(session.Def.Id, session.Host.SourceHash);
    }

    /// <summary>Plays a mission replay from the mission data under <paramref name="dataDir"/> (the folder holding rules, maps, missions).</summary>
    /// <exception cref="InvalidDataException">The replay has no mission, the script hash differs, the mission files are invalid, or the replay does not fit the mission.</exception>
    /// <exception cref="RulesException">The rules data is missing or invalid.</exception>
    public static ReplayResult Play(ReplayData data, string dataDir) => Play(data, dataDir, out _);

    /// <summary>As <see cref="Play(ReplayData, string)"/>, also returning the script host that ran (outcome, objectives, errors).</summary>
    public static ReplayResult Play(ReplayData data, string dataDir, out MissionHost host)
    {
        ArgumentNullException.ThrowIfNull(data);
        ReplayMission recorded = data.Mission ?? throw new InvalidDataException("This replay was not recorded on a mission.");

        string folder = MissionLoader.FolderOf(dataDir, recorded.Id);
        if (!Directory.Exists(folder))
            throw new InvalidDataException($"Replay was recorded on mission '{recorded.Id}', which is not in {Path.Combine(dataDir, "missions")}.");
        MissionDef def = MissionLoader.Load(folder);
        string script = File.ReadAllText(Path.Combine(folder, def.ScriptFile));
        var h = new MissionHost(def, script);
        if (h.SourceHash != recorded.ScriptHash)
            throw new InvalidDataException(
                $"Script mismatch: mission '{recorded.Id}' was recorded with script hash 0x{recorded.ScriptHash:x16}, but {def.ScriptFile} now hashes to 0x{h.SourceHash:x16}. " +
                "The replay cannot be played with a changed script; use the build that recorded it.");

        RuleSet rules = RulesLoader.Load(Path.Combine(dataDir, "rules"));
        MapData map = MapFile.Load(Path.Combine(dataDir, "maps", def.MapFile));
        SimSetup setup = MissionSession.MakeSetup(def, map, rules, h);
        host = h;
        return ReplayPlayer.Run(data, setup, h.PlaceInitialActors);
    }
}
