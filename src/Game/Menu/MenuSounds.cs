// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Game.Audio;

namespace Rts.Game.Menu;

/// <summary>
/// Sound hooks for the menus. They play through <see cref="IAudio.PlaySfx(string)"/> with these ids, which data/audio/sounds.toml defines
/// (M3-20, reusing existing sfx files). The menu screens play Move on selection change, Select on a chosen entry, Back on leaving
/// and Deny on a greyed-out or empty entry.
/// </summary>
public static class MenuSounds
{
    /// <summary>Selection moved.</summary>
    public const string Move = "ui_move";

    /// <summary>An entry was chosen.</summary>
    public const string Select = "ui_select";

    /// <summary>Went back.</summary>
    public const string Back = "ui_back";

    /// <summary>A greyed-out entry was chosen.</summary>
    public const string Deny = "ui_deny";

    /// <summary>Plays <paramref name="id"/> if there is an audio backend.</summary>
    public static void Play(IAudio? audio, string id) => audio?.PlaySfx(id);
}
