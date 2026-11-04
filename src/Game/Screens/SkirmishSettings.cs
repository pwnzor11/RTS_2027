// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Sim.Rules;

namespace Rts.Game.Screens;

/// <summary>
/// The skirmish choices from the setup screen: the map (seed 0 = the curated Rust Belt map, any other seed a generated one),
/// the AI tier, the player's colour and (M3) the faction of each side.
/// </summary>
/// <param name="MapSeed">0 for the curated map, else the seed of a generated map and of the simulation.</param>
/// <param name="AiTier">AI difficulty tier 1..5.</param>
/// <param name="PlayerColour">Index into <see cref="Rts.Game.Sprites.PlayerColours"/>.</param>
/// <param name="PlayerFaction">Faction of the human (slot 0). Authority unless the setup screen says otherwise.</param>
/// <param name="AiFaction">Faction of the AI (slot 1).</param>
public sealed record SkirmishSettings(ulong MapSeed, int AiTier, int PlayerColour,
    Faction PlayerFaction = Faction.Authority, Faction AiFaction = Faction.Authority)
{
    /// <summary>The curated map (seed 0), tier 2 (Regular), colour 0, Authority on both sides.</summary>
    public static SkirmishSettings Default { get; } = new(0, 2, 0);

    /// <summary>
    /// The value for <c>SimSetup.Factions</c> (slot 0 = human, slot 1 = AI). Null when both sides are Authority, so a default
    /// skirmish builds exactly the M2 setup.
    /// </summary>
    // ASSUMED (M3-08): an all-Authority pick maps to null (the pre-M3 setup) rather than an explicit [Authority, Authority] list.
    public IReadOnlyList<Faction>? Factions =>
        PlayerFaction == Faction.Authority && AiFaction == Faction.Authority
            ? null
            : new[] { PlayerFaction, AiFaction };
}
