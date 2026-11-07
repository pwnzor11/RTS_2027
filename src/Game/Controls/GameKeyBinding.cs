// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Microsoft.Xna.Framework.Input;

namespace Rts.Game.Controls;

/// <summary>One row of the controls table: what it is called on screen and which keyboard keys the game code reads for it.</summary>
/// <param name="Group">Camera, Selection, Orders or Build &amp; System (see <see cref="GameKeys"/>).</param>
/// <param name="Label">The key (or keys) as shown to the player, for example <c>Ctrl+F5..F8</c>.</param>
/// <param name="Action">What it does, one short line.</param>
/// <param name="Handled">Every <see cref="Keys"/> value the game code tests for this row; empty for mouse-only rows.</param>
public sealed record GameKeyBinding(string Group, string Label, string Action, params Keys[] Handled);
