// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.AI;
using Rts.Data;
using Rts.Game.Sandbox;
using Rts.Scripting;
using Rts.Sim;
using Rts.Sim.Core;
using Rts.Sim.Map;

namespace Rts.Game.Saves;

/// <summary>Rebuilds a game around a saved simulation: rules and map are loaded, the sim and script state restored, and the AI attached afresh.</summary>
public static class GameLoader
{
    /// <summary>What the player is told when a game is loaded: the AI's memory is not saved.</summary>
    public const string AiNote = "The computer players start thinking afresh from the saved position.";

    /// <summary>The message shown when the rules changed since a save (<paramref name="what"/> = "save") or replay ("replay") was made.</summary>
    public static string RulesMismatchMessage(ulong saved, ulong current, string what = "save") =>
        $"This {what} was made with different game data (rules 0x{saved:x16}, this game has 0x{current:x16}). " +
        $"It was probably made by another version of the game and cannot be {(what == "save" ? "loaded" : "played")}.";

    /// <summary>Reads the save at <paramref name="path"/> and rebuilds the game from the rules in <paramref name="rulesDir"/>.</summary>
    /// <exception cref="SaveLoadException">The file cannot be read, does not fit this game's data, or is damaged.</exception>
    public static LoadedGame Load(string path, string rulesDir)
    {
        byte[] bytes;
        try { bytes = File.ReadAllBytes(path); }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            throw new SaveLoadException("The save file could not be read: " + e.Message, e);
        }
        return Load(new MemoryStream(bytes), rulesDir);
    }

    /// <summary>As <see cref="Load(string, string)"/>, from a stream.</summary>
    public static LoadedGame Load(Stream input, string rulesDir)
    {
        GameSaveHeader header = GameSaveFile.ReadHeader(input);
        RuleSet rules;
        try { rules = RulesLoader.Load(rulesDir); }
        catch (RulesException e) { throw new SaveLoadException("The game data could not be loaded: " + e.Message, e); }
        if (rules.RulesHash != header.RulesHash) throw new SaveLoadException(RulesMismatchMessage(header.RulesHash, rules.RulesHash));

        string dataDir = Path.GetFullPath(Path.Combine(rulesDir, ".."));
        try
        {
            return header.Mode == SaveMode.Mission ? LoadMission(input, header, rules, rulesDir, dataDir) : LoadSkirmish(input, header, rules, rulesDir, dataDir);
        }
        catch (Exception e) when (e is InvalidDataException or EndOfStreamException or InvalidOperationException or IOException or ArgumentException)
        {
            throw new SaveLoadException("The save could not be restored: " + e.Message, e);
        }
    }

    private static LoadedGame LoadSkirmish(Stream input, GameSaveHeader header, RuleSet rules, string rulesDir, string dataDir)
    {
        Screens.SkirmishSettings settings = header.Skirmish ?? throw new SaveLoadException("The save is damaged (no skirmish setup).");
        MapData map = ResolveMap(header.MapId, dataDir);
        ulong simSeed = settings.MapSeed == 0 ? SkirmishGame.CuratedSimSeed : settings.MapSeed;
        var setup = new SimSetup(simSeed, map, 2, rules.Sim, rules.RulesHash) { AllowDebug = false, Factions = settings.Factions };
        Simulation sim = Simulation.Load(input, setup);
        AiDriver? ai = BuildAi(rulesDir, header.Ai);
        return new LoadedGame(header, sim, map, ai, null, rulesDir, header.Ai.Count > 0 ? AiNote : "");
    }

    private static LoadedGame LoadMission(Stream input, GameSaveHeader header, RuleSet rules, string rulesDir, string dataDir)
    {
        string folder = MissionLoader.FolderOf(dataDir, header.MissionId);
        if (header.MissionId.Length == 0 || header.MissionId != Path.GetFileName(header.MissionId) || !Directory.Exists(folder))
            throw new SaveLoadException($"This save belongs to the mission '{header.MissionId}', which is not in this game's data.");
        MissionDef def = MissionLoader.Load(folder);
        string script = File.ReadAllText(Path.Combine(folder, def.ScriptFile));
        var host = new MissionHost(def, script);
        if (host.SourceHash != header.ScriptHash)
            throw new SaveLoadException($"The script of mission '{header.MissionId}' has changed since this save was made, so the save cannot be loaded.");
        MapData map = MapFile.Load(Path.Combine(dataDir, "maps", def.MapFile));
        Simulation sim = Simulation.Load(input, MissionSession.MakeSetup(def, map, rules, host));
        AiDriver? ai = BuildAi(rulesDir, header.Ai);
        return new LoadedGame(header, sim, map, ai, host, rulesDir, header.Ai.Count > 0 ? AiNote : "");
    }

    /// <summary>The map a save names (<c>file:NAME</c> under data/maps, or <c>generated:SIZE:SEED</c>).</summary>
    public static MapData ResolveMap(string mapId, string dataDir)
    {
        if (mapId.StartsWith("file:", StringComparison.Ordinal))
        {
            string name = mapId[5..];
            if (name.Length == 0 || name != Path.GetFileName(name)) throw new SaveLoadException("The save names an invalid map file.");
            string path = Path.Combine(dataDir, "maps", name);
            if (!File.Exists(path)) throw new SaveLoadException($"The map '{name}' this save was made on is not in this game's data.");
            return MapFile.Load(path);
        }
        string[] parts = mapId.Split(':');
        if (parts.Length == 3 && parts[0] == "generated" && int.TryParse(parts[1], out int size) && ulong.TryParse(parts[2], out ulong seed))
            return MapGenerator.Generate(size, seed);
        throw new SaveLoadException($"The save names a map this game does not know ('{mapId}').");
    }

    private static AiDriver? BuildAi(string rulesDir, IReadOnlyList<SavedAi> slots)
    {
        if (slots.Count == 0) return null;
        var driver = new AiDriver(slots[0].Seed);
        foreach (SavedAi slot in slots)
        {
            DifficultyProfile profile = LoadProfile(rulesDir, slot.Tier);
            if (!AiRegistry.TryCreate(slot.AiId, profile, out IAiPlayer? ai))
                throw new SaveLoadException($"This save uses the computer player '{slot.AiId}', which this game does not have.");
            driver.Add(slot.Slot, ai, profile);
        }
        return driver;
    }

    private static DifficultyProfile LoadProfile(string rulesDir, int tier)
    {
        try
        {
            IReadOnlyList<DifficultyProfile> all = DifficultyLoader.Load(DifficultyLoader.PathBesideRules(rulesDir));
            return all[Math.Clamp(tier, 1, all.Count) - 1];
        }
        catch (RulesException)
        {
            return DifficultyProfile.Default;
        }
    }
}
