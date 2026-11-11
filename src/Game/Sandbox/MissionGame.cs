// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.AI;
using Rts.Data;
using Rts.Game.Saves;
using Rts.Scripting;
using Rts.Sim;
using Rts.Sim.Map;
using Rts.Sim.Replay;

namespace Rts.Game.Sandbox;

/// <summary>
/// A campaign mission as the game runs it (D-061): the <see cref="MissionSession"/> (sim + Lua host) plus an <see cref="AiDriver"/> for the
/// mission's "ai" slots. Shared by the mission game screen and <c>--mission-check</c>, so both start the same way.
/// </summary>
public sealed class MissionGame
{
    /// <summary>The human player's slot.</summary>
    public const byte LocalPlayer = 0;

    private MissionGame(MissionSession session, AiDriver? ai, string rulesDir, int aiTier)
        : this(session.Def, session.Host, session.Sim, session.Map, session.Rules.RulesHash, ai, rulesDir, aiTier)
    {
        Session = session;
    }

    private MissionGame(MissionDef def, MissionHost host, Simulation sim, MapData map, ulong rulesHash, AiDriver? ai, string rulesDir, int aiTier)
    {
        RulesDir = rulesDir;
        Def = def;
        Host = host;
        Sim = sim;
        Map = map;
        Ai = ai;
        _rulesHash = rulesHash;
        _aiTier = aiTier;
        ai?.SetMissionHooks(new MissionAiHooks(host, MissionAiHooks.LoadTiers(rulesDir)));   // M8-06: AI.* script hooks (read-only, D-051)
    }

    private readonly ulong _rulesHash;
    private readonly int _aiTier;

    /// <summary>The data/rules directory the mission was built from (its files go into a replay header).</summary>
    public string RulesDir { get; }

    /// <summary>The loaded mission session when this is a new game; null for a game restored from a save (see <see cref="FromSave"/>).</summary>
    public MissionSession? Session { get; }

    /// <summary>The parsed mission.toml.</summary>
    public MissionDef Def { get; }

    /// <summary>The AI driver for the mission's AI slots (give it to <see cref="Rts.Game.Host.SimHost.Ai"/>), or null if there are none.</summary>
    public AiDriver? Ai { get; }

    /// <summary>The simulation.</summary>
    public Simulation Sim { get; }

    /// <summary>The map.</summary>
    public MapData Map { get; }

    /// <summary>The script host (objectives, messages, errors).</summary>
    public MissionHost Host { get; }

    /// <summary>
    /// S18 MIS-01 / MIS-05: whether the mission screen should end. A scripted mission ends only when the script has decided
    /// (<see cref="MissionHost.Outcome"/>) or the local player is defeated; the sim's own elimination rule (<see cref="Simulation.GameOver"/>)
    /// neither ends it early (an enemy with no buildings left) nor keeps it running (a third team still alive after Mission.Lose).
    /// Only when the script has halted on a fatal error does the sim's rule decide, so the game can still end.
    /// </summary>
    public bool IsOver => Host.Outcome != MissionOutcome.None || Sim.Players.Defeated[LocalPlayer] || (Host.Halted && Sim.GameOver);

    /// <summary>S18 MIS-01: true when the end screen should say Victory: the script won, or (halted script only) the sim's winning team is ours.</summary>
    public bool LocalWon => Host.Outcome == MissionOutcome.Won
        || (Host.Outcome == MissionOutcome.None && !Sim.Players.Defeated[LocalPlayer] && Sim.WinningTeam >= 0 && Sim.WinningTeam == Sim.Setup.TeamOf(LocalPlayer));

    /// <summary>What a replay of this mission stores in its header: the mission id and script hash (M3-19).</summary>
    public ReplayMission ReplayRef => new(Def.Id, Host.SourceHash);

    /// <summary>The fixed part of a save header for this game (see <see cref="GameSaver.Capture"/>).</summary>
    public GameSaveHeader SaveTemplate()
    {
        var ai = new List<SavedAi>();
        if (Ai is not null)
            foreach (MissionPlayer p in Def.Players)
                if (p.Role == "ai") ai.Add(new SavedAi((byte)p.Slot, SkirmishGame.ChooseAiId(), _aiTier, Sim.Setup.FactionOf((byte)p.Slot), Def.Seed));
        return new GameSaveHeader
        {
            Mode = SaveMode.Mission,
            MapId = "file:" + Def.MapFile,
            MapName = Map.Name,
            RulesHash = _rulesHash,
            LocalPlayer = LocalPlayer,
            MissionId = Def.Id,
            ScriptHash = Host.SourceHash,
            Ai = ai,
        };
    }

    /// <summary>Wraps a mission restored from a save (the script state is already inside the simulation).</summary>
    public static MissionGame FromSave(LoadedGame loaded)
    {
        MissionHost host = loaded.Host ?? throw new InvalidOperationException("not a mission save");
        int tier = loaded.Header.Ai.Count > 0 ? loaded.Header.Ai[0].Tier : 1;
        return new MissionGame(host.Def, host, loaded.Sim, loaded.Map, loaded.Header.RulesHash, loaded.Ai, loaded.RulesDir, tier);
    }

    /// <summary>Builds the mission <paramref name="missionId"/> from the data folder beside <paramref name="rulesDir"/>.</summary>
    /// <param name="rulesDir">The data/rules directory.</param>
    /// <param name="missionId">Folder name under data/missions.</param>
    /// <param name="aiTier">AI difficulty tier 1..5 for the AI slots.</param>
    /// <param name="adjustDef">Optional hook returning the mission definition to run (M8-07: campaign tech allow-list); null = as loaded.</param>
    public static MissionGame Create(string rulesDir, string missionId, int aiTier = 1, Func<MissionDef, MissionDef>? adjustDef = null)
    {
        string dataDir = Path.GetFullPath(Path.Combine(rulesDir, ".."));
        MissionSession session = MissionSession.Create(dataDir, missionId, adjustDef);
        AiDriver? driver = null;
        foreach (byte slot in session.AiSlots)
        {
            DifficultyProfile profile = LoadProfile(rulesDir, aiTier);
            string aiId = SkirmishGame.ChooseAiId();
            if (!AiRegistry.TryCreate(aiId, profile, out IAiPlayer? ai))
                throw new InvalidOperationException($"AI '{aiId}' is not registered");
            driver ??= new AiDriver(session.Def.Seed);
            driver.Add(slot, ai, profile);
        }

        return new MissionGame(session, driver, rulesDir, aiTier);
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
