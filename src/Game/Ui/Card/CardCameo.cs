// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Game.Sandbox;

namespace Rts.Game.Ui.Card;

/// <summary>One small unit icon in the group grid.</summary>
/// <param name="Key">The unit it selects.</param>
/// <param name="RulesId">Rules id of its type (cameo art, grouping).</param>
/// <param name="Health">Current health.</param>
/// <param name="MaxHealth">Maximum health of its type.</param>
/// <param name="Group">Control group number 1..9 the unit belongs to, 0 for none (drawn as a small number on the tile).</param>
public sealed record CardCameo(UnitKey Key, string RulesId, int Health, int MaxHealth, int Group = 0);
