// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
namespace Rts.Sim.Rules;

/// <summary>VersusPercent[(int)ArmorClass] = damage percent (integer). Splash: 0 = target only,
/// 1 = 3×3 cell block with falloff (design 02 §4). Splash hits allies when HitsAllies.</summary>
public sealed record WarheadType(string Id, IReadOnlyList<int> VersusPercent, int Splash, bool HitsAllies);
