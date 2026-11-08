// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).

namespace Rts.Game.Render;

/// <summary>
/// The decor on/off switch (M12-03): when off, no decals and no land-linked sprites are drawn, which is the old bare look. The in-game key is
/// <c>GameAction.DecorToggle</c>; the environment variable <c>RTS_DECOR=0</c> starts the game with decor off.
/// </summary>
public static class DecorSettings
{
    /// <summary>True when decor is drawn. Render only, never saved, never read by the simulation.</summary>
    public static bool Enabled { get; set; } = Environment.GetEnvironmentVariable("RTS_DECOR") != "0";
}
