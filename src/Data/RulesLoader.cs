// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Sim.Rules;
using Tomlyn;
using Tomlyn.Model;

namespace Rts.Data;

/// <summary>
/// Loads and validates every file in data/rules. Problems are collected across all files before
/// <see cref="RulesException"/> is thrown. Order: read files, check id uniqueness, resolve <c>base</c>, parse
/// warheads, weapons, units, structures, effects and terrain (each list sorted by id before anything refers to
/// it), check name references, then build <see cref="SimRules"/> and its hash.
/// </summary>
public static class RulesLoader
{
    /// <summary>Global constants.</summary>
    public const string GameFile = "game.toml";

    /// <summary>Warhead table (with the armour versus matrix).</summary>
    public const string WarheadsFile = "warheads.toml";

    /// <summary>Weapon table.</summary>
    public const string WeaponsFile = "weapons.toml";

    /// <summary>Vehicle table.</summary>
    public const string VehiclesFile = "vehicles.toml";

    /// <summary>Infantry table.</summary>
    public const string InfantryFile = "infantry.toml";

    /// <summary>Building table.</summary>
    public const string BuildingsFile = "buildings.toml";

    /// <summary>Terrain speed and buildable table.</summary>
    public const string TerrainFile = "terrain.toml";

    /// <summary>Tech effect list.</summary>
    public const string TechsFile = "techs.toml";

    /// <summary>Upgrade table (M3). Optional: a rules set without it has no upgrades.</summary>
    public const string UpgradesFile = "upgrades.toml";

    /// <summary>Loads the rules from <paramref name="rulesDir"/>.</summary>
    /// <exception cref="RulesException">Any file is missing, unparseable or invalid.</exception>
    public static RuleSet Load(string rulesDir)
    {
        if (!Directory.Exists(rulesDir))
        {
            throw new RulesException(rulesDir, "<dir>", "rules directory not found");
        }

        return Load(file => { string path = Path.Combine(rulesDir, file); return File.Exists(path) ? File.ReadAllText(path) : null; });
    }

    /// <summary>Loads the rules from file text supplied by <paramref name="readText"/> (relative file name to text, or
    /// null when the file is missing). Used for rules embedded in replays and saves (<see cref="RulesBundle"/>).</summary>
    /// <exception cref="RulesException">Any file is missing, unparseable or invalid.</exception>
    public static RuleSet Load(Func<string, string?> readText)
    {
        var problems = new List<RulesProblem>();
        TomlTable? gameRoot = ReadFile(readText, GameFile, problems);
        TomlTable? warheadRoot = ReadFile(readText, WarheadsFile, problems);
        TomlTable? weaponRoot = ReadFile(readText, WeaponsFile, problems);
        TomlTable? vehicleRoot = ReadFile(readText, VehiclesFile, problems);
        TomlTable? infantryRoot = ReadFile(readText, InfantryFile, problems);
        TomlTable? buildingRoot = ReadFile(readText, BuildingsFile, problems);
        TomlTable? terrainRoot = ReadFile(readText, TerrainFile, problems);
        TomlTable? techRoot = ReadFile(readText, TechsFile, problems);
        TomlTable? upgradeRoot = readText(UpgradesFile) is null ? null : ReadFile(readText, UpgradesFile, problems);

        Dictionary<string, string> ids = RegisterTypeIds(problems,
            (WarheadsFile, warheadRoot), (WeaponsFile, weaponRoot), (VehiclesFile, vehicleRoot),
            (InfantryFile, infantryRoot), (BuildingsFile, buildingRoot), (UpgradesFile, upgradeRoot));
        var refs = new RulesReferences();

        List<RulesEntry> warheadEntries = Entries(warheadRoot, WarheadsFile, ids, problems);
        List<RulesEntry> weaponEntries = Entries(weaponRoot, WeaponsFile, ids, problems);
        List<RulesEntry> vehicleEntries = Entries(vehicleRoot, VehiclesFile, ids, problems);
        List<RulesEntry> infantryEntries = Entries(infantryRoot, InfantryFile, ids, problems);
        List<RulesEntry> buildingEntries = Entries(buildingRoot, BuildingsFile, ids, problems);
        List<RulesEntry> upgradeEntries = Entries(upgradeRoot, UpgradesFile, ids, problems);
        // Building ids in final (sorted) order, so vehicles can refer to a building by index (deploys_into) before buildings parse.
        List<string> buildingIds = buildingEntries.Select(e => e.Id).ToList();
        buildingIds.Sort(string.CompareOrdinal);

        List<WarheadType> warheads = ParseAll(warheadEntries, problems, WeaponRulesLoader.ParseWarhead);
        SortById(warheads, w => w.Id);
        List<WeaponType> weapons = ParseAll(weaponEntries, problems, f => WeaponRulesLoader.ParseWeapon(f, warheads));
        SortById(weapons, w => w.Id);
        List<VehicleType> vehicles = ParseAll(vehicleEntries, problems, f => UnitRulesLoader.ParseVehicle(f, weapons, buildingIds, refs));
        SortById(vehicles, v => v.Id);
        List<InfantryType> infantry = ParseAll(infantryEntries, problems, f => UnitRulesLoader.ParseInfantry(f, weapons, refs));
        SortById(infantry, i => i.Id);
        List<BuildingType> buildings = ParseAll(buildingEntries, problems, f => StructureRulesLoader.ParseBuilding(f, weapons, vehicles, refs, buildingIds));
        SortById(buildings, b => b.Id);
        List<UpgradeType> upgrades = ParseAll(upgradeEntries, problems, f => UpgradeRulesLoader.ParseUpgrade(f, refs));
        SortById(upgrades, u => u.Id);
        if (upgrades.Count > RuleCounts.MaxUpgrades)
        {
            problems.Add(new RulesProblem(UpgradesFile, "<file>", $"at most {RuleCounts.MaxUpgrades} upgrades, got {upgrades.Count}"));
        }

        List<EffectDef> effects = techRoot is null
            ? new List<EffectDef>()
            : EffectRulesLoader.Parse(techRoot, TechsFile, ids, refs, problems);
        SortById(effects, e => e.Id);

        TerrainTable? terrain = terrainRoot is null
            ? null
            : TerrainRulesLoader.Parse(terrainRoot, TerrainFile, problems);
        GameRules? game = gameRoot is null ? null : GameRulesLoader.Parse(gameRoot, GameFile, problems);
        EconomyRules? economy = gameRoot is null ? null : GameRulesLoader.ParseEconomy(gameRoot, GameFile, problems);
        TunnelRules? tunnels = gameRoot is null ? null : GameRulesLoader.ParseTunnels(gameRoot, GameFile, problems);
        RadiationRules? radiation = gameRoot is null ? null : GameRulesLoader.ParseRadiation(gameRoot, GameFile, problems);
        StealthRules? stealth = gameRoot is null ? null : GameRulesLoader.ParseStealth(gameRoot, GameFile, problems);
        IReadOnlyList<FactionStartKit>? kits = gameRoot is null ? null : GameRulesLoader.ParseStartKits(gameRoot, GameFile, problems);

        var typeIds = new HashSet<string>(
            vehicleEntries.Concat(infantryEntries).Concat(buildingEntries).Concat(upgradeEntries).Select(e => e.Id), StringComparer.Ordinal);
        var provides = new HashSet<string>(buildingEntries.SelectMany(ProvidesOf), StringComparer.Ordinal);
        refs.Validate(typeIds, provides, problems);

        if (problems.Count > 0 || game is null || terrain is null || economy is null || tunnels is null || radiation is null
            || stealth is null || kits is null)
        {
            throw new RulesException(problems);
        }

        var sim = new SimRules(vehicles, infantry, buildings, weapons, warheads, terrain, effects)
        {
            Economy = economy, Tunnels = tunnels, Radiation = radiation, Stealth = stealth, StartKits = kits, Upgrades = upgrades,
        };
        return new RuleSet(game, sim, RulesHash.Compute(game, sim));
    }

