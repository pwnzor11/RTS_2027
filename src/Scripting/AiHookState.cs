// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
namespace Rts.Scripting;

/// <summary>
/// What the script has asked the built-in AI of one player to do (<c>AI.*</c>, D-051). The script only sets these flags, inside the
/// tick, in hashed mission state; the AI (outside Step) reads them on its next decision and never writes them. Level-triggered: the AI
/// compares <see cref="WaveSeq"/> with the last one it served.
/// </summary>
/// <param name="AttackZone">Zone name the AI should attack, or empty.</param>
/// <param name="DefendZone">Zone name the AI should defend, or empty.</param>
/// <param name="Tier">Difficulty tier 1..5 forced by the script, or 0 for no override.</param>
/// <param name="WaveSeq">Counts AI.Wave calls; 0 = none yet.</param>
/// <param name="WaveId">Id of the latest wave (a script-chosen name the AI maps to a composition), or empty.</param>
/// <param name="WaveZone">Zone the latest wave spawns at or attacks, or empty.</param>
public readonly record struct AiHookState(string AttackZone, string DefendZone, int Tier, int WaveSeq, string WaveId, string WaveZone)
{
    /// <summary>No hooks set.</summary>
    public static AiHookState None { get; } = new("", "", 0, 0, "", "");
}
