// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.AI;
using Rts.Data;
using Rts.Scripting;
using Rts.Sim;
using Rts.Sim.Map;

namespace Rts.Game.Sandbox;

/// <summary>
/// A campaign mission as the game runs it (D-061): the <see cref="MissionSession"/> (sim + Lua host) plus an <see cref="AiDriver"/> for the
/// mission's "ai" slots. Shared by the mission game screen and <c>--mission-check</c>, so both start the same way.
/// </summary>
public sealed class MissionGame
{
    /// <summary>The human player's slot.</summary>
    public const byte LocalPlayer = 0;

    private MissionGame(MissionSession session, AiDriver? ai, string rulesDir)
    {
        RulesDir = rulesDir;
        Session = session;
        Ai = ai;
    }

    /// <summary>The data/rules directory the mission was built from (its files go into a replay header).</summary>
    public string RulesDir { get; }

    /// <summary>The loaded mission.</summary>
    public MissionSession Session { get; }

    /// <summary>The AI driver for the mission's AI slots (give it to <see cref="Rts.Game.Host.SimHost.Ai"/>), or null if there are none.</summary>
    public AiDriver? Ai { get; }

    /// <summary>The simulation.</summary>
    public Simulation Sim => Session.Sim;

    /// <summary>The map.</summary>
    public MapData Map => Session.Map;

    /// <summary>The script host (objectives, messages, errors).</summary>
    public MissionHost Host => Session.Host;

    /// <summary>Builds the mission <paramref name="missionId"/> from the data folder beside <paramref name="rulesDir"/>.</summary>
    /// <param name="rulesDir">The data/rules directory.</param>
    /// <param name="missionId">Folder name under data/missions.</param>
    /// <param name="aiTier">AI difficulty tier 1..5 for the AI slots.</param>
    public static MissionGame Create(string rulesDir, string missionId, int aiTier = 1)
    {
        string dataDir = Path.GetFullPath(Path.Combine(rulesDir, ".."));
        MissionSession session = MissionSession.Create(dataDir, missionId);
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

        return new MissionGame(session, driver, rulesDir);
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
