// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
namespace Rts.Sim.Rules;

/// <summary>
/// All type tables the sim reads. Built by Rts.Data from TOML; each list is sorted by Id (ordinal), and a
/// type's index in its list is the index stored in sim state. Lookups by id use binary search, never a
/// hash map.
/// </summary>
public sealed record SimRules(
    IReadOnlyList<VehicleType> Vehicles,
    IReadOnlyList<InfantryType> Infantry,
    IReadOnlyList<BuildingType> Buildings,
    IReadOnlyList<WeaponType> Weapons,
    IReadOnlyList<WarheadType> Warheads,
    TerrainTable Terrain,
    IReadOnlyList<EffectDef> Effects)
{
    /// <summary>Economy and production constants (game.toml [economy]).</summary>
    public EconomyRules Economy { get; init; } = EconomyRules.Default;

    /// <summary>Build time in ticks: <paramref name="buildTime"/> when positive, else derived from cost.</summary>
    public static int BuildTicks(int cost, int buildTime, EconomyRules e) =>
        buildTime > 0 ? buildTime : Math.Max(1, cost * e.BuildTicksPer100Credits / 100);

    public static int IndexOf<T>(IReadOnlyList<T> list, string id, Func<T, string> idOf)
    {
        int lo = 0, hi = list.Count - 1;
        while (lo <= hi)
        {
            int mid = (lo + hi) >> 1;
            int c = string.CompareOrdinal(idOf(list[mid]), id);
            if (c == 0) return mid;
            if (c < 0) lo = mid + 1; else hi = mid - 1;
        }
        return -1;
    }
}
