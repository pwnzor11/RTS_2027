using System.Globalization;
using Rts.Sim.Core;

namespace Rts.Game.Ui;

/// <summary>
/// The names the debug console may use: vehicle and infantry type ids in rules order (a type's index is its position in
/// its list), and the number of players in the sandbox.
/// </summary>
/// <param name="VehicleIds">Vehicle type ids, index = vehicle type index.</param>
/// <param name="InfantryIds">Infantry type ids, index = infantry type index.</param>
/// <param name="PlayerCount">Number of player slots; valid player numbers are 0 to PlayerCount - 1.</param>
public sealed record DebugCatalog(IReadOnlyList<string> VehicleIds, IReadOnlyList<string> InfantryIds, int PlayerCount);

/// <summary>A parsed debug console command. The console executes it; the parser does no game work.</summary>
public abstract record DebugCommand;

/// <summary>`help`: list the commands and the type ids.</summary>
public sealed record HelpCommand : DebugCommand;

/// <summary>`spawn vehicle|infantry &lt;typeId&gt; [count] [player]`: spawn at the cell under the mouse.</summary>
/// <param name="Kind">Vehicle or Infantry.</param>
/// <param name="TypeIndex">Index of the type in its rules list (the DebugOp Arg2).</param>
/// <param name="TypeId">The type id as the player typed it, resolved to <see cref="TypeIndex"/>.</param>
/// <param name="Count">Number of units, 1 to <see cref="DebugCommandParser.MaxCount"/>.</param>
/// <param name="Player">Owner of the spawned units.</param>
public sealed record SpawnCommand(ActorKind Kind, int TypeIndex, string TypeId, int Count, byte Player) : DebugCommand;

/// <summary>`cash &lt;amount&gt; [player]`: set a player's cash.</summary>
/// <param name="Amount">New cash, 0 or more.</param>
/// <param name="Player">The player whose cash changes.</param>
public sealed record CashCommand(int Amount, byte Player) : DebugCommand;

/// <summary>`kill`: kill every selected unit.</summary>
public sealed record KillCommand : DebugCommand;

/// <summary>`reveal`: toggle the omniscient view (front end only).</summary>
public sealed record RevealCommand : DebugCommand;

/// <summary>`speed 1|2|4|8`: run that many ticks per tick interval (front end only).</summary>
/// <param name="Multiplier">1, 2, 4 or 8.</param>
public sealed record SpeedCommand(int Multiplier) : DebugCommand;

/// <summary>`pause`: toggle pause.</summary>
public sealed record PauseCommand : DebugCommand;

/// <summary>`step [n]`: pause and advance n ticks.</summary>
/// <param name="Ticks">Ticks to run, 1 to <see cref="DebugCommandParser.MaxStepTicks"/>.</param>
public sealed record StepCommand(int Ticks) : DebugCommand;

/// <summary>The outcome of <see cref="DebugCommandParser.Parse"/>: a command, or an error line for the console.</summary>
/// <param name="Command">The parsed command, or null when the line was rejected.</param>
/// <param name="Error">Why the line was rejected, or null when it was accepted.</param>
public readonly record struct DebugParseResult(DebugCommand? Command, string? Error)
{
    /// <summary>True when the line parsed.</summary>
    public bool Succeeded => Command is not null;

    /// <summary>An accepted command.</summary>
    public static DebugParseResult Accept(DebugCommand command) => new(command, null);

    /// <summary>A rejected line with the message to show.</summary>
    public static DebugParseResult Reject(string error) => new(null, error);
}

/// <summary>
/// Turns one line typed into the debug console into a <see cref="DebugCommand"/>, or into an error message. Pure: it
/// reads only the <see cref="DebugCatalog"/>, so the test project can check it without a window. Words are split on
/// white space; verbs, kinds and type ids match ignoring case.
/// </summary>
public static class DebugCommandParser
{
    /// <summary>Largest count a spawn command may ask for in one line.</summary>
    public const int MaxCount = 50;

    /// <summary>Largest number of ticks a step command may run.</summary>
    public const int MaxStepTicks = 1000;

    private static readonly int[] SpeedSteps = { 1, 2, 4, 8 };
    private const string SpawnUsage = "usage: spawn vehicle|infantry <typeId> [count] [player]";

    /// <summary>Parses one console line.</summary>
    /// <param name="line">The text typed, without the Enter key.</param>
    /// <param name="catalog">The type ids and player count that the commands may name.</param>
    public static DebugParseResult Parse(string line, DebugCatalog catalog)
    {
        string[] words = line.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        if (words.Length == 0) return DebugParseResult.Reject("empty line; type help for the commands");

        switch (words[0].ToLowerInvariant())
        {
            case "help":
                return words.Length == 1 ? DebugParseResult.Accept(new HelpCommand()) : DebugParseResult.Reject("usage: help");
            case "kill":
                return words.Length == 1 ? DebugParseResult.Accept(new KillCommand()) : DebugParseResult.Reject("usage: kill (kills the selected units)");
            case "reveal":
                return words.Length == 1 ? DebugParseResult.Accept(new RevealCommand()) : DebugParseResult.Reject("usage: reveal");
            case "pause":
                return words.Length == 1 ? DebugParseResult.Accept(new PauseCommand()) : DebugParseResult.Reject("usage: pause");
            case "spawn":
                return ParseSpawn(words, catalog);
            case "cash":
                return ParseCash(words, catalog);
            case "speed":
                return ParseSpeed(words);
            case "step":
                return ParseStep(words);
            default:
                return DebugParseResult.Reject($"unknown command '{words[0]}'; type help for the commands");
        }
    }

