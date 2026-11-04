// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
namespace Rts.Game.Audio;

/// <summary>What the music should suit. Tracks are found by file name in data/music (see <see cref="MusicPlayer"/>).</summary>
public enum MusicState
{
    /// <summary>Menus and screens outside a game: <c>menu*.ogg</c>.</summary>
    Menu,
    /// <summary>Fighting: <c>battle_authority*.ogg</c> or <c>battle_directorate*.ogg</c> by the player's faction.</summary>
    Battle,
    /// <summary>In a game, no fighting: <c>tension*.ogg</c>.</summary>
    Tension,
    /// <summary>The player's team won: <c>victory*.ogg</c>, played once.</summary>
    Victory,
    /// <summary>The player was defeated: <c>defeat*.ogg</c>, played once.</summary>
    Defeat,
}
