using Rts.Sim.Core;
using Tomlyn;
using Tomlyn.Model;

namespace Rts.Data;

/// <summary>
/// Loads and validates data/rules/game.toml and data/rules/units.toml. Collects every problem before
/// throwing <see cref="RulesException"/>.
/// </summary>
public static class RulesLoader
{
    /// <summary>File name of the global constants.</summary>
    public const string GameFile = "game.toml";

    /// <summary>File name of the unit types.</summary>
    public const string UnitsFile = "units.toml";

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
        TomlTable? unitsRoot = ReadFile(rulesDir, UnitsFile, problems);

        GameRules? game = gameRoot is null ? null : ParseGame(gameRoot, problems);
        var units = new List<UnitTypeDef>();
        if (unitsRoot is not null)
        {
            ParseUnits(unitsRoot, units, problems);
        }

        if (problems.Count > 0 || game is null)
        {
            throw new RulesException(problems);
        }

        var sorted = units.OrderBy(u => u.Id, StringComparer.Ordinal).ToList();
        ulong hash = RulesHash.Compute(game, sorted);
        return new RuleSet(game, sorted, hash);
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

    private static void ParseUnits(TomlTable root, List<UnitTypeDef> units, List<RulesProblem> problems)
    {
        foreach (var kv in root)
        {
            if (kv.Value is not TomlTable unit)
            {
                problems.Add(new RulesProblem(UnitsFile, kv.Key, "expected a [table] for each unit"));
                continue;
            }

            foreach (var field in unit)
            {
                if (field.Key is not ("speed" or "max_health"))
                {
                    problems.Add(new RulesProblem(UnitsFile, $"{kv.Key}.{field.Key}", "unknown key"));
                }
            }

            int? speed = ReadInt(unit, UnitsFile, kv.Key, "speed", 1, int.MaxValue, problems);
            int? maxHealth = ReadInt(unit, UnitsFile, kv.Key, "max_health", 1, int.MaxValue, problems);
            if (speed is { } s && maxHealth is { } hp)
            {
                units.Add(new UnitTypeDef(kv.Key, s, hp));
            }
        }
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
