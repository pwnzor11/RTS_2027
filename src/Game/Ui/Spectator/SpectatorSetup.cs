// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.AI;
using Rts.Data;
using Rts.Game.Host;
using Rts.Game.Sandbox;
using Rts.Sim.Rules;

namespace Rts.Game.Ui.Spectator;

/// <summary>
/// Builds the all-AI game the spectator watches (M4-08 debug entry; the setup screen for it is M4-11). It starts the standard
/// skirmish (<see cref="SkirmishGame.Create"/>) and puts a second AI on player 0, so both sides play and nobody is a human.
/// </summary>
public static class SpectatorSetup
{
    /// <summary>The AI difficulty tier a debug spectator game uses on both sides.</summary>
    public const int DefaultAiTier = 2;

    /// <summary>Creates the game: <paramref name="seed"/> 0 is the curated map, any other seed a generated one.</summary>
    /// <param name="rulesDir">The data/rules directory.</param>
    /// <param name="seed">Map and sim seed.</param>
    /// <param name="aiTier">AI tier 1..5 for both sides.</param>
    public static SkirmishGame Create(string rulesDir, ulong seed, int aiTier = DefaultAiTier)
    {
        SkirmishGame game = SkirmishGame.Create(rulesDir, seed, aiTier);
        DifficultyProfile profile = LoadProfile(rulesDir, aiTier);
        if (!AiRegistry.TryCreate(game.AiId, profile, out IAiPlayer? ai))
            throw new InvalidOperationException($"AI '{game.AiId}' is not registered");
        game.Ai.Add(SkirmishGame.LocalPlayer, ai, profile);
        return game;
    }

    /// <summary>
    /// Creates the all-AI game of settings that have no Human slot (M4-INT2): 2 to 4 AIs, each with its slot's tier, faction and team.
    /// </summary>
    /// <param name="rulesDir">The data/rules directory.</param>
    /// <param name="settings">Settings whose slots are all AI or Closed.</param>
    /// <exception cref="InvalidOperationException">A slot is Human, or the settings cannot start a game.</exception>
    public static SkirmishGame Create(string rulesDir, Screens.SkirmishSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        if (!settings.IsSpectator) throw new InvalidOperationException("A spectator game has no Human slot.");
        return SkirmishGame.Create(rulesDir, settings);
    }

    /// <summary>Per sim player (player order of the settings), true when its slot is an AI slot with the Commander flag.</summary>
    public static IReadOnlyList<bool> CommandersOf(Screens.SkirmishSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        return settings.PlayerSlots.Select(i => settings.Slots[i].Kind == Screens.SlotKind.Ai && settings.Slots[i].Commander).ToArray();
    }

    /// <summary>A headless spectator host over <paramref name="game"/>: AIs attached, <see cref="SimHost.Spectator"/> set, full vision.</summary>
    public static SimHost CreateHost(SkirmishGame game)
    {
        var host = new SimHost(game.Sim) { Ai = game.Ai };
        host.Spectator = true;
        host.SetViewer(-1);
        return host;
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
