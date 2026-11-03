// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
namespace Rts.AI;

/// <summary>What one side did in a game: structures and units that ever existed for it (the free scraper counts as a unit),
/// and the lowest / highest / final funds (cash + stored credits) seen (sampled every few ticks). For reading balance, not part of the result proper.</summary>
public readonly record struct AiSideStats(int Structures, int Units, int CashMin, int CashMax, int CashEnd);

/// <summary>Outcome of one AI-vs-AI game. Winner: 0 = side A, 1 = side B, -1 = draw. ValueA / ValueB = sum of the
/// current health of everything each side still has (the tiebreak report for drawn games). StatsA / StatsB are the
/// per-side counters.</summary>
public readonly record struct AiGameResult(int Winner, int EndTick, int ValueA, int ValueB, ulong FinalHash,
    AiSideStats StatsA = default, AiSideStats StatsB = default);
