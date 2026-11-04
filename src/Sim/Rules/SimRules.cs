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

    /// <summary>Tunnel constants (game.toml [tunnels], M3). Defaults when the table is absent.</summary>
    public TunnelRules Tunnels { get; init; } = TunnelRules.Default;

    /// <summary>Radiation constants (game.toml [radiation], M3). Defaults when the table is absent.</summary>
    public RadiationRules Radiation { get; init; } = RadiationRules.Default;

    /// <summary>Stealth constants (game.toml [stealth], M3). Defaults when the table is absent.</summary>
    public StealthRules Stealth { get; init; } = StealthRules.Default;

    /// <summary>Garrison constants (game.toml [garrison], M4). Defaults when the table is absent.</summary>
    public GarrisonRules Garrison { get; init; } = GarrisonRules.Default;

    /// <summary>Quick-Set Foam constants (game.toml [foam], M4). Defaults when the table is absent.</summary>
    public FoamRules Foam { get; init; } = FoamRules.Default;

    /// <summary>Start kit per faction, indexed by <c>(int)</c><see cref="Faction"/> (game.toml [faction.&lt;name&gt;], M3).
    /// Always <see cref="RuleCounts.Factions"/> entries; ids are validated lazily by <see cref="ResolveStartKit"/>.</summary>
    public IReadOnlyList<FactionStartKit> StartKits { get; init; } =
        new[] { FactionStartKit.AuthorityDefault, FactionStartKit.DirectorateDefault };

    /// <summary>Upgrade table (upgrades.toml, M3), sorted by id; an upgrade's index is its bit in the player upgrade mask.
    /// At most <see cref="RuleCounts.MaxUpgrades"/>. Empty when the file is absent.</summary>
    public IReadOnlyList<UpgradeType> Upgrades { get; init; } = Array.Empty<UpgradeType>();

    /// <summary>Faction implied by a type id's prefix: <c>dir_</c> = Directorate, anything else (incl. <c>auth_</c>) = Authority.</summary>
    public static Faction FactionFromId(string id) =>
        id.StartsWith("dir_", StringComparison.Ordinal) ? Faction.Directorate : Faction.Authority;

    /// <summary>Index of an upgrade by id, or -1.</summary>
    public int UpgradeIndex(string id) => IndexOf(Upgrades, id, u => u.Id);

    /// <summary>
    /// Resolves a faction's start kit to indices: the conyard's building index and each start unit as (kind, index) where
    /// kind is <see cref="Core.ActorKind.Vehicle"/> or <see cref="Core.ActorKind.Infantry"/>. Throws InvalidOperationException
    /// naming the first unknown id — call it only when a player of that faction is actually set up (lazy validation).
    /// </summary>
    public (int Conyard, IReadOnlyList<(Core.ActorKind Kind, int Type)> Units) ResolveStartKit(Faction faction)
    {
        FactionStartKit kit = StartKits[(int)faction];
        int conyard = IndexOf(Buildings, kit.Conyard, b => b.Id);
        if (conyard < 0) throw new InvalidOperationException($"start kit of {faction}: unknown building '{kit.Conyard}'");
        var units = new List<(Core.ActorKind, int)>(kit.Units.Count);
        foreach (string id in kit.Units)
        {
            int v = IndexOf(Vehicles, id, x => x.Id);
            if (v >= 0) { units.Add((Core.ActorKind.Vehicle, v)); continue; }
            int i = Infantry == null ? -1 : IndexOf(Infantry, id, x => x.Id);
            if (i >= 0) { units.Add((Core.ActorKind.Infantry, i)); continue; }
            throw new InvalidOperationException($"start kit of {faction}: unknown unit '{id}'");
        }
        return (conyard, units);
    }

    /// <summary>Build time in ticks: <paramref name="buildTime"/> when positive, else derived from cost.</summary>
    public static int BuildTicks(int cost, int buildTime, EconomyRules e) =>
        buildTime > 0 ? buildTime : Math.Max(1, cost * e.BuildTicksPer100Credits / 100);

    /// <summary>Binary search of an id-sorted list; -1 when absent.</summary>
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
