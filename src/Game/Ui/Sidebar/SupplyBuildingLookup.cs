// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Sim.Rules;

namespace Rts.Game.Ui.Sidebar;

/// <summary>
/// Finds a faction's cheap supply building for the cap tooltip (SUP-4, design 13 §2.3): the building of that faction with
/// <c>supply_provided &gt; 0</c> that the player can build (not the start-kit conyard or Mother Rig, which are <c>buildable = false</c>),
/// the cheapest first (ties go to the lower index).
/// Read from the rules only, so the name is never hard-coded.
/// </summary>
public static class SupplyBuildingLookup
{
    /// <summary>Index of the faction's supply building in <see cref="SimRules.Buildings"/>, or -1 when it has none.</summary>
    /// <param name="rules">The game's rules.</param>
    /// <param name="faction">The player's faction.</param>
    public static int Find(SimRules rules, Faction faction)
    {
        string conyard = (int)faction < rules.StartKits.Count ? rules.StartKits[(int)faction].Conyard : "";
        int best = -1;
        for (int i = 0; i < rules.Buildings.Count; i++)
        {
            BuildingType b = rules.Buildings[i];
            if (b.Faction != faction || b.SupplyProvided <= 0 || !b.Buildable || b.Id == conyard) continue;
            if (best < 0 || b.Cost < rules.Buildings[best].Cost) best = i;
        }

        return best;
    }

    /// <summary>The display name of the faction's supply building (see <see cref="CameoNames.Display"/>), or null when it has none.</summary>
    public static string? NameOf(SimRules rules, Faction faction)
    {
        int i = Find(rules, faction);
        return i < 0 ? null : CameoNames.Display(rules.Buildings[i].Id);
    }
}
