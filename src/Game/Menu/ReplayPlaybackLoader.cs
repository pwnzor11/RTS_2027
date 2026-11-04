// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Data;
using Rts.Game.Sandbox;
using Rts.Game.Saves;
using Rts.Scripting;
using Rts.Sim;
using Rts.Sim.Core;
using Rts.Sim.Map;
using Rts.Sim.Replay;

namespace Rts.Game.Menu;

/// <summary>Opens a replay file for the viewer: finds the map it was recorded on, rebuilds its setup (a mission replay re-creates the mission) and returns a <see cref="ReplayPlayback"/>.</summary>
public static class ReplayPlaybackLoader
{
    /// <summary>Opens the replay at <paramref name="path"/> using the data folder beside <paramref name="rulesDir"/>.</summary>
    /// <exception cref="SaveLoadException">The file is damaged, its map or mission is not in this game's data, or the data changed since it was recorded.</exception>
    public static ReplayPlayback Open(string path, string rulesDir)
    {
        ReplayData data;
        try
        {
            using FileStream file = File.OpenRead(path);
            data = ReplayReader.Read(file);
        }
        catch (Exception e) when (e is InvalidDataException or EndOfStreamException or IOException or UnauthorizedAccessException)
        {
            throw new SaveLoadException("The replay could not be read: " + e.Message, e);
        }
        return Open(data, rulesDir, path);
    }

    /// <summary>As <see cref="Open(string, string)"/>, from a parsed replay. <paramref name="replayPath"/> (when known) lets the map saved
    /// beside the replay (<c>&lt;replay&gt;.map.toml</c>) be found first.</summary>
    public static ReplayPlayback Open(ReplayData data, string rulesDir, string? replayPath = null)
    {
        string dataDir = Path.GetFullPath(Path.Combine(rulesDir, ".."));
        try
        {
            return data.Mission is not null ? OpenMission(data, rulesDir, dataDir) : OpenPlain(data, rulesDir, dataDir, replayPath);
        }
        catch (RulesException e)
        {
            throw new SaveLoadException("The game data could not be loaded: " + e.Message, e);
        }
        catch (InvalidDataException e)
        {
            throw new SaveLoadException("This replay cannot be played with the current game data: " + e.Message, e);
        }
    }

    private static ReplayPlayback OpenPlain(ReplayData data, string rulesDir, string dataDir, string? replayPath)
    {
        // The rules the replay was recorded with travel in its header; older files without them use the current rules.
        RuleSet rules = data.RulesFiles.Count > 0 ? RulesBundle.Load(data.RulesFiles) : RulesLoader.Load(rulesDir);
        if (rules.RulesHash != data.RulesHash)
            throw new SaveLoadException(GameLoader.RulesMismatchMessage(data.RulesHash, rules.RulesHash, "replay"));
        MapData map = FindMap(data, dataDir, replayPath);
        SimSetup setup = data.Header.ToSetup(map, rules.Sim, rules.RulesHash);
        return new ReplayPlayback(new Simulation(setup), data);
    }

    private static ReplayPlayback OpenMission(ReplayData data, string rulesDir, string dataDir)
    {
        ReplayMission recorded = data.Mission!;
        string folder = MissionLoader.FolderOf(dataDir, recorded.Id);
        if (recorded.Id != Path.GetFileName(recorded.Id) || !Directory.Exists(folder))
            throw new SaveLoadException($"This replay was recorded on the mission '{recorded.Id}', which is not in this game's data.");
        MissionDef def = MissionLoader.Load(folder);
        string script = File.ReadAllText(Path.Combine(folder, def.ScriptFile));
        var host = new MissionHost(def, script);
        if (host.SourceHash != recorded.ScriptHash)
            throw new SaveLoadException($"The script of mission '{recorded.Id}' has changed since this replay was recorded, so it cannot be played.");
        RuleSet rules = RulesLoader.Load(rulesDir);
        if (rules.RulesHash != data.RulesHash)
            throw new SaveLoadException(GameLoader.RulesMismatchMessage(data.RulesHash, rules.RulesHash, "replay"));
        MapData map = MapFile.Load(Path.Combine(dataDir, "maps", def.MapFile));
        SimSetup setup = MissionSession.MakeSetup(def, map, rules, host);
        data.Header.CheckMatches(setup, MapHasher.Compute(map), "Replay", checkSeed: true);
        return new ReplayPlayback(new Simulation(setup), data, host.PlaceInitialActors, host);
    }

    // The map is not named in a replay, only hashed: try the map saved beside the replay, then the map files, then the generated map of the
    // replay's seed.
    private static MapData FindMap(ReplayData data, string dataDir, string? replayPath = null)
    {
        if (replayPath is not null)
        {
            string beside = SkirmishGame.MapBesideReplay(replayPath);
            if (File.Exists(beside))
            {
                try
                {
                    MapData candidate = MapFile.Load(beside);
                    if (MapHasher.Compute(candidate) == data.MapHash) return candidate;
                }
                catch (Exception e) when (e is InvalidDataException or IOException or MapFileException)
                {
                    // A damaged or stale map beside the replay is not the one.
                }
            }
        }

        string mapsDir = Path.Combine(dataDir, "maps");
        if (Directory.Exists(mapsDir))
        {
            foreach (string file in Directory.EnumerateFiles(mapsDir, "*.map.toml").Order(StringComparer.Ordinal))
            {
                try
                {
                    MapData candidate = MapFile.Load(file);
                    if (MapHasher.Compute(candidate) == data.MapHash) return candidate;
                }
                catch (Exception e) when (e is InvalidDataException or IOException)
                {
                    // A map that does not load is not the one.
                }
            }
        }

        if (data.MapWidth == data.MapHeight && data.MapWidth is >= 8 and <= 256)
        {
            MapData generated = MapGenerator.Generate(data.MapWidth, data.Seed);
            if (MapHasher.Compute(generated) == data.MapHash) return generated;
        }
        throw new SaveLoadException($"The map '{data.Header.MapName}' this replay was recorded on is not in this game's data.");
    }
}
