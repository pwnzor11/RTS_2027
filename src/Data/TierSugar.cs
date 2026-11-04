// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Sim.Rules;

namespace Rts.Data;

/// <summary>
/// Tier sugar (schema v4, M4-01, design 08 §4): a type of tier N &gt;= 2 gets its faction's tierN provides name
/// (<see cref="FactionStartKit.Tier2Provides"/> / <see cref="FactionStartKit.Tier3Provides"/>) appended to its prerequisites, unless
/// the name is empty, already listed, or provided by the type itself (the gate structure). Lists keep their id order; the RulesHash
/// hashes the final prerequisites.
/// </summary>
internal static class TierSugar
{
    /// <summary>Rewrites the prerequisites of every tiered type in place (same index, same order).</summary>
    public static void Apply(IReadOnlyList<FactionStartKit> kits, List<VehicleType> vehicles, List<InfantryType> infantry,
        List<BuildingType> buildings, List<UpgradeType> upgrades)
    {
        for (int i = 0; i < vehicles.Count; i++)
            if (Extra(kits, vehicles[i].Faction, vehicles[i].Tier, vehicles[i].Prerequisites, null) is { } p) vehicles[i] = vehicles[i] with { Prerequisites = p };
        for (int i = 0; i < infantry.Count; i++)
            if (Extra(kits, infantry[i].Faction, infantry[i].Tier, infantry[i].Prerequisites, null) is { } p) infantry[i] = infantry[i] with { Prerequisites = p };
        for (int i = 0; i < buildings.Count; i++)
            if (Extra(kits, buildings[i].Faction, buildings[i].Tier, buildings[i].Prerequisites, buildings[i].Provides) is { } p) buildings[i] = buildings[i] with { Prerequisites = p };
        for (int i = 0; i < upgrades.Count; i++)
            if (Extra(kits, upgrades[i].Faction, upgrades[i].Tier, upgrades[i].Prerequisites, null) is { } p) upgrades[i] = upgrades[i] with { Prerequisites = p };
    }

    /// <summary>The prerequisites with the tier name appended, or null when nothing changes.</summary>
    public static IReadOnlyList<string>? Extra(IReadOnlyList<FactionStartKit> kits, Faction faction, int tier,
        IReadOnlyList<string> prerequisites, IReadOnlyList<string>? provides)
    {
        if (tier < 2 || (int)faction >= kits.Count) return null;
        FactionStartKit kit = kits[(int)faction];
        string name = tier >= 3 ? kit.Tier3Provides : kit.Tier2Provides;
        if (name.Length == 0 || Contains(prerequisites, name) || (provides is not null && Contains(provides, name))) return null;
        var list = new List<string>(prerequisites.Count + 1);
        list.AddRange(prerequisites);
        list.Add(name);
        return list;
    }

    private static bool Contains(IReadOnlyList<string> list, string name)
    {
        for (int i = 0; i < list.Count; i++) if (string.Equals(list[i], name, StringComparison.Ordinal)) return true;
        return false;
    }
}
