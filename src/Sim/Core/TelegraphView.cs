// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
namespace Rts.Sim.Core;

/// <summary>
/// A surfacing telegraph as a player may see it (design 05 §1, D-075): Cell (for an entrance, its top-left cell), Owner,
/// TicksLeft until the unit appears / the entrance is finished, and Kind. Listed for the owner and allies always, for anyone
/// else only while the cell is in their team's vision or inside their Seismograph radius.
/// </summary>
public readonly record struct TelegraphView(CellCoord Cell, byte Owner, int TicksLeft, TelegraphKind Kind);
