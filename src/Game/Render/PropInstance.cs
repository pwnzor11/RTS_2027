// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
namespace Rts.Game.Render;

/// <summary>One land-linked sprite on the map (M12-02): what, which variant, which cell and a small pixel offset from the cell centre.</summary>
/// <param name="Kind">The sprite kind.</param>
/// <param name="Variant">Variant number (0 small, 1 medium, 2 large for the placeholder art).</param>
/// <param name="CellX">Cell column.</param>
/// <param name="CellY">Cell row.</param>
/// <param name="OffX">Horizontal offset in zoom-1 pixels from the cell centre.</param>
/// <param name="OffY">Vertical offset in zoom-1 pixels from the cell centre.</param>
public readonly record struct PropInstance(PropKind Kind, byte Variant, short CellX, short CellY, sbyte OffX, sbyte OffY);
