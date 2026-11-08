// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).

namespace Rts.Game.Render;

/// <summary>Which land-linked sprite a land class gets and how often (M12-02): the share of cells in a cluster that carry one.</summary>
/// <param name="Kind">The sprite.</param>
/// <param name="Permille">Share of the cluster's cells (of 1000) that carry a sprite; every cluster gets at least one.</param>
public sealed record SpriteRule(PropKind Kind, int Permille);
