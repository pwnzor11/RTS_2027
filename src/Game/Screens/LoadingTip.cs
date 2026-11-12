// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
namespace Rts.Game.Screens;

/// <summary>One loading-screen tip (M10-20).</summary>
/// <param name="Text">The advice, one or two short sentences.</param>
/// <param name="Faction">Faction id (authority, directorate, left_behind, expedition) the tip is about, or null for every game.</param>
public sealed record LoadingTip(string Text, string? Faction);
