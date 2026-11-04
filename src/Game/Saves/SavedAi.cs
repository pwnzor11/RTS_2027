// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Sim.Rules;

namespace Rts.Game.Saves;

/// <summary>One AI slot of a saved game. The AI's own memory is not saved (the AI interface has no save/restore): it restarts fresh from the loaded sim.</summary>
/// <param name="Slot">The player slot the AI plays.</param>
/// <param name="AiId">Registry id of the AI.</param>
/// <param name="Tier">Difficulty tier 1..5.</param>
/// <param name="Faction">The slot's faction (informational; the sim save carries the real one).</param>
/// <param name="Seed">The seed the AI driver was created with.</param>
public sealed record SavedAi(byte Slot, string AiId, int Tier, Faction Faction, ulong Seed);
