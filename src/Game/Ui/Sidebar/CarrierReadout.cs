// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using System.Globalization;

namespace Rts.Game.Ui.Sidebar;

/// <summary>
/// The hangar line of one selected own carrier (M7-14): "Hangar d/s", docked aircraft over hangar slots. The sim decides both numbers
/// (<c>PlayerView.OwnCarrierHangar</c>); this only formats them.
/// </summary>
public static class CarrierReadout
{
    /// <summary>The line: docked aircraft, a slash, then the hangar slots (for example "Hangar 2/4").</summary>
    /// <param name="docked">Aircraft docked in the hangar.</param>
    /// <param name="slots">Hangar slots of the carrier.</param>
    public static string Text(int docked, int slots) =>
        "Hangar " + Math.Max(0, docked).ToString(CultureInfo.InvariantCulture) + "/" + Math.Max(0, slots).ToString(CultureInfo.InvariantCulture);
}
