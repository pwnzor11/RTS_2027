// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Sim.Core;
using Rts.Sim.Rules;
using Rts.Sim.View;

namespace Rts.Game.Controls;

/// <summary>Right click on a wreck (M6-09): which units can salvage and where the wreck is. Pure.</summary>
public static class SalvageOrders
{
    /// <summary>Ash drained per 10 ticks by a unit type (0 = cannot salvage, or an unknown type).</summary>
    public static int SalvageRateOf(SimRules rules, ActorKind kind, int typeIndex) => kind switch
    {
        ActorKind.Vehicle when (uint)typeIndex < (uint)rules.Vehicles.Count => rules.Vehicles[typeIndex].SalvageRate,
        ActorKind.Infantry when (uint)typeIndex < (uint)rules.Infantry.Count => rules.Infantry[typeIndex].SalvageRate,
        _ => 0,
    };

    /// <summary>True for a Hauler, Tinker or any unit with salvage_rate above 0 (vehicle or infantry; the sim reads the kind from the order).</summary>
    public static bool CanSalvage(SimRules rules, ActorKind kind, int typeIndex) =>
        (kind == ActorKind.Vehicle || kind == ActorKind.Infantry) && SalvageRateOf(rules, kind, typeIndex) > 0;

    /// <summary>True when one of <paramref name="wrecks"/> lies on <paramref name="cell"/>.</summary>
    public static bool WreckAt(IReadOnlyList<WreckView> wrecks, CellCoord cell)
    {
        foreach (WreckView w in wrecks)
            if (w.Cell == cell) return true;
        return false;
    }
}
