// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Sim.Core;

namespace Rts.Data.Stamps;

/// <summary>Outcome of <see cref="StampLibrary.Apply"/>. <see cref="Changed"/> are the canvas cell indexes whose value changed, ascending
/// (the editor snapshots these for undo). <see cref="Refused"/> are the cells the stamp wanted to write but may not: off the map, near a
/// start position, or a resource field kept as it was.</summary>
public sealed record StampResult(IReadOnlyList<int> Changed, IReadOnlyList<CellCoord> Refused);
