// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Game.Controls;
using Rts.Game.Sandbox;

namespace Rts.Game.Ui.Card;

/// <summary>What a click on the card means (the result of <see cref="CardRouting.Click"/>).</summary>
public enum CardActionKind
{
    /// <summary>The click was not on the card: let it through.</summary>
    None,
    /// <summary>The click was on the card but on nothing that does anything: swallow it (no order, no deselect).</summary>
    Swallow,
    /// <summary>Press the bound key of an order action.</summary>
    Order,
    /// <summary>Select just this unit.</summary>
    SelectOnly,
    /// <summary>Remove this unit from the selection.</summary>
    Remove,
    /// <summary>Sell the selected building.</summary>
    Sell,
    /// <summary>Toggle repair on the selected building.</summary>
    Repair,
}

/// <summary>The outcome of a click on the card.</summary>
/// <param name="Kind">What to do.</param>
/// <param name="Action">The order action for <see cref="CardActionKind.Order"/>.</param>
/// <param name="Unit">The unit for <see cref="CardActionKind.SelectOnly"/> and <see cref="CardActionKind.Remove"/>.</param>
public readonly record struct CardAction(CardActionKind Kind, GameAction Action = default, UnitKey Unit = default)
{
    /// <summary>True when the card used up the click (anything but <see cref="CardActionKind.None"/>).</summary>
    public bool Consumed => Kind != CardActionKind.None;
}
