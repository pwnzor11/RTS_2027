using Rts.Data;
using Rts.Sim;
using Rts.Sim.Core;

namespace Rts.Tools;

/// <summary>
/// Command-line entry points: <c>validate</c>, <c>run</c> and <c>help</c>.
/// Exit codes: 0 ok, 1 usage or rules error, 2 sim not implemented yet.
/// </summary>
public static class Cli
{
    private const int ExitOk = 0;
    private const int ExitError = 1;
    private const int ExitNotImplemented = 2;
    private const int Players = 2;

    private const string Usage =
        """
        Usage:
          Tools validate [--rules <dir>]
          Tools run --ticks <n> --seed <s> [--rules <dir>] [--units <k>]
          Tools help

        --rules defaults to the nearest data/rules found walking up from the current directory.
        --units defaults to 4 per player.
        Exit codes: 0 ok, 1 usage or rules error, 2 sim not implemented yet.
        """;

    /// <summary>Runs one CLI invocation and returns its exit code.</summary>
    public static int Run(string[] args)
    {
        if (args.Length == 0 || args[0] is "help" or "--help" or "-h")
        {
            Console.WriteLine(Usage);
            return ExitOk;
        }

        switch (args[0])
        {
            case "validate":
                return Validate(args);
            case "run":
                return Run(args, sim: true);
            default:
                Console.Error.WriteLine($"error: unknown command '{args[0]}'");
                Console.Error.WriteLine(Usage);
                return ExitError;
        }
    }

    private static int Validate(string[] args)
    {
        if (!TryParseFlags(args, ["rules"], out var flags, out string? error))
        {
            return UsageError(error);
        }
        if (!TryResolveRulesDir(flags, out string? dir, out error))
        {
            return UsageError(error);
        }

        try
        {
            RuleSet rules = RulesLoader.Load(dir!);
            Console.WriteLine($"OK rules hash 0x{rules.RulesHash:x16}");
            return ExitOk;
        }
        catch (RulesException ex)
        {
            foreach (RulesProblem p in ex.Problems)
            {
                Console.Error.WriteLine($"{p.File}: {p.Key}: {p.Message}");
            }
            return ExitError;
        }
    }

    private static int Run(string[] args, bool sim)
    {
        if (!TryParseFlags(args, ["ticks", "seed", "rules", "units"], out var flags, out string? error))
        {
            return UsageError(error);
        }
        if (!flags.TryGetValue("ticks", out string? ticksText) || !int.TryParse(ticksText, out int ticks) || ticks < 0)
        {
            return UsageError("--ticks <n> is required and must be a non-negative integer");
        }
        if (!flags.TryGetValue("seed", out string? seedText) || !ulong.TryParse(seedText, out ulong seed))
        {
            return UsageError("--seed <s> is required and must be a non-negative integer");
        }
        int unitsPerPlayer = 4;
        if (flags.TryGetValue("units", out string? unitsText)
            && (!int.TryParse(unitsText, out unitsPerPlayer) || unitsPerPlayer < 0))
        {
            return UsageError("--units <k> must be a non-negative integer");
        }
        if (!TryResolveRulesDir(flags, out string? dir, out error))
        {
            return UsageError(error);
        }

        RuleSet rules;
        try
        {
            rules = RulesLoader.Load(dir!);
        }
        catch (RulesException ex)
        {
            foreach (RulesProblem p in ex.Problems)
            {
                Console.Error.WriteLine($"{p.File}: {p.Key}: {p.Message}");
            }
            return ExitError;
        }

        int width = rules.Game.DefaultMapWidth;
        int height = rules.Game.DefaultMapHeight;
        if (unitsPerPlayer > 0 && rules.Units.Count == 0)
        {
            return UsageError("rules define no unit types; cannot spawn units");
        }
        if (unitsPerPlayer > width - 2 || height < 3)
        {
            return UsageError($"--units {unitsPerPlayer} does not fit a {width}x{height} map (max {Math.Max(0, width - 2)})");
        }

        var setup = new SimSetup(seed, width, height, Players, rules.Units, rules.RulesHash);
        try
        {
            var simulation = new Simulation(setup);
            var commands = new List<Command>();
            ushort sequence = 0;
            for (byte player = 0; player < Players; player++)
            {
                for (int i = 0; i < unitsPerPlayer; i++)
                {
                    // Player 0 starts on the top edge, player 1 on the bottom edge; units fill left to right.
                    var spawnCell = new CellCoord(1 + i, player == 0 ? 1 : height - 2);
                    EntityId id = simulation.SpawnUnit(player, 0, spawnCell);

                    var target = new CellCoord(width - 2 - i, height / 2);
                    commands.Add(new Command(ExecTick: 0, Player: player, Sequence: sequence++,
                        Kind: CommandKind.Move, Subject: id, Target: EntityId.None, Cell: target, Arg: 0));
                }
            }

            for (int t = 0; t < ticks; t++)
            {
                simulation.Step(t == 0 ? commands : Array.Empty<Command>());
            }

            Console.WriteLine($"tick {simulation.Tick} hash 0x{simulation.ComputeStateHash():x16}");
            return ExitOk;
        }
        catch (NotImplementedException)
        {
            Console.WriteLine("sim not implemented yet");
            return ExitNotImplemented;
        }
    }

    private static bool TryResolveRulesDir(Dictionary<string, string> flags, out string? dir, out string? error)
    {
        error = null;
        if (flags.TryGetValue("rules", out dir))
        {
            if (!Directory.Exists(dir))
            {
                error = $"rules directory not found: {dir}";
                return false;
            }
            return true;
        }

        dir = RulesLocator.FindRulesDir(Directory.GetCurrentDirectory());
        if (dir is null)
        {
            error = "no data/rules directory found above the current directory; pass --rules <dir>";
            return false;
        }
        return true;
    }

    /// <summary>Parses <c>--name value</c> pairs. Only names in <paramref name="allowed"/> are accepted.</summary>
    private static bool TryParseFlags(string[] args, string[] allowed, out Dictionary<string, string> flags, out string? error)
    {
        flags = new Dictionary<string, string>(StringComparer.Ordinal);
        error = null;
        for (int i = 1; i < args.Length; i++)
        {
            string arg = args[i];
            if (!arg.StartsWith("--", StringComparison.Ordinal) || arg.Length <= 2)
            {
                error = $"unexpected argument '{arg}'";
                return false;
            }
            string name = arg[2..];
            if (Array.IndexOf(allowed, name) < 0)
            {
                error = $"unknown option '--{name}'";
                return false;
            }
            if (i + 1 >= args.Length)
            {
                error = $"option '--{name}' needs a value";
                return false;
            }
            flags[name] = args[++i];
        }
        return true;
    }

    private static int UsageError(string? message)
    {
        Console.Error.WriteLine($"error: {message}");
        Console.Error.WriteLine(Usage);
        return ExitError;
    }
}
