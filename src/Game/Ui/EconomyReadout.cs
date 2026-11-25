// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using System.Globalization;

namespace Rts.Game.Ui;

/// <summary>
/// The player's economy line in the HUD (QOL-04): credits harvested per minute and how many harvesters are working out of the total.
/// Pure text, so it is unit-tested; the numbers come from <c>IncomeTracker</c> and the own-actor list.
/// </summary>
public static class EconomyReadout
{
    /// <summary>The line, e.g. "Income +380/min   Harvesters 2/3".</summary>
    /// <param name="incomePerMinute">Credits harvested per minute (0 before there is a minute of samples).</param>
    /// <param name="working">Harvesters that are not idle.</param>
    /// <param name="total">Living harvesters the player owns.</param>
    public static string Line(int incomePerMinute, int working, int total) =>
        string.Create(CultureInfo.InvariantCulture, $"Income +{Math.Max(0, incomePerMinute)}/min   Harvesters {Math.Clamp(working, 0, total)}/{Math.Max(0, total)}");
}
