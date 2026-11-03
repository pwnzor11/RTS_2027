// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Microsoft.Xna.Framework;

namespace Rts.Game.Input;

/// <summary>A screen area that gets first refusal on the mouse over whatever is drawn below it (the map, other panels).</summary>
public interface IUiRegion
{
    /// <summary>True when the screen point is inside the region.</summary>
    bool HitTest(Point screen);
}
