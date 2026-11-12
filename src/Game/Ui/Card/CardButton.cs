// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Game.Controls;

namespace Rts.Game.Ui.Card;

/// <summary>One button on the card. <paramref name="KeyText"/> is the CURRENT bound key from <see cref="GameKeys.Active"/> (empty for Sell and Repair).</summary>
/// <param name="Kind">What it does.</param>
/// <param name="Action">The key-table action for an order button (ignored for Sell and Repair).</param>
/// <param name="Label">Button text.</param>
/// <param name="KeyText">The bound key as shown on screen, or empty.</param>
public sealed record CardButton(CardButtonKind Kind, GameAction Action, string Label, string KeyText)
{
    /// <summary>True when the button shows a state that is on (the stance every selected unit shares, M13-15): drawn as an active button.</summary>
    public bool Active { get; init; }

    /// <summary>The text drawn on the button: the label and the key.</summary>
    public string Text => KeyText.Length == 0 ? Label : Label + " " + KeyText;
}