    /// <summary>Maps every type id to the file that defines it. A repeated id is reported at its second definition.</summary>
    private static Dictionary<string, string> RegisterTypeIds(List<RulesProblem> problems,
        params (string File, TomlTable? Root)[] files)
    {
        var ids = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var (file, root) in files)
        {
            if (root is null)
            {
                continue;
            }
            foreach (var kv in root)
            {
                if (kv.Value is not TomlTable)
                {
                    continue; // reported as "expected a [table]" when the file is parsed
                }
                if (ids.TryGetValue(kv.Key, out string? other))
                {
                    problems.Add(new RulesProblem(file, kv.Key, $"duplicate id, already defined in {other}"));
                }
                else
                {
                    ids[kv.Key] = file;
                }
            }
        }
        return ids;
    }

    private static List<RulesEntry> Entries(TomlTable? root, string file, Dictionary<string, string> ids,
        List<RulesProblem> problems) =>
        root is null ? new List<RulesEntry>() : RulesInheritance.Resolve(file, root, ids, problems);

    private static List<T> ParseAll<T>(IEnumerable<RulesEntry> entries, List<RulesProblem> problems,
        Func<RulesFields, T?> parse) where T : class
    {
        var list = new List<T>();
        foreach (RulesEntry entry in entries)
        {
            T? item = parse(new RulesFields(entry, problems));
            if (item is not null)
            {
                list.Add(item);
            }
        }
        return list;
    }

    private static IEnumerable<string> ProvidesOf(RulesEntry entry) =>
        entry.Values.TryGetValue("provides", out object? raw) && raw is TomlArray array
            ? array.OfType<string>()
            : Array.Empty<string>();

    private static void SortById<T>(List<T> list, Func<T, string> idOf) =>
        list.Sort((a, b) => string.CompareOrdinal(idOf(a), idOf(b)));

    private static TomlTable? ReadFile(Func<string, string?> readText, string file, List<RulesProblem> problems)
    {
        string? text = readText(file);
        if (text is null)
        {
            problems.Add(new RulesProblem(file, "<file>", "file not found"));
            return null;
        }

        try
        {
            return TomlSerializer.Deserialize<TomlTable>(text);
        }
        catch (TomlException ex)
        {
            problems.Add(new RulesProblem(file, "<parse>", ex.Message));
            return null;
        }
    }
}