    /// <summary>The lines `help` prints: the commands, then the vehicle and infantry type ids, wrapped to fit.</summary>
    /// <param name="catalog">The type ids to list.</param>
    public static IReadOnlyList<string> HelpLines(DebugCatalog catalog)
    {
        var lines = new List<string>
        {
            "spawn vehicle|infantry <typeId> [count] [player]   at the mouse cell",
            "cash <amount> [player]   kill (selected units)   reveal   speed 1|2|4|8",
            "pause   step [n]   help   Up/Down history   Esc or F1 closes this",
        };
        lines.AddRange(Wrap("vehicles: ", catalog.VehicleIds));
        lines.AddRange(Wrap("infantry: ", catalog.InfantryIds));
        return lines;
    }

    private static DebugParseResult ParseSpawn(string[] words, DebugCatalog catalog)
    {
        if (words.Length < 3 || words.Length > 5) return DebugParseResult.Reject(SpawnUsage);

        ActorKind kind;
        IReadOnlyList<string> ids;
        switch (words[1].ToLowerInvariant())
        {
            case "vehicle":
                kind = ActorKind.Vehicle;
                ids = catalog.VehicleIds;
                break;
            case "infantry":
                kind = ActorKind.Infantry;
                ids = catalog.InfantryIds;
                break;
            default:
                return DebugParseResult.Reject("spawn kind must be vehicle or infantry");
        }

        string typeId = words[2];
        int index = IndexOfId(ids, typeId);
        if (index < 0)
        {
            return DebugParseResult.Reject(
                $"unknown {words[1].ToLowerInvariant()} type '{typeId}'; valid: {string.Join(", ", ids)}");
        }

        int count = 1;
        if (words.Length >= 4 && Bounded(words[3], "count", 1, MaxCount, out count) is { } countError)
        {
            return DebugParseResult.Reject(countError);
        }

        byte player = 0;
        if (words.Length == 5 && PlayerOf(words[4], catalog, out player) is { } playerError)
        {
            return DebugParseResult.Reject(playerError);
        }

        return DebugParseResult.Accept(new SpawnCommand(kind, index, ids[index], count, player));
    }

    private static DebugParseResult ParseCash(string[] words, DebugCatalog catalog)
    {
        if (words.Length is < 2 or > 3) return DebugParseResult.Reject("usage: cash <amount> [player]");

        if (Bounded(words[1], "amount", 0, int.MaxValue, out int amount) is { } amountError)
        {
            return DebugParseResult.Reject(amountError);
        }

        byte player = 0;
        if (words.Length == 3 && PlayerOf(words[2], catalog, out player) is { } playerError)
        {
            return DebugParseResult.Reject(playerError);
        }

        return DebugParseResult.Accept(new CashCommand(amount, player));
    }

    private static DebugParseResult ParseSpeed(string[] words)
    {
        if (words.Length != 2 || !TryWhole(words[1], out int multiplier))
        {
            return DebugParseResult.Reject("usage: speed 1|2|4|8");
        }

        return Array.IndexOf(SpeedSteps, multiplier) >= 0
            ? DebugParseResult.Accept(new SpeedCommand(multiplier))
            : DebugParseResult.Reject("speed must be 1, 2, 4 or 8");
    }

    private static DebugParseResult ParseStep(string[] words)
    {
        if (words.Length > 2) return DebugParseResult.Reject("usage: step [n]");

        int ticks = 1;
        if (words.Length == 2 && Bounded(words[1], "step count", 1, MaxStepTicks, out ticks) is { } error)
        {
            return DebugParseResult.Reject(error);
        }

        return DebugParseResult.Accept(new StepCommand(ticks));
    }

    // Returns the index of the id in the list (case-insensitive), or -1. Linear on purpose: the rules lists are not
    // guaranteed to be sorted, and SimRules.IndexOf needs a sorted list.
    private static int IndexOfId(IReadOnlyList<string> ids, string id)
    {
        for (int i = 0; i < ids.Count; i++)
        {
            if (string.Equals(ids[i], id, StringComparison.OrdinalIgnoreCase)) return i;
        }

        return -1;
    }

    private static string? PlayerOf(string text, DebugCatalog catalog, out byte player)
    {
        string? error = Bounded(text, "player", 0, catalog.PlayerCount - 1, out int value);
        player = (byte)value; // only meaningful when error is null; the bounds check keeps it in 0..PlayerCount-1
        return error;
    }

    // Returns an error message, or null with the value set. A negative number parses, so the range message explains it.
    private static string? Bounded(string text, string name, int min, int max, out int value)
    {
        if (!TryWhole(text, out value)) return $"{name} '{text}' is not a whole number";
        if (value < min || value > max) return $"{name} must be from {min} to {max}";
        return null;
    }

    private static bool TryWhole(string text, out int value) =>
        int.TryParse(text, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out value);

    // Greedy word wrap of a label and its ids into lines of at most 100 characters.
    private static IEnumerable<string> Wrap(string label, IReadOnlyList<string> ids)
    {
        const int Width = 100;
        string current = label;
        bool any = false;
        foreach (string id in ids)
        {
            string piece = any ? ", " + id : id;
            if (current.Length + piece.Length > Width && any)
            {
                yield return current;
                current = "          " + id; // continuation lines are indented under the label
            }
            else
            {
                current += piece;
            }

            any = true;
        }

        yield return current;
    }
}
