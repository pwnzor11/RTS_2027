// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
namespace Rts.Game.Screens;

/// <summary>
/// The skirmish choices from the setup screen. For M2 only the map seed changes the game (it seeds the sandbox
/// scenario); the AI tier and colour are stored for the production and AI packages.
/// </summary>
/// <param name="MapSeed">Simulation seed.</param>
/// <param name="AiTier">AI difficulty tier 1..5.</param>
/// <param name="PlayerColour">Index into <see cref="Rts.Game.Sprites.PlayerColours"/>.</param>
public sealed record SkirmishSettings(ulong MapSeed, int AiTier, int PlayerColour)
{
    /// <summary>The seed the sandbox has always used, tier 2 (Regular), colour 0.</summary>
    public static SkirmishSettings Default { get; } = new(Rts.Game.Sandbox.SimSnapshotSource.DefaultSeed, 2, 0);
}
