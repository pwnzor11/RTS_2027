// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Sim.Core;

namespace Rts.Sim.View;

/// <summary>A wreck as every player sees it (wrecks are public, D-102): slot in the wreck table, cell, ash value left, ticks left.</summary>
public readonly record struct WreckView(int Slot, CellCoord Cell, int Value, int TicksLeft);
