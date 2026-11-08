// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
namespace Rts.Game.Ui.Card;

/// <summary>One "Label value" stat of a unit.</summary>
/// <param name="Label">For example "Damage".</param>
/// <param name="Value">For example "6".</param>
public sealed record CardStat(string Label, string Value)
{
    /// <summary>"Damage 6".</summary>
    public string Text => Label + " " + Value;
}
