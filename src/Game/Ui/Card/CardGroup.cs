// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
namespace Rts.Game.Ui.Card;

/// <summary>A count of selected units of one type.</summary>
/// <param name="RulesId">Rules id of the type.</param>
/// <param name="Name">Display name.</param>
/// <param name="Count">How many are selected.</param>
public sealed record CardGroup(string RulesId, string Name, int Count);
