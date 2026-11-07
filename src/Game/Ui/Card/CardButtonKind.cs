// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
namespace Rts.Game.Ui.Card;

/// <summary>What a button on the selection card does when clicked.</summary>
public enum CardButtonKind
{
    /// <summary>The same as pressing the bound key of <see cref="CardButton.Action"/> (an order for the selected units).</summary>
    Order,
    /// <summary>Sell the selected building.</summary>
    Sell,
    /// <summary>Toggle repair on the selected building.</summary>
    Repair,
}
