// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
namespace Rts.Game.Screens;

/// <summary>
/// The skirmish choices from the setup screen: the map (seed 0 = the curated Rust Belt map, any other seed a generated one),
/// the AI tier and the player's colour.
/// </summary>
/// <param name="MapSeed">0 for the curated map, else the seed of a generated map and of the simulation.</param>
/// <param name="AiTier">AI difficulty tier 1..5.</param>
/// <param name="PlayerColour">Index into <see cref="Rts.Game.Sprites.PlayerColours"/>.</param>
public sealed record SkirmishSettings(ulong MapSeed, int AiTier, int PlayerColour)
{
    /// <summary>The curated map (seed 0), tier 2 (Regular), colour 0.</summary>
    public static SkirmishSettings Default { get; } = new(0, 2, 0);
}
