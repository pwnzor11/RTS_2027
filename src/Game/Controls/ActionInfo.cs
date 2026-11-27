// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Microsoft.Xna.Framework.Input;

namespace Rts.Game.Controls;

/// <summary>What the game knows about one rebindable action: its saved id, where it shows on the Controls page and its default key.</summary>
/// <param name="Action">The action.</param>
/// <param name="Id">The key name used in the settings file (snake_case, stable).</param>
/// <param name="Group">One of the <see cref="GameKeys"/> group names.</param>
/// <param name="Description">One short line shown beside the key.</param>
/// <param name="Default">The factory key.</param>
/// <param name="Context">Where the action is read.</param>
/// <param name="DefaultAlt">S47: true when the factory key is Alt + <paramref name="Default"/> (for example Alt + Enter).</param>
public sealed record ActionInfo(GameAction Action, string Id, string Group, string Description, Keys Default, KeyContext Context = KeyContext.Play, bool DefaultAlt = false)
{
    /// <summary>The factory key as a chord (<see cref="Default"/> with Alt when <see cref="DefaultAlt"/>).</summary>
    public KeyChord DefaultChord => new(Default, DefaultAlt);
}
