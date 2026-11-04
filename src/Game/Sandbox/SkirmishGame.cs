// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.AI;
using Rts.Data;
using Rts.Sim;
using Rts.Sim.Core;
using Rts.Sim.Map;
using Rts.Sim.Rules;

namespace Rts.Game.Sandbox;

/// <summary>
/// The M2 skirmish as the menu starts it (D-062, D-067): two players, the curated Rust Belt map for seed 0 or a generated
/// 64x64 map for any other seed, <c>AllowDebug</c> off, <see cref="SkirmishStart.Apply"/> for the starting Construction Yards,
/// and an <see cref="AiDriver"/> on player 1. Shared by the game screen and <c>--skirmish-check</c> so both start identically.
/// </summary>
public sealed class SkirmishGame
{
    /// <summary>The human player's slot.</summary>
    public const byte LocalPlayer = 0;

    /// <summary>The AI player's slot.</summary>
    public const byte AiPlayer = 1;

    /// <summary>Map file for seed 0, looked up in data/maps.</summary>
    public const string CuratedMapFile = "rustbelt_m2.map.toml";

    /// <summary>Size of generated maps, in cells.</summary>
    public const int GeneratedMapSize = 64;

    /// <summary>Simulation seed used with the curated map (map seed 0).</summary>
    public const ulong CuratedSimSeed = 2027;

    /// <summary>Id of the Recruit AI, used when the registry knows it.</summary>
    public const string RecruitAiId = "recruit";

    private SkirmishGame(Simulation sim, MapData map, RuleSet rules, AiDriver ai, string mapLabel, string aiId, bool curated)
    {
        Sim = sim;
        Map = map;
        Rules = rules;
        Ai = ai;
        MapLabel = mapLabel;
        AiId = aiId;
        Curated = curated;
    }

    /// <summary>The simulation, set up and ready for its first Step.</summary>
    public Simulation Sim { get; }

    /// <summary>The map.</summary>
    public MapData Map { get; }

    /// <summary>The loaded rules.</summary>
    public RuleSet Rules { get; }

    /// <summary>The driver holding the AI on slot <see cref="AiPlayer"/>; give it to <see cref="Rts.Game.Host.SimHost.Ai"/>.</summary>
    public AiDriver Ai { get; }

    /// <summary>Which map this is, for the setup screen: "Rust Belt (curated)" or "Generated 64x64".</summary>
    public string MapLabel { get; }

    /// <summary>The AI id actually used ("recruit" if registered, else "skirmisher").</summary>
    public string AiId { get; }

    /// <summary>True when the curated map was used (map seed 0).</summary>
    public bool Curated { get; }

    /// <summary>The map description the setup screen shows for a map seed (no files are read).</summary>
    public static string DescribeMap(ulong mapSeed) =>
        mapSeed == 0 ? "Rust Belt (curated)" : $"Generated {GeneratedMapSize}x{GeneratedMapSize}";

    /// <summary>The AI id used for new games: Recruit once the registry has it, else the Skirmisher placeholder.</summary>
    public static string ChooseAiId() => AiRegistry.Contains(RecruitAiId) ? RecruitAiId : AiRegistry.SkirmisherId;

    /// <summary>Builds the skirmish.</summary>
    /// <param name="rulesDir">The data/rules directory.</param>
    /// <param name="mapSeed">0 = curated Rust Belt map; anything else seeds a generated map and the simulation.</param>
    /// <param name="aiTier">AI difficulty tier 1..5.</param>
    /// <param name="factions">Faction per player slot for <see cref="SimSetup.Factions"/> (M3); null = all Authority.</param>
    /// <exception cref="RulesException">The rules data is missing or invalid.</exception>
    /// <exception cref="InvalidOperationException">A chosen faction's start kit names a type the rules do not have yet.</exception>
    public static SkirmishGame Create(string rulesDir, ulong mapSeed, int aiTier, IReadOnlyList<Faction>? factions = null)
    {
        RuleSet rules = RulesLoader.Load(rulesDir);
        bool curated = mapSeed == 0;
        MapData map = curated
            ? MapFile.Load(Path.Combine(rulesDir, "..", "maps", CuratedMapFile))
            : MapGenerator.Generate(GeneratedMapSize, mapSeed);
        ulong simSeed = curated ? CuratedSimSeed : mapSeed;
        var setup = new SimSetup(simSeed, map, 2, rules.Sim, rules.RulesHash) { AllowDebug = false, Factions = factions };
        var sim = new Simulation(setup);
        SkirmishStart.Apply(sim);

        DifficultyProfile profile = LoadProfile(rulesDir, aiTier);
        string aiId = ChooseAiId();
        if (!AiRegistry.TryCreate(aiId, profile, out IAiPlayer? ai))
            throw new InvalidOperationException($"AI '{aiId}' is not registered");
        var driver = new AiDriver(simSeed);
        driver.Add(AiPlayer, ai, profile);
        return new SkirmishGame(sim, map, rules, driver, DescribeMap(mapSeed), aiId, curated);
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
