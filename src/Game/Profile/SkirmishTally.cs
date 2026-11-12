// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
namespace Rts.Game.Profile;

/// <summary>M10-19: wins and losses in skirmish games against the AI.</summary>
/// <param name="Wins">Games won.</param>
/// <param name="Losses">Games lost.</param>
public readonly record struct SkirmishTally(int Wins, int Losses)
{
    /// <summary>This tally plus one game.</summary>
    public SkirmishTally Add(bool won) => won ? this with { Wins = Math.Min(Wins + 1, PlayerProfile.MaxCount) } : this with { Losses = Math.Min(Losses + 1, PlayerProfile.MaxCount) };
}
