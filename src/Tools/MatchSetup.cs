using Rts.Data;
using Rts.Sim;
using Rts.Sim.Core;
using Rts.Sim.Map;
using Rts.Sim.Rules;

namespace Rts.Tools;

/// <summary>Option parsing and rules/map/setup loading shared by the `match` and `replay` commands (M1-10).</summary>
internal static class MatchSetup
{
    /// <summary>Map used when --map is not given: data/maps/test_64.map.toml next to the rules directory.</summary>
    public const string DefaultMapName = "test_64.map.toml";

    /// <summary>Headless matches are always two players.</summary>
    public const int Players = 2;

    /// <summary>Parses <c>--name value</c> options and positional arguments from <paramref name="args"/> (already
    /// stripped of the command name). Only option names in <paramref name="allowed"/> are accepted.</summary>
    public static bool TryParse(string[] args, string[] allowed, out Dictionary<string, string> flags,
        out List<string> positional, out string? error)
    {
        flags = new Dictionary<string, string>(StringComparer.Ordinal);
        positional = new List<string>();
        error = null;
        for (int i = 0; i < args.Length; i++)
        {
            string arg = args[i];
            if (!arg.StartsWith("--", StringComparison.Ordinal))
            {
                positional.Add(arg);
                continue;
            }
            string name = arg[2..];
            if (Array.IndexOf(allowed, name) < 0)
            {
                error = $"unknown option '{arg}'";
                return false;
            }
            if (i + 1 >= args.Length)
            {
                error = $"option '{arg}' needs a value";
                return false;
            }
            flags[name] = args[++i];
        }
        return true;
    }

    /// <summary>Loads the rules from <paramref name="rulesArg"/>, or from the nearest data/rules above the current
    /// directory when it is null. Prints problems to stderr and returns null on failure.</summary>
    public static RuleSet? LoadRules(string? rulesArg, out string rulesDir)
    {
        rulesDir = rulesArg ?? RulesLocator.FindRulesDir(Directory.GetCurrentDirectory()) ?? "";
        if (rulesDir.Length == 0)
        {
            Console.Error.WriteLine("error: no data/rules directory found above the current directory; pass --rules <dir>");
            return null;
        }
        if (!Directory.Exists(rulesDir))
        {
            Console.Error.WriteLine($"error: rules directory not found: {rulesDir}");
            return null;
        }
        try
        {
            return RulesLoader.Load(rulesDir);
        }
        catch (RulesException ex)
        {
            foreach (RulesProblem p in ex.Problems) Console.Error.WriteLine($"{p.File}: {p.Key}: {p.Message}");
            return null;
        }
    }

    /// <summary>The default map path for a rules directory: <c>maps/test_64.map.toml</c> beside it.</summary>
    public static string DefaultMapPath(string rulesDir) =>
        Path.Combine(Path.GetDirectoryName(Path.GetFullPath(rulesDir)) ?? ".", "maps", DefaultMapName);

    /// <summary>Loads a map file. Prints the problem to stderr and returns null on failure.</summary>
    public static MapData? LoadMap(string path)
    {
        try
        {
            return MapFile.Load(path);
        }
        catch (MapFileException e)
        {
            Console.Error.WriteLine($"error: {e.Message}");
            return null;
        }
    }

    /// <summary>Simulation setup for the given seed, map, player count and rules.</summary>
    public static SimSetup MakeSetup(ulong seed, MapData map, RuleSet rules, int players) =>
        new(seed, map, players, rules.Sim, rules.RulesHash);
}
