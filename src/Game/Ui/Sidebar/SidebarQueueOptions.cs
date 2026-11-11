// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Sim.Core;
using Rts.Sim.Rules;

namespace Rts.Game.Ui.Sidebar;

/// <summary>
/// S18 UXG-01: the types a production lane is holding (the finished structure waiting to be placed, and the items in progress) that are
/// no longer in <see cref="PlayerHud.Options"/> because a prerequisite was lost. The sidebar adds them back so the player can still
/// place or cancel them; otherwise the lane's ready slot is held by an invisible item for the rest of the match.
/// </summary>
public static class SidebarQueueOptions
{
    /// <summary>The held types of <paramref name="category"/> that are not in <paramref name="shown"/>, as options that can be clicked.</summary>
    public static IEnumerable<BuildOption> Missing(PlayerHud hud, SimRules rules, ProductionCategory category, IReadOnlyCollection<BuildOption> shown)
    {
        foreach (QueueView queue in hud.Queues)
        {
            if (queue.Category != category) continue;
            var seen = new HashSet<int>();
            if (queue.ReadyTypeIndex >= 0 && seen.Add(queue.ReadyTypeIndex) && !Has(shown, category, queue.ReadyTypeIndex))
                yield return Option(rules, category, queue.ReadyTypeIndex);
            foreach (QueueItemView item in queue.Items)
                if (seen.Add(item.TypeIndex) && !Has(shown, category, item.TypeIndex))
                    yield return Option(rules, category, item.TypeIndex);
        }
    }

    private static bool Has(IReadOnlyCollection<BuildOption> shown, ProductionCategory category, int type)
    {
        foreach (BuildOption o in shown)
            if (o.Category == category && o.TypeIndex == type) return true;
        return false;
    }

    private static BuildOption Option(SimRules rules, ProductionCategory category, int type) =>
        new(category, type, true, QueueBlockReason.None, CostOf(rules, category, type));

    private static int CostOf(SimRules rules, ProductionCategory category, int type) => category switch
    {
        ProductionCategory.Structure or ProductionCategory.Defence => (uint)type < (uint)rules.Buildings.Count ? rules.Buildings[type].Cost : 0,
        ProductionCategory.Infantry => (uint)type < (uint)rules.Infantry.Count ? rules.Infantry[type].Cost : 0,
        _ => (uint)type < (uint)rules.Vehicles.Count ? rules.Vehicles[type].Cost : 0,
    };
}
