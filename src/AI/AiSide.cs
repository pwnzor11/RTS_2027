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

    /// <summary>The label for a file name: every character that is unsafe in a file name (<c>/ \ : * ? " &lt; &gt; |</c> and control
    /// characters) becomes '_'. Only for file names; <see cref="Label"/> is what gets printed.</summary>
    public string FileLabel => string.Concat(Label.Select(c => "/\\:*?\"<>|".Contains(c) || char.IsControl(c) ? '_' : c));
}
