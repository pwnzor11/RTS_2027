// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Sim.Rules;

namespace Rts.Game.Saves;

/// <summary>One AI slot of a saved game. Since header version 3 the AI's own memory travels with it as <see cref="State"/> (M3-21); a save
/// without one (older saves, or an AI that keeps nothing) restarts that AI fresh from the loaded sim.</summary>
/// <param name="Slot">The player slot the AI plays.</param>
/// <param name="AiId">Registry id of the AI.</param>
/// <param name="Tier">Difficulty tier 1..5.</param>
/// <param name="Faction">The slot's faction (informational; the sim save carries the real one).</param>
/// <param name="Seed">The seed the AI driver was created with.</param>
public sealed record SavedAi(byte Slot, string AiId, int Tier, Faction Faction, ulong Seed)
{
    /// <summary>The AI's saved memory (<c>AiDriver.CaptureState</c>), or null for a fresh start.</summary>
    public byte[]? State { get; init; }
}
