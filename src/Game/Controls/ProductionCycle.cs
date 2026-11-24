// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Game.Ui.Alerts;
using Rts.Sim.Core;
using Rts.Sim.Rules;
using Rts.Sim.View;

namespace Rts.Game.Controls;

/// <summary>
/// QOL-05: the own production buildings the cycling key steps through. Every finished, living factory counts (busy or idle), in slot
/// order; the selection and camera then move to the next one.
/// </summary>
public static class ProductionCycle
{
    /// <summary>Appends the own unit-producing buildings (infantry, vehicle or aircraft lanes) to <paramref name="dest"/>, in list (slot) order.</summary>
    public static void Buildings(IReadOnlyList<ViewActor> own, SimRules rules, List<ViewActor> dest)
    {
        foreach (ViewActor a in own)
        {
            if (a.Kind != ActorKind.Building || a.Ghost || a.TypeIndex < 0 || a.TypeIndex >= rules.Buildings.Count) continue;
            ProductionCategory? makes = rules.Buildings[a.TypeIndex].Produces;
            if (makes is { } c && IdleFinder.UnitLanes.Contains(c)) dest.Add(a);
        }
    }
}
