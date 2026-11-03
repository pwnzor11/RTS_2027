// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Microsoft.Xna.Framework;

namespace Rts.Game.Input;

/// <summary>A rectangular <see cref="IUiRegion"/>. The rectangle can be replaced as the layout changes.</summary>
public sealed class RectRegion : IUiRegion
{
    /// <summary>Creates a region covering <paramref name="bounds"/>.</summary>
    public RectRegion(Rectangle bounds) => Bounds = bounds;

    /// <summary>The covered rectangle in screen pixels.</summary>
    public Rectangle Bounds { get; set; }

    /// <inheritdoc />
    public bool HitTest(Point screen) => Bounds.Contains(screen);
}
