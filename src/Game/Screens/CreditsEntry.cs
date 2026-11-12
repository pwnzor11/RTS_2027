// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
namespace Rts.Game.Screens;

/// <summary>One name in the credits roll (M10-20).</summary>
/// <param name="Name">Who or what is credited.</param>
/// <param name="Credit">What for, and the licence.</param>
/// <param name="Match">Texts that find this entry's rows in docs/CREDITS.md (used by the sync test).</param>
public sealed record CreditsEntry(string Name, string Credit, IReadOnlyList<string> Match);
