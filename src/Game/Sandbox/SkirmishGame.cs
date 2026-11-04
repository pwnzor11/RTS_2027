// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.AI;
using Rts.Data;
using Rts.Game.Saves;
using Rts.Game.Ui.Llm;
using Rts.Game.Screens;
using Rts.Llm;
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
public sealed class SkirmishGame : IDisposable
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

    private readonly string _mapId;
    private readonly IReadOnlyList<SavedAi> _savedAi;

    private SkirmishGame(Simulation sim, MapData map, MapMeta meta, RuleSet rules, AiDriver ai, string mapLabel, string aiId, bool curated,
        byte? local, string mapId, IReadOnlyList<SavedAi> savedAi)
    {
        Sim = sim;
        Map = map;
        MapMeta = meta;
        Rules = rules;
        Ai = ai;
        MapLabel = mapLabel;
        AiId = aiId;
        Curated = curated;
        Local = local;
        _mapId = mapId;
        _savedAi = savedAi;
    }

    /// <summary>The local human's player number (always 0: the human comes first), or null in a spectator game.</summary>
    // ASSUMED (M4-11): the contract names this "byte? SkirmishGame.LocalPlayer", but LocalPlayer is a const byte used by other files
    // (SpectatorSetup, tests), so the nullable one is Local.
    public byte? Local { get; }

    /// <summary>Metadata of the map (features, generator provenance); <see cref="MapMeta.DefaultFor"/> for a version-1 file or the legacy path.</summary>
    public MapMeta MapMeta { get; }

    /// <summary>True when nobody is Human: AI against AI.</summary>
    public bool IsSpectator => Local is null;

    /// <summary>The simulation, set up and ready for its first Step.</summary>
    public Simulation Sim { get; }

    /// <summary>The map.</summary>
    public MapData Map { get; }

    /// <summary>The loaded rules.</summary>
    public RuleSet Rules { get; }

    /// <summary>The driver holding the AI on slot <see cref="AiPlayer"/>; give it to <see cref="Rts.Game.Host.SimHost.Ai"/>.</summary>
    public AiDriver Ai { get; }

    /// <summary>M5: the commander chat shared by every LLM commander in this game (M5-05 writes, the chat panel M5-07 reads).</summary>
    public CommanderChatLog Chat { get; } = new();

    /// <summary>M5: the LLM commanders of this game, one per slot whose <see cref="SkirmishSlot.Commander"/> is set (M5-05 fills; empty
    /// otherwise).</summary>
    public IReadOnlyList<ICommanderChannel> Commanders { get; private set; } = Array.Empty<ICommanderChannel>();

    private readonly List<Commander> _commanders = new();

    /// <summary>Cancels any call the LLM commanders still have in flight (M5-05). Safe to call more than once.</summary>
    public void Dispose()
    {
        foreach (Commander c in _commanders) c.Dispose();
    }

    private void AttachCommanders(IEnumerable<(byte Player, SkirmishSlot Slot)> slots, Settings app)
    {
        LlmConfig cfg = LlmHooks.Defaults.Resolve(app.ProviderKind, app.LlmBaseUrl, app.LlmModel);
        IReadOnlyList<CommanderPersona>? personas = null;
        foreach ((byte player, SkirmishSlot slot) in slots)
        {
            if (!slot.Commander) continue;
            IChatProvider? provider = app.ProviderKind == LlmProviderKind.None ? null : ProviderFactory.Create(cfg, LlmHooks.SecretStore.Load());
            personas ??= PersonaCatalog.Load(Path.Combine(AppContext.BaseDirectory, "data", "llm", "personas.toml"));
            string faction = slot.Faction.ToString();
            CommanderPersona persona = PersonaCatalog.Pick(personas, faction, player);
            var commander = new Commander(player, provider, persona, faction, app.LlmCallCap, app.LlmChatOn, Chat);
            Ai.SetDirectiveSource(player, commander);
            _commanders.Add(commander);
        }
        Commanders = _commanders.ToArray();
    }

    /// <summary>Which map this is, for the setup screen: "Rust Belt (curated)" or "Generated 64x64".</summary>
    public string MapLabel { get; }

    /// <summary>The AI id actually used ("recruit" if registered, else "skirmisher").</summary>
    public string AiId { get; }

    /// <summary>True when the curated map was used (map seed 0).</summary>
    public bool Curated { get; }

    /// <summary>The fixed part of a save header for this game (see <see cref="GameSaver.Capture"/>), given the setup choices it was started with.</summary>
    public GameSaveHeader SaveTemplate(SkirmishSettings settings) => new()
    {
        Mode = SaveMode.Skirmish,
        MapId = _mapId,
        MapName = Map.Name,
        RulesHash = Rules.RulesHash,
        LocalPlayer = Local ?? 0,
        Skirmish = settings,
        Ai = _savedAi,
    };

    /// <summary>
    /// Writes the map next to a replay as <c>&lt;replayPath&gt;.map.toml</c> (with its metadata) so the replay can be checked or watched with
    /// the map it was recorded on. Only maps that are not shipped in data/maps need it: returns null (writes nothing) for the curated map.
    /// </summary>
    /// <returns>The path written, or null for the curated map.</returns>
    public string? SaveMapBeside(string replayPath)
    {
        ArgumentException.ThrowIfNullOrEmpty(replayPath);
        if (Curated) return null;
        string path = MapBesideReplay(replayPath);
        MapFile.Save(Map, MapMeta, path);
        return path;
    }

    /// <summary>The path <see cref="SaveMapBeside"/> writes for a replay file: the replay path plus <c>.map.toml</c>.</summary>
    public static string MapBesideReplay(string replayPath) => replayPath + ".map.toml";

    /// <summary>The map description the setup screen shows for a map choice (no files are read).</summary>
    public static string DescribeMap(MapChoice map)
    {
        if (map.Generated is { } g)
            return $"{MapPicker.BiomeName(g.Biome)} {MapGenerator.CellsFor(g.Size)}x{MapGenerator.CellsFor(g.Size)}, seed {g.Seed}";
        return map.File == CuratedMapFile ? "Rust Belt (curated)" : MapPicker.LabelOf(map.File ?? "");
    }

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
        string mapId = curated ? "file:" + CuratedMapFile : $"generated:{GeneratedMapSize}:{mapSeed}";
        return new SkirmishGame(sim, map, MapMeta.DefaultFor(map), rules, driver, DescribeMap(mapSeed), aiId, curated, LocalPlayer, mapId,
            new[] { new SavedAi(AiPlayer, aiId, aiTier, factions is { Count: > 1 } ? factions[1] : Faction.Authority, simSeed) });
    }

    /// <summary>
    /// Builds the skirmish the setup screen describes (M4-11): 2 to 4 players, a map file or a generated map, a neutral slot when the map
    /// has features, one AI per AI slot, and no local player when nobody is Human. Closed slots are not players and get no start.
    /// The curated map with the default settings builds exactly the same game as <see cref="Create(string, ulong, int, IReadOnlyList{Faction})"/>.
    /// </summary>
    /// <param name="rulesDir">The data/rules directory.</param>
    /// <param name="settings">The setup choices; <see cref="SkirmishSettings.Validate"/> must return no errors.</param>
    /// <param name="appSettings">M5: the app settings (LLM provider, chat, call cap). Null = no LLM commanders even if a slot asks for one.</param>
    /// <exception cref="RulesException">The rules data is missing or invalid.</exception>
    /// <exception cref="InvalidOperationException">The settings are invalid, the map has too few starts or cannot be read, or a faction's start kit names a missing type.</exception>
    public static SkirmishGame Create(string rulesDir, SkirmishSettings settings, Settings? appSettings = null)
    {
        ArgumentNullException.ThrowIfNull(settings);
        IReadOnlyList<string> errors = settings.Validate();
        if (errors.Count > 0) throw new InvalidOperationException(errors[0]);

        RuleSet rules = RulesLoader.Load(rulesDir);
        int players = settings.PlayerCount;
        (MapData map, MapMeta meta, string mapId) = LoadMap(rulesDir, settings);
        if (players > map.StartPositions.Count)
            throw new InvalidOperationException($"The map '{map.Name}' has {map.StartPositions.Count} start positions; {players} players need {players}.");
        if (meta.MaxPlayers > 0 && players > meta.MaxPlayers)
            throw new InvalidOperationException($"The map '{map.Name}' is for at most {meta.MaxPlayers} players.");

        SimSetup setup = MakeSetup(rules, settings, map, meta);
        bool neutral = meta.Features.Count > 0;
        var sim = new Simulation(setup);
        SkirmishStart.Apply(sim, SkirmishStart.ChooseStarts(players, map.StartPositions.Count));
        if (neutral) MapFeatures.Place(sim, meta, (byte)players);

        string aiId = ChooseAiId();
        var driver = new AiDriver(settings.SimSeed);
        var saved = new List<SavedAi>();
        foreach ((byte player, SkirmishSlot slot) in settings.AiPlayers)
        {
            DifficultyProfile profile = LoadProfile(rulesDir, slot.AiTier);
            if (!AiRegistry.TryCreate(aiId, profile, out IAiPlayer? ai))
                throw new InvalidOperationException($"AI '{aiId}' is not registered");
            driver.Add(player, ai, profile);
            saved.Add(new SavedAi(player, aiId, slot.AiTier, slot.Faction, settings.SimSeed));
        }

        bool curated = settings.Map.File == CuratedMapFile;
        byte? local = settings.IsSpectator ? null : (byte)0;
        var game = new SkirmishGame(sim, map, meta, rules, driver, DescribeMap(settings.Map), aiId, curated, local, mapId, saved);
        if (appSettings is not null) game.AttachCommanders(settings.AiPlayers, appSettings);
        return game;
    }

    /// <summary>
    /// The SimSetup of a skirmish (M4-INT2: shared by <see cref="Create(string, SkirmishSettings, Settings)"/> and the save loader, so a loaded game has the
    /// same players, teams and factions): one sim player per open slot, plus a neutral player on team 255 after them when the map has features.
    /// </summary>
    public static SimSetup MakeSetup(RuleSet rules, SkirmishSettings settings, MapData map, MapMeta meta)
    {
        int players = settings.PlayerCount;
        bool neutral = meta.Features.Count > 0;
        int total = players + (neutral ? 1 : 0);
        IReadOnlyList<Faction>? factions = settings.Factions;
        IReadOnlyList<byte>? teams = settings.Teams;
        if (neutral)
        {
            var f = new Faction[total];
            for (int i = 0; i < total; i++) f[i] = factions is not null && i < factions.Count ? factions[i] : Faction.Authority;
            factions = f;
            var t = new byte[total];
            for (int i = 0; i < players; i++) t[i] = teams is not null ? teams[i] : (byte)i;
            t[players] = Teams.Neutral;
            teams = t;
        }

        return new SimSetup(settings.SimSeed, map, total, rules.Sim, rules.RulesHash) { AllowDebug = false, Factions = factions, Teams = teams };
    }

    /// <summary>The id a save or replay uses for a generated map: <c>gen:BIOME:SIZE:PLAYERS:SEED</c>.</summary>
    public static string GeneratedMapId(MapGenParams gen) =>
        $"gen:{MapFile.BiomeKeys[(int)gen.Biome]}:{(int)gen.Size}:{gen.Players}:{gen.Seed}";

    /// <summary>Loads the map the settings name, with its metadata and the id a save records for it.</summary>
    public static (MapData Map, MapMeta Meta, string MapId) LoadMap(string rulesDir, SkirmishSettings settings)
    {
        if (settings.ResolvedGenerator is { } gen)
        {
            (MapData generated, MapMeta meta) = MapGenerator.GenerateWithMeta(gen);
            return (generated, meta, GeneratedMapId(gen));
        }
        string file = settings.Map.File!;
        string path = MapPicker.ResolveFile(rulesDir, file);
        try
        {
            (MapData map, MapMeta meta) = MapFile.LoadWithMeta(path);
            return (map, meta, (file == CuratedMapFile ? "file:" : "user:") + file);
        }
        catch (MapFileException e)
        {
            throw new InvalidOperationException("Cannot load the map: " + e.Message, e);
        }
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
