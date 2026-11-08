// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).

namespace Rts.Game.Controls;

/// <summary>Where a <see cref="GameAction"/> is read. Two actions may share a key only when their contexts never overlap.</summary>
public enum KeyContext
{
    /// <summary>A normal game (sandbox, skirmish, mission).</summary>
    Play,

    /// <summary>An AI-vs-AI spectator game.</summary>
    Spectator,

    /// <summary>Both kinds of game.</summary>
    Both,
}
