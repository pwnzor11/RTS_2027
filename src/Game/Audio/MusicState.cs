// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
namespace Rts.Game.Audio;

/// <summary>What the music should suit. Tracks are found by file name in data/music (see <see cref="MusicPlayer"/>).</summary>
public enum MusicState
{
    /// <summary>Menus and screens outside a game: <c>menu*.ogg</c>.</summary>
    Menu,
    /// <summary>Fighting: <c>battle_&lt;faction&gt;*.ogg</c> of the player's faction, else a generic <c>battle*.ogg</c>, else tension.</summary>
    Battle,
    /// <summary>In a game, no fighting: <c>tension_&lt;faction&gt;.ogg</c>, else any <c>tension*.ogg</c>.</summary>
    Tension,
    /// <summary>The player's team won: <c>victory*.ogg</c>, played once.</summary>
    Victory,
    /// <summary>The player was defeated: <c>defeat*.ogg</c>, played once.</summary>
    Defeat,
    /// <summary>A mission briefing: <c>briefing_&lt;faction&gt;*.ogg</c> of the mission's faction, else menu.</summary>
    Briefing,
    /// <summary>The end-credits roll: <c>credits*.ogg</c>, else menu.</summary>
    Credits,
}
