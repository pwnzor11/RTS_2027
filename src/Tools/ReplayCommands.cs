using Rts.Data;
using Rts.Sim.Map;
using Rts.Sim.Replay;

namespace Rts.Tools;

/// <summary>
/// `Tools replay` subcommands (M1-10). <c>verify</c> plays a replay back headlessly and checks every recorded hash,
/// naming the first state part that diverged. <c>info</c> prints the header and record counts.
/// Exit codes: 0 ok, 1 desync or error, 2 usage.
/// </summary>
public static class ReplayCommands
{
    private const string Usage =
        "usage: replay verify <file> [--rules <dir>] [--map <path>]\n" +
        "       replay info <file>\n" +
        "  --map defaults to data/maps/test_64.map.toml; it must be the map the replay was recorded on.";

    private static readonly string[] VerifyOptions = ["rules", "map"];

    public static int Run(string[] args)
    {
        if (args.Length == 0) return UsageError("missing subcommand");
        return args[0] switch
        {
            "verify" => Verify(args[1..]),
            "info" => Info(args[1..]),
            _ => UsageError($"unknown replay command '{args[0]}'"),
        };
    }

    private static int Verify(string[] args)
    {
        if (!MatchSetup.TryParse(args, VerifyOptions, out var flags, out var positional, out string? error) || positional.Count != 1)
            return UsageError(error ?? "verify needs exactly one replay file");
        string file = positional[0];

        RuleSet? rules = MatchSetup.LoadRules(flags.TryGetValue("rules", out string? rulesArg) ? rulesArg : null, out string rulesDir);
        if (rules is null) return 1;
        string mapPath = flags.TryGetValue("map", out string? mapArg) ? mapArg : MatchSetup.DefaultMapPath(rulesDir);
        MapData? map = MatchSetup.LoadMap(mapPath);
        if (map is null) return 1;

        ReplayData header;
        try
        {
            using var fs = File.OpenRead(file);
            header = ReplayReader.Read(fs);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or InvalidDataException)
        {
            Console.Error.WriteLine($"error: cannot read replay {file}: {e.Message}");
            return 1;
        }

        if (header.RulesHash != rules.RulesHash)
        {
            Console.Error.WriteLine($"error: replay was recorded with rules 0x{header.RulesHash:x16}; the rules in {rulesDir} hash to 0x{rules.RulesHash:x16}");
            return 1;
        }
        ulong mapHash = MapHasher.Compute(map);
        if (header.MapHash != mapHash)
        {
            Console.Error.WriteLine($"error: replay was recorded on map 0x{header.MapHash:x16}; {mapPath} hashes to 0x{mapHash:x16}. Pass the map it was recorded on with --map.");
            return 1;
        }

        var setup = MatchSetup.MakeSetup(header.Seed, map, rules, header.PlayerCount);
        ReplayResult result;
        try
        {
            using var fs = File.OpenRead(file);
            result = ReplayPlayer.Run(fs, setup);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or InvalidDataException)
        {
            Console.Error.WriteLine($"error: replay {file} cannot be played back: {e.Message}");
            return 1;
        }

        if (result.FirstMismatchTick >= 0)
        {
            string where = result.FirstMismatchPart is { } part
                ? $"in part {part}"
                : "(replay has no sub-hashes)";
            Console.WriteLine($"DESYNC at tick {result.FirstMismatchTick} {where}");
            return 1;
        }
        Console.WriteLine($"OK {result.TicksRun} ticks, final hash 0x{result.FinalHash:x16}");
        return 0;
    }

    private static int Info(string[] args)
    {
        if (!MatchSetup.TryParse(args, [], out _, out var positional, out string? error) || positional.Count != 1)
            return UsageError(error ?? "info needs exactly one replay file");

        ReplayData data;
        try
        {
            using var fs = File.OpenRead(positional[0]);
            data = ReplayReader.Read(fs);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or InvalidDataException)
        {
            Console.Error.WriteLine($"error: cannot read replay {positional[0]}: {e.Message}");
            return 1;
        }

        int commands = 0, hashChecks = 0, withSubHashes = 0;
        foreach (ReplayRecord r in data.Records)
        {
            if (r.Type == ReplayWriter.TypeCommand) commands++;
            else if (r.Type == ReplayWriter.TypeHashCheck)
            {
                hashChecks++;
                if (r.SubHashes is not null) withSubHashes++;
            }
        }

        Console.WriteLine($"format version {ReplayWriter.Version}");
        Console.WriteLine($"rules hash 0x{data.RulesHash:x16}");
        Console.WriteLine($"map hash 0x{data.MapHash:x16} ({data.MapWidth}x{data.MapHeight})");
        Console.WriteLine($"seed {data.Seed} (0x{data.Seed:x16})");
        Console.WriteLine($"players {data.PlayerCount}");
        Console.WriteLine($"spawns {data.Spawns.Count}");
        Console.WriteLine($"commands {commands}");
        Console.WriteLine($"hash checks {hashChecks} (with sub-hashes {withSubHashes})");
        Console.WriteLine($"final tick {data.FinalTick}");
        return 0;
    }

    private static int UsageError(string message)
    {
        Console.Error.WriteLine($"error: {message}");
        Console.Error.WriteLine(Usage);
        return 2;
    }
}
