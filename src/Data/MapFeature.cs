// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Sim.Core;

namespace Rts.Data;

/// <summary>A neutral structure a map places at setup (M4, <c>[[features]]</c>): building type id and the footprint's top-left cell.</summary>
public sealed record MapFeature(string Type, CellCoord Cell);
