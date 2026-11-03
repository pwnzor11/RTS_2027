// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Microsoft.Xna.Framework;
using Rts.Sim.Core;

namespace Rts.Game;

/// <summary>
/// A unit ready to draw or pick: its view from the current snapshot plus the interpolated position in
/// zoom-1 world pixels (64 px per cell).
/// </summary>
public readonly record struct RenderUnit(UnitView View, Vector2 World);
