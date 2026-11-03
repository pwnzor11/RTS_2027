using Rts.Sim.Core;
using Rts.Sim.Rules;
using Tomlyn;
using Tomlyn.Model;

namespace Rts.Data;

/// <summary>
/// Loads and validates data/rules/game.toml and data/rules/vehicles.toml. Collects every problem before
/// throwing <see cref="RulesException"/>.
/// </summary>
public static class RulesLoader
{
    /// <summary>File name of the global constants.</summary>
    public const string GameFile = "game.toml";

    /// <summary>File name of the vehicle types.</summary>
    public const string VehiclesFile = "vehicles.toml";

    private const int MaxMapDimension = SimConstants.MaxMapCells;

    /// <summary>Loads the rules from <paramref name="rulesDir"/>.</summary>
    /// <exception cref="RulesException">Any file is missing, unparseable or invalid.</exception>
    public static RuleSet Load(string rulesDir)
    {
        var problems = new List<RulesProblem>();
        if (!Directory.Exists(rulesDir))
        {
            throw new RulesException(rulesDir, "<dir>", "rules directory not found");
        }

        TomlTable? gameRoot = ReadFile(rulesDir, GameFile, problems);
        TomlTable? vehiclesRoot = ReadFile(rulesDir, VehiclesFile, problems);

        GameRules? game = gameRoot is null ? null : ParseGame(gameRoot, problems);
        var vehicles = new List<VehicleType>();
        if (vehiclesRoot is not null)
        {
            ParseVehicles(vehiclesRoot, vehicles, problems);
        }

        if (problems.Count > 0 || game is null)
        {
            throw new RulesException(problems);
        }

        vehicles.Sort((x, y) => string.CompareOrdinal(x.Id, y.Id));
        var sim = new SimRules(
            vehicles,
            Array.Empty<InfantryType>(),
            Array.Empty<BuildingType>(),
            Array.Empty<WeaponType>(),
            Array.Empty<WarheadType>(),
            DefaultTerrain.Create(),
            Array.Empty<EffectDef>());
        ulong hash = RulesHash.Compute(game, sim);
        return new RuleSet(game, sim, hash);
    }

    private static TomlTable? ReadFile(string dir, string file, List<RulesProblem> problems)
    {
        string path = Path.Combine(dir, file);
        if (!File.Exists(path))
        {
            problems.Add(new RulesProblem(file, "<file>", "file not found"));
            return null;
        }

        try
        {
            return TomlSerializer.Deserialize<TomlTable>(File.ReadAllText(path));
        }
        catch (TomlException ex)
        {
            problems.Add(new RulesProblem(file, "<parse>", ex.Message));
            return null;
        }
    }

    private static GameRules? ParseGame(TomlTable root, List<RulesProblem> problems)
    {
        foreach (var kv in root)
        {
            if (kv.Key != "game")
            {
                problems.Add(new RulesProblem(GameFile, kv.Key, "unknown key"));
            }
        }

        if (!root.TryGetValue("game", out object? section) || section is null)
        {
            problems.Add(new RulesProblem(GameFile, "game", "missing required [game] table"));
            return null;
        }
        if (section is not TomlTable game)
        {
            problems.Add(new RulesProblem(GameFile, "game", "expected a [game] table"));
            return null;
        }

        foreach (var kv in game)
        {
            if (kv.Key is not ("tick_rate" or "default_map_width" or "default_map_height"))
            {
                problems.Add(new RulesProblem(GameFile, "game." + kv.Key, "unknown key"));
            }
        }

        int? tickRate = ReadInt(game, GameFile, "game", "tick_rate", 1, int.MaxValue, problems);
        if (tickRate is { } tr && tr != SimConstants.TicksPerSecond)
        {
            problems.Add(new RulesProblem(GameFile, "game.tick_rate",
                $"must be {SimConstants.TicksPerSecond} (the sim tick rate), got {tr}"));
        }
        int? width = ReadInt(game, GameFile, "game", "default_map_width", 1, MaxMapDimension, problems);
        int? height = ReadInt(game, GameFile, "game", "default_map_height", 1, MaxMapDimension, problems);

        if (tickRate is null || width is null || height is null)
        {
            return null;
        }
        return new GameRules(tickRate.Value, width.Value, height.Value);
    }

    private static readonly string[] VehicleKeys =
    [
        "speed", "max_health", "locomotion", "turn_rate", "armor", "sight", "primary_weapon",
        "has_turret", "turret_turn_rate", "crusher", "cost", "prerequisites",
    ];

