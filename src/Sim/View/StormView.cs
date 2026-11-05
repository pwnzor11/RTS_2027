// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Sim.Core;

namespace Rts.Sim.View;

/// <summary>A live ash storm as every player sees it (M6-09): slot in the storm table, centre cell, radius in cells (the storm covers the
/// Chebyshev square of cells within Radius of the centre), ticks left and the firing player.</summary>
public readonly record struct StormView(int Slot, CellCoord Centre, int Radius, int TicksLeft, byte Owner);
