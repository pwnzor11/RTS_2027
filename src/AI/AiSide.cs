// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Data;
namespace Rts.AI;

/// <summary>One side of an AI-vs-AI game: an AI id and the difficulty profile it plays with.</summary>
public sealed record AiSide(string AiId, DifficultyProfile Profile)
{
    /// <summary>Label such as "skirmisher:veteran".</summary>
    public string Label => AiId + ":" + Profile.Id;
}
