// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Sim.Rules;

namespace Rts.Data.Stamps;

/// <summary>What one stamp cell writes (M12-13). A null field leaves that layer of the map cell alone; <see cref="Set"/> false (the '.' cell)
/// leaves the whole cell alone. <see cref="Density"/> is the density byte (the sprite variant of a prop).</summary>
public readonly record struct StampCell(bool Set, LandType? Land = null, byte? Height = null, OverlayKind? Overlay = null, byte? Density = null, byte? Decor = null);
