// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Sim.Core;
using Rts.Sim.Rules;

namespace Rts.Game.Ui.Sidebar;

/// <summary>
/// The upgrade button for a selected building that morphs into another type (the Hatch into a Bore Head, M3-18).
/// <see cref="Reason"/> is empty when enabled, otherwise the tooltip text.
/// </summary>
/// <param name="TargetType">Index of the building type it becomes (the Upgrade command's Arg).</param>
/// <param name="Caption">Button label.</param>
/// <param name="Cost">Cash charged: the price difference.</param>
/// <param name="Enabled">Whether the sim would accept the command now.</param>
/// <param name="Reason">Why it is disabled, or empty.</param>
public readonly record struct BuildingUpgradeOffer(int TargetType, string Caption, int Cost, bool Enabled, string Reason)
{
    /// <summary>
    /// The offer for a building of type <paramref name="typeIndex"/>, or null when its type has no morph target. Mirrors the sim's
    /// checks (prerequisites owned, cash for the difference); the sim stays the judge. Power is not a sim condition, so it is not one here.
    /// </summary>
    /// <param name="rules">Rules.</param>
    /// <param name="typeIndex">Rules index of the selected building's type.</param>
    /// <param name="cash">Local player's cash.</param>
    /// <param name="ownedBuildingIds">Rules ids of the local player's finished buildings.</param>
    public static BuildingUpgradeOffer? For(SimRules rules, int typeIndex, int cash, IReadOnlyCollection<string> ownedBuildingIds)
    {
        if (typeIndex < 0 || typeIndex >= rules.Buildings.Count) return null;
        BuildingType from = rules.Buildings[typeIndex];
        int target = from.MorphsInto;
        if (target < 0 || target >= rules.Buildings.Count) return null;
        BuildingType to = rules.Buildings[target];
        if (to.FootprintW != from.FootprintW || to.FootprintH != from.FootprintH) return null;
        int cost = Math.Max(0, to.Cost - from.Cost);
        string caption = $"UPGRADE {CaptionOf(to.Id)} ${cost}";
        foreach (string need in to.Prerequisites)
        {
            if (!ownedBuildingIds.Contains(need) && !string.Equals(need, from.Id, StringComparison.Ordinal))
                return new BuildingUpgradeOffer(target, caption, cost, false, $"Needs {need}");
        }
        if (cash < cost) return new BuildingUpgradeOffer(target, caption, cost, false, $"Needs ${cost - cash} more cash");
        return new BuildingUpgradeOffer(target, caption, cost, true, "");
    }

    /// <summary>The Upgrade command for this offer.</summary>
    public Command Command(byte player, EntityId building) =>
        new(0, player, 0, CommandKind.Upgrade, building, EntityId.None, default, TargetType);

    private static string CaptionOf(string id)
    {
        int u = id.IndexOf('_');
        return (u >= 0 && u + 1 < id.Length ? id[(u + 1)..] : id).Replace('_', ' ').ToUpperInvariant();
    }
}
