// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using System.Globalization;
using Rts.Sim.Core;

namespace Rts.Game.Ui.Sidebar;

/// <summary>
/// Pure rules of the sidebar shipment timer (M7-14, D-141): when it is shown and its text ("Shipment in 1:05 +300"). The sim decides
/// every number (<c>PlayerView.ShipmentEtaTicks</c>, <c>NextShipmentPayload</c>); this only formats them, so it is unit-tested without graphics.
/// </summary>
public static class ShipmentReadout
{
    /// <summary>The row is shown only for a player with a shipment scheduled (ETA of 0 or more) and a payload above zero.</summary>
    /// <param name="etaTicks">Ticks until the next shipment, -1 when none is scheduled.</param>
    /// <param name="payload">Credits the next shipment carries.</param>
    public static bool Shown(int etaTicks, int payload) => etaTicks >= 0 && payload > 0;

    /// <summary>Ticks as "m:ss", rounded up to whole seconds so the display never reads 0:00 before the shipment sails.</summary>
    public static string Clock(int ticks)
    {
        int perSecond = SimConstants.TicksPerSecond;
        int seconds = (Math.Max(0, ticks) + perSecond - 1) / perSecond;
        return (seconds / 60).ToString(CultureInfo.InvariantCulture) + ":" + (seconds % 60).ToString("00", CultureInfo.InvariantCulture);
    }

    /// <summary>The row text through the keyed string <see cref="UiText.ShipmentTimer"/>.</summary>
    public static string Text(int etaTicks, int payload) =>
        UiText.Get(UiText.ShipmentTimer)
            .Replace("{time}", Clock(etaTicks), StringComparison.Ordinal)
            .Replace("{payload}", payload.ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal);

    /// <summary>True once the payload has decayed at least one step (the row is drawn amber then).</summary>
    public static bool Decayed(int decayStep) => decayStep > 0;
}
