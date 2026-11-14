// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Sim.Core;

namespace Rts.AI.Recruit;

/// <summary>
/// S24 (M5-12, D-241, s24-dai2): the economy commander orders. <see cref="CommanderOrderKind.ExpandTo"/> founds a second (or further) refinery base at or nearest
/// the order cell, once per order, ahead of the other build steps; <see cref="CommanderOrderKind.TechTo"/> adds the tier gates up to the ordered tier now;
/// <see cref="CommanderOrderKind.Reserve"/> keeps Amount credits untouched by units, structures and research. With no such order every value set here is
/// its default and the AI behaves exactly as before. Stateless: the one memory (which expand order is done) lives in <see cref="BuildOrderManager"/>.
/// </summary>
internal static class EconomyOrders
{
    /// <summary>Hands the economy orders of this decision to the managers (call once per decision, before the build order and production think).</summary>
    public static void Apply(IReadOnlyList<CommanderOrder> orders, TierPlanner planner, BuildOrderManager build, ProductionManager production)
    {
        int reserve = 0, tech = 0;
        CommanderOrder? expand = null;
        foreach (CommanderOrder o in orders)
            switch (o.Kind)
            {
                case CommanderOrderKind.Reserve: reserve = Math.Max(reserve, o.Amount); break;
                case CommanderOrderKind.TechTo: tech = Math.Max(tech, Math.Clamp(o.Amount, 2, 5)); break;
                case CommanderOrderKind.ExpandTo:
                    if (o.IssuedTick > build.ExpandDoneTick && (expand is null || o.IssuedTick > expand.Value.IssuedTick)) expand = o;
                    break;
            }
        build.OrderReserve = reserve;
        production.OrderReserve = reserve;
        planner.TechTier = tech;
        planner.ExpandOrderTick = expand?.IssuedTick ?? -1;
        build.ExpandCell = expand?.Cell;
        build.ExpandOrderTick = expand?.IssuedTick ?? -1;
    }
}
