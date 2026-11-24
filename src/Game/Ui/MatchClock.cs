// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using System.Globalization;
using Rts.Sim.Core;

namespace Rts.Game.Ui;

/// <summary>
/// The match clock shown in the HUD (QOL-18): elapsed game time from the sim tick, "m:ss", or "h:mm:ss" past an hour. Pure, so it is
/// unit-tested. Game time, so it stops while the game is paused and follows the game speed.
/// </summary>
public static class MatchClock
{
    /// <summary>The elapsed time at <paramref name="tick"/>, e.g. "14:32" or "1:05:09".</summary>
    public static string Format(int tick)
    {
        int seconds = Math.Max(0, tick) / SimConstants.TicksPerSecond;
        int hours = seconds / 3600, minutes = seconds % 3600 / 60, secs = seconds % 60;
        return hours > 0
            ? string.Create(CultureInfo.InvariantCulture, $"{hours}:{minutes:00}:{secs:00}")
            : string.Create(CultureInfo.InvariantCulture, $"{minutes:00}:{secs:00}");
    }
}