    private static void ParseVehicles(TomlTable root, List<VehicleType> vehicles, List<RulesProblem> problems)
    {
        foreach (var kv in root)
        {
            if (kv.Value is not TomlTable t)
            {
                problems.Add(new RulesProblem(VehiclesFile, kv.Key, "expected a [table] for each vehicle"));
                continue;
            }

            foreach (var field in t)
            {
                if (Array.IndexOf(VehicleKeys, field.Key) < 0)
                {
                    problems.Add(new RulesProblem(VehiclesFile, $"{kv.Key}.{field.Key}", "unknown key"));
                }
            }

            string f = VehiclesFile, id = kv.Key;
            int? speed = ReadInt(t, f, id, "speed", 1, int.MaxValue, problems);
            int? maxHealth = ReadInt(t, f, id, "max_health", 1, int.MaxValue, problems);
            Locomotion? loco = ReadEnum(t, f, id, "locomotion", ParseLocomotion,
                "foot, wheeled, tracked, hover, air or burrow", problems);
            int? turnRate = ReadInt(t, f, id, "turn_rate", 0, 255, problems);
            ArmorClass? armor = ReadEnum(t, f, id, "armor", ParseArmor,
                "unarmored, light, heavy, structure or fortified", problems);
            int? sight = ReadInt(t, f, id, "sight", 0, SimConstants.MaxMapCells, problems);
            string? weapon = ReadString(t, f, id, "primary_weapon", problems);
            bool? hasTurret = ReadBool(t, f, id, "has_turret", problems);
            int? turretTurn = ReadInt(t, f, id, "turret_turn_rate", 0, 255, problems);
            bool? crusher = ReadBool(t, f, id, "crusher", problems);
            int? cost = ReadInt(t, f, id, "cost", 0, int.MaxValue, problems);
            List<string>? prereqs = ReadStringList(t, f, id, "prerequisites", problems);

            int weaponIndex = -1;
            if (!string.IsNullOrEmpty(weapon))
            {
                // Weapons are not loaded yet (M1-02); any named weapon is unresolved for now.
                problems.Add(new RulesProblem(f, $"{id}.primary_weapon", $"unknown weapon '{weapon}'"));
            }

            if (speed is { } s && maxHealth is { } hp && loco is { } lc && turnRate is { } tr && armor is { } ar
                && sight is { } si && weapon is not null && hasTurret is { } ht && turretTurn is { } tt
                && crusher is { } cr && cost is { } co && prereqs is not null)
            {
                vehicles.Add(new VehicleType(id, lc, s, tr, hp, ar, si, weaponIndex, ht, tt, cr, co, prereqs));
            }
        }
    }

    private static Locomotion? ParseLocomotion(string v) => v switch
    {
        "foot" => Locomotion.Foot,
        "wheeled" => Locomotion.Wheeled,
        "tracked" => Locomotion.Tracked,
        "hover" => Locomotion.Hover,
        "air" => Locomotion.Air,
        "burrow" => Locomotion.Burrow,
        _ => null,
    };

    private static ArmorClass? ParseArmor(string v) => v switch
    {
        "unarmored" => ArmorClass.Unarmored,
        "light" => ArmorClass.Light,
        "heavy" => ArmorClass.Heavy,
        "structure" => ArmorClass.Structure,
        "fortified" => ArmorClass.Fortified,
        _ => null,
    };

    private static T? ReadEnum<T>(TomlTable table, string file, string prefix, string key,
        Func<string, T?> parse, string allowed, List<RulesProblem> problems)
        where T : struct
    {
        string? text = ReadString(table, file, prefix, key, problems);
        if (text is null)
        {
            return null;
        }
        T? value = parse(text);
        if (value is null)
        {
            problems.Add(new RulesProblem(file, prefix + "." + key, $"must be one of {allowed}, got '{text}'"));
        }
        return value;
    }

    private static string? ReadString(TomlTable table, string file, string prefix, string key, List<RulesProblem> problems)
    {
        string dotted = prefix + "." + key;
        if (!table.TryGetValue(key, out object? raw) || raw is null)
        {
            problems.Add(new RulesProblem(file, dotted, "missing required key"));
            return null;
        }
        if (raw is not string text)
        {
            problems.Add(new RulesProblem(file, dotted, $"expected a string, got {TypeName(raw)}"));
            return null;
        }
        return text;
    }

    private static bool? ReadBool(TomlTable table, string file, string prefix, string key, List<RulesProblem> problems)
    {
        string dotted = prefix + "." + key;
        if (!table.TryGetValue(key, out object? raw) || raw is null)
        {
            problems.Add(new RulesProblem(file, dotted, "missing required key"));
            return null;
        }
        if (raw is not bool value)
        {
            problems.Add(new RulesProblem(file, dotted, $"expected a boolean, got {TypeName(raw)}"));
            return null;
        }
        return value;
    }

    private static List<string>? ReadStringList(TomlTable table, string file, string prefix, string key, List<RulesProblem> problems)
    {
        string dotted = prefix + "." + key;
        if (!table.TryGetValue(key, out object? raw) || raw is null)
        {
            problems.Add(new RulesProblem(file, dotted, "missing required key"));
            return null;
        }
        if (raw is not TomlArray array)
        {
            problems.Add(new RulesProblem(file, dotted, $"expected an array, got {TypeName(raw)}"));
            return null;
        }
        var list = new List<string>();
        foreach (object? item in array)
        {
            if (item is not string text)
            {
                problems.Add(new RulesProblem(file, dotted, "expected an array of strings"));
                return null;
            }
            list.Add(text);
        }
        return list;
    }

    /// <summary>Reads a required integer in [min, max]. Records a problem and returns null otherwise.</summary>
    private static int? ReadInt(TomlTable table, string file, string prefix, string key,
        int min, int max, List<RulesProblem> problems)
    {
        string dotted = prefix + "." + key;
        if (!table.TryGetValue(key, out object? raw) || raw is null)
        {
            problems.Add(new RulesProblem(file, dotted, "missing required key"));
            return null;
        }
        if (raw is not long value)
        {
            problems.Add(new RulesProblem(file, dotted, $"expected an integer, got {TypeName(raw)}"));
            return null;
        }
        if (value < min || value > max)
        {
            problems.Add(new RulesProblem(file, dotted, $"must be between {min} and {max}, got {value}"));
            return null;
        }
        return (int)value;
    }

    private static string TypeName(object value) => value switch
    {
        string => "a string",
        bool => "a boolean",
        double => "a float",
        TomlArray => "an array",
        TomlTable => "a table",
        _ => value.GetType().Name,
    };
}
