// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
namespace Rts.Scripting;

/// <summary>A visible mission countdown (<c>Mission.Timer</c>, M8-03). Saved and hashed in the "mission" part (v2 tail).</summary>
/// <param name="Id">Script id.</param>
/// <param name="Text">HUD label (empty = none).</param>
/// <param name="EndTick">Sim tick at which it expires and calls <paramref name="Fn"/>.</param>
/// <param name="Fn">Global name of the expiry callback.</param>
/// <param name="Paused">True while the countdown is frozen; <paramref name="EndTick"/> then holds the ticks left.</param>
public sealed record MissionTimer(string Id, string Text, int EndTick, string Fn, bool Paused);
