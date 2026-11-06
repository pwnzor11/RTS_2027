// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Data;

namespace Rts.AI;

/// <summary>
/// M8-06 (D-051): the read-only input path from a mission script to the built-in AI. The game layer implements it over the mission host's
/// hashed <c>AiHookState</c> (src/AI cannot reference src/Scripting). <see cref="AiDriver"/> reads it once per tick per slot, outside Step.
/// </summary>
public interface IMissionAiHooks
{
    /// <summary>The hooks currently set for <paramref name="player"/> (<see cref="AiHooks.None"/> when none).</summary>
    AiHooks Read(byte player);

    /// <summary>The difficulty profile of tier <paramref name="tier"/> (1..5), or null when unknown (the slot keeps its own profile).</summary>
    DifficultyProfile? ProfileForTier(int tier);
}
