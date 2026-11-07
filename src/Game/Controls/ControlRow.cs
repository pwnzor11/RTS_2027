// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
namespace Rts.Game.Controls;

/// <summary>One line of the Controls page: a rebindable action, or a fixed input (mouse, modifiers, Esc, F1) shown for reference.</summary>
/// <param name="Group">One of the <see cref="GameKeys"/> group names.</param>
/// <param name="Description">What it does, one short line.</param>
/// <param name="Action">The rebindable action; null for a fixed input.</param>
/// <param name="FixedLabel">The input as shown for a fixed row; empty for a rebindable one (its key comes from the bindings).</param>
public sealed record ControlRow(string Group, string Description, GameAction? Action, string FixedLabel = "")
{
    /// <summary>The input as shown to the player under <paramref name="bindings"/>.</summary>
    public string LabelFor(KeyBindings bindings) => Action is { } a ? KeyNames.Of(bindings.Get(a)) : FixedLabel;
}
