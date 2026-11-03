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
