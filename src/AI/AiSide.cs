// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Data;
using Rts.Sim.Rules;
namespace Rts.AI;

/// <summary>One side of an AI-vs-AI game: an AI id, the difficulty profile it plays with and the faction it plays (default Authority).</summary>
public sealed record AiSide(string AiId, DifficultyProfile Profile, Faction Faction = Faction.Authority)
{
    /// <summary>Label such as "skirmisher:veteran".</summary>
    public string Label => AiId + ":" + Profile.Id + (Faction == Faction.Authority ? "" : "/" + Faction.ToString().ToLowerInvariant());
}
