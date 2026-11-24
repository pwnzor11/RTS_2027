// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using System.Globalization;

namespace Rts.Game.Controls;

/// <summary>Short on-screen lines of the A43 quality-of-life items (pause plan, repair all, sell several). Plain text, in the game's terse style.</summary>
public static class QolText
{
    /// <summary>HUD line under PAUSED while orders wait: how many units have one.</summary>
    public static string OrdersHeld(int count) => string.Create(CultureInfo.InvariantCulture, $"Orders held: {count}");

    /// <summary>Toast after repair-all started repairs.</summary>
    public static string RepairsStarted(int count) => string.Create(CultureInfo.InvariantCulture, $"Repairs started: {count}");

    /// <summary>Toast when repair-all found nothing to do.</summary>
    public const string NothingToRepair = "Nothing needs repair";

    /// <summary>Toast after the first press of the sell key: how many buildings the second press sells.</summary>
    public static string SellPrompt(int count) => string.Create(CultureInfo.InvariantCulture, $"Sell {count}? Press again");

    /// <summary>Toast when the sell key finds no building marked or selected.</summary>
    public const string NothingToSell = "No building marked";
}
