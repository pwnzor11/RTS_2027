// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Data;

namespace Rts.AI;

/// <summary>M8-06: an AI that reads mission script hooks. <see cref="AiDriver"/> calls it right before each Think when a hook source is set.</summary>
public interface IHookableAi
{
    /// <summary>Gives the AI the hooks and the profile in force for its next decision (the slot's own profile when no tier is forced).</summary>
    void ApplyHooks(AiHooks hooks, DifficultyProfile profile);
}
