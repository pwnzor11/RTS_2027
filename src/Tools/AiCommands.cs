using System.Diagnostics;
using System.Globalization;
using System.Text;
using Rts.AI;
using Rts.Data;
using Rts.Sim.Map;

namespace Rts.Tools;

/// <summary>
/// `Tools ai match` and `Tools ai matrix`: headless AI-vs-AI games (M1.5-06). Every seed generates a map and is
/// played twice with the start positions swapped. Exit codes: 0 ok, 1 data error, 2 bad arguments.
/// </summary>
public static class AiCommands
{
    private const int DefaultTier = 2;
    private const string Usage =
        "usage: ai match --a <aiId>[:<tier>] --b <aiId>[:<tier>] [--seeds 8] [--ticks 6000] [--size 64] [--out file.md] [--record-dir dir]\n" +
        "       ai matrix --ai <aiId> [--tiers 1-5] [--seeds 4] [--ticks 6000] [--size 64] [--out file.md]\n" +
        "  <tier> is 1..5 or recruit|regular|veteran|elite|commissar (default regular). Seeds are 1..N.";

    private static readonly string[] MatchOptions = ["a", "b", "seeds", "ticks", "size", "out", "record-dir"];
    private static readonly string[] MatrixOptions = ["ai", "tiers", "seeds", "ticks", "size", "out"];

    /// <summary>Runs `ai ...` (args exclude the leading "ai").</summary>
    public static int Run(string[] args)
    {
        if (args.Length == 0) return UsageError("missing subcommand (match or matrix)");
        string[] rest = args[1..];
        return args[0] switch
        {
            "match" => Match(rest),
            "matrix" => Matrix(rest),
            _ => UsageError($"unknown subcommand '{args[0]}'"),
        };
    }

    // ---- ai match -----------------------------------------------------------------------------

    private static int Match(string[] args)
    {
        if (!MatchSetup.TryParse(args, MatchOptions, out var flags, out var positional, out string? error) || positional.Count > 0)
            return UsageError(error ?? $"unexpected argument '{positional[0]}'");
        if (!Common(flags, 8, out int seeds, out int ticks, out int size, out error)) return UsageError(error!);
        if (!flags.TryGetValue("a", out string? aArg) || !flags.TryGetValue("b", out string? bArg))
            return UsageError("--a and --b are required");

        if (!LoadData(out var world, out var tiers)) return 1;
        if (!ParseSide(aArg, tiers!, out AiSide? a, out error) || !ParseSide(bArg, tiers!, out AiSide? b, out error))
            return UsageError(error!);

        string? recordDir = flags.TryGetValue("record-dir", out string? rd) ? rd : null;
        var clock = Stopwatch.StartNew();
        MatchTally tally;
        try { tally = PlayPair(world!, a!, b!, seeds, ticks, size, recordDir, verbose: true); }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or ArgumentException or InvalidOperationException)
        {
            Console.Error.WriteLine($"error: {e.Message}");
            return 1;
        }
        clock.Stop();

        string table = MatchTable(a!, b!, tally, seeds, ticks, size);
        Console.WriteLine(table);
        PrintSpeed(tally.Ticks, clock);
        return WriteOut(flags, table);
    }

    private static string MatchTable(AiSide a, AiSide b, MatchTally t, int seeds, int ticks, int size)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"# AI match: {a.Label} (A) vs {b.Label} (B)");
        sb.AppendLine();
        sb.AppendLine($"{seeds} seeds x 2 (start positions swapped), {ticks} ticks per game, map size {size}.");
        sb.AppendLine();
        sb.AppendLine("| Games | A wins | B wins | Draws | Avg end tick | A win % |");
        sb.AppendLine("|---:|---:|---:|---:|---:|---:|");
        sb.AppendLine($"| {t.Games} | {t.AWins} | {t.BWins} | {t.Draws} | {t.AvgEndTick} | {Pct(t.AWins, t.Games)} |");
        if (t.Draws > 0)
            sb.AppendLine().AppendLine($"Draw tiebreak (remaining health): A ahead in {t.DrawsAAhead}, B ahead in {t.DrawsBAhead}, level in {t.Draws - t.DrawsAAhead - t.DrawsBAhead}.");
        return sb.ToString();
    }

    // ---- ai matrix ----------------------------------------------------------------------------

    private static int Matrix(string[] args)
    {
        if (!MatchSetup.TryParse(args, MatrixOptions, out var flags, out var positional, out string? error) || positional.Count > 0)
            return UsageError(error ?? $"unexpected argument '{positional[0]}'");
        if (!Common(flags, 4, out int seeds, out int ticks, out int size, out error)) return UsageError(error!);
        if (!flags.TryGetValue("ai", out string? aiId)) return UsageError("--ai is required");
        if (!AiRegistry.Contains(aiId)) return UsageError($"unknown AI '{aiId}' (known: {string.Join(", ", AiRegistry.Ids)})");
        if (!ParseTiers(flags.TryGetValue("tiers", out string? tierArg) ? tierArg : "1-5", out List<int>? list, out error))
            return UsageError(error!);

        if (!LoadData(out var world, out var profiles)) return 1;
        int n = list!.Count;
        var wins = new int[n, n];    // wins of row tier over column tier
        var draws = new int[n, n];
        var games = new int[n, n];
        long totalTicks = 0;
        var clock = Stopwatch.StartNew();
        try
        {
            for (int r = 0; r < n; r++)
                for (int c = r; c < n; c++)
                {
                    var sa = new AiSide(aiId, profiles![list[r] - 1]);
                    var sb = new AiSide(aiId, profiles[list[c] - 1]);
                    MatchTally t = PlayPair(world!, sa, sb, seeds, ticks, size, null, verbose: false);
                    totalTicks += t.Ticks;
                    games[r, c] = t.Games; draws[r, c] = t.Draws; wins[r, c] = t.AWins;
                    games[c, r] = t.Games; draws[c, r] = t.Draws; wins[c, r] = c == r ? t.AWins : t.BWins;
                }
        }
        catch (Exception e) when (e is ArgumentException or InvalidOperationException)
        {
            Console.Error.WriteLine($"error: {e.Message}");
            return 1;
        }
        clock.Stop();

        var md = new StringBuilder();
        md.AppendLine($"# AI tier matrix: {aiId}");
        md.AppendLine();
        md.AppendLine($"Row tier's win % against the column tier. {seeds} seeds x 2 sides per cell, {ticks} ticks per game, map size {size}. Draws count as non-wins.");
        md.AppendLine();
        WriteMatrix(md, "Win %", list, profiles!, (r, c) => Pct(wins[r, c], games[r, c]));
        md.AppendLine();
        WriteMatrix(md, "Draw %", list, profiles!, (r, c) => Pct(draws[r, c], games[r, c]));
        string text = md.ToString();
        Console.WriteLine(text);
        PrintSpeed(totalTicks, clock);
        return WriteOut(flags, text);
    }

    private static void WriteMatrix(StringBuilder md, string title, List<int> tiers, IReadOnlyList<DifficultyProfile> profiles,
        Func<int, int, string> cell)
    {
        md.AppendLine($"## {title}");
        md.AppendLine();
        md.Append("| row \\ col |");
        foreach (int t in tiers) md.Append($" {profiles[t - 1].Id} |");
        md.AppendLine();
        md.Append("|---|");
        foreach (int _ in tiers) md.Append("---:|");
        md.AppendLine();
        for (int r = 0; r < tiers.Count; r++)
        {
            md.Append($"| **{profiles[tiers[r] - 1].Id}** |");
            for (int c = 0; c < tiers.Count; c++) md.Append($" {cell(r, c)} |");
            md.AppendLine();
        }
    }

    // ---- playing ------------------------------------------------------------------------------

    private sealed record World(Rts.Sim.Rules.SimRules Rules, ulong RulesHash, string RulesDir);

    private sealed class MatchTally
    {
        public int Games, AWins, BWins, Draws, DrawsAAhead, DrawsBAhead;
        public long Ticks, EndTickSum;
        public long AvgEndTick => Games == 0 ? 0 : EndTickSum / Games;
    }

    private static MatchTally PlayPair(World w, AiSide a, AiSide b, int seeds, int ticks, int size, string? recordDir, bool verbose)
    {
        var tally = new MatchTally();
        if (recordDir is not null) Directory.CreateDirectory(recordDir);
        for (int s = 1; s <= seeds; s++)
        {
            MapData map = MapGenerator.Generate(size, (ulong)s);
            for (int g = 0; g < 2; g++)
            {
                bool aInSlot0 = g == 0;
                FileStream? replay = recordDir is null ? null
                    : File.Create(Path.Combine(recordDir, $"{a.Label}_vs_{b.Label}_seed{s}_g{g + 1}.rtsreplay".Replace(':', '-')));
                AiGameResult r;
                using (replay) r = AiMatchRunner.Play(map, w.Rules, w.RulesHash, (ulong)s, a, b, aInSlot0, ticks, replay,
                    BuildInfo.Version, replay is null ? null : RulesBundle.ReadDirectory(w.RulesDir));
                tally.Games++;
                tally.Ticks += r.EndTick;
                tally.EndTickSum += r.EndTick;
                if (r.Winner == 0) tally.AWins++;
                else if (r.Winner == 1) tally.BWins++;
                else
                {
                    tally.Draws++;
                    if (r.ValueA > r.ValueB) tally.DrawsAAhead++;
                    else if (r.ValueB > r.ValueA) tally.DrawsBAhead++;
                }
                if (verbose)
                {
                    string result = r.Winner == 0 ? "A wins" : r.Winner == 1 ? "B wins" : "draw";
                    Console.WriteLine($"seed {s} game {g + 1} (A in slot {(aInSlot0 ? 0 : 1)}): {result} at tick {r.EndTick} (health A {r.ValueA}, B {r.ValueB})");
                }
            }
        }
        return tally;
    }

    // ---- arguments ----------------------------------------------------------------------------

    private static bool LoadData(out World? world, out IReadOnlyList<DifficultyProfile>? tiers)
    {
        world = null; tiers = null;
        var rules = MatchSetup.LoadRules(null, out string rulesDir);
        if (rules is null) return false;
        try { tiers = DifficultyLoader.Load(DifficultyLoader.PathBesideRules(rulesDir)); }
        catch (RulesException ex)
        {
            foreach (RulesProblem p in ex.Problems) Console.Error.WriteLine($"{p.File}: {p.Key}: {p.Message}");
            return false;
        }
        world = new World(rules.Sim, rules.RulesHash, rulesDir);
        return true;
    }

    private static bool Common(Dictionary<string, string> flags, int defaultSeeds, out int seeds, out int ticks, out int size, out string? error)
    {
        error = null; ticks = 6000; size = 64;
        if (!Int(flags, "seeds", defaultSeeds, out seeds) || seeds < 1) { error = "--seeds must be a positive integer"; return false; }
        if (!Int(flags, "ticks", 6000, out ticks) || ticks < 1) { error = "--ticks must be a positive integer"; return false; }
        if (!Int(flags, "size", 64, out size) || size < MapGenerator.MinSize || size > Rts.Sim.Core.SimConstants.MaxMapCells)
        { error = $"--size must be {MapGenerator.MinSize}..{Rts.Sim.Core.SimConstants.MaxMapCells}"; return false; }
        return true;
    }

    private static bool Int(Dictionary<string, string> flags, string name, int fallback, out int value)
    {
        value = fallback;
        return !flags.TryGetValue(name, out string? text) || int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out value);
    }

    private static bool ParseSide(string text, IReadOnlyList<DifficultyProfile> tiers, out AiSide? side, out string? error)
    {
        side = null; error = null;
        int colon = text.IndexOf(':');
        string id = colon < 0 ? text : text[..colon];
        if (!AiRegistry.Contains(id)) { error = $"unknown AI '{id}' (known: {string.Join(", ", AiRegistry.Ids)})"; return false; }
        DifficultyProfile? profile = null;
        if (colon < 0) profile = tiers[DefaultTier - 1];
        else
        {
            string t = text[(colon + 1)..];
            if (int.TryParse(t, NumberStyles.None, CultureInfo.InvariantCulture, out int n) && n >= 1 && n <= tiers.Count) profile = tiers[n - 1];
            else foreach (DifficultyProfile p in tiers) if (p.Id == t) profile = p;
        }
        if (profile is null) { error = $"unknown tier in '{text}'"; return false; }
        side = new AiSide(id, profile);
        return true;
    }

    private static bool ParseTiers(string text, out List<int>? tiers, out string? error)
    {
        tiers = new List<int>(); error = null;
        foreach (string part in text.Split(',', StringSplitOptions.RemoveEmptyEntries))
        {
            int dash = part.IndexOf('-');
            int lo, hi;
            bool ok;
            if (dash < 0) { ok = int.TryParse(part, NumberStyles.None, CultureInfo.InvariantCulture, out lo); hi = lo; }
            else
            {
                ok = int.TryParse(part[..dash], NumberStyles.None, CultureInfo.InvariantCulture, out lo);
                ok &= int.TryParse(part[(dash + 1)..], NumberStyles.None, CultureInfo.InvariantCulture, out hi);
            }
            if (!ok || lo < 1 || hi > 5 || lo > hi) { error = $"bad --tiers '{text}' (use e.g. 1-5 or 2,4)"; tiers = null; return false; }
            for (int t = lo; t <= hi; t++) if (!tiers.Contains(t)) tiers.Add(t);
        }
        if (tiers.Count == 0) { error = "--tiers is empty"; tiers = null; return false; }
        tiers.Sort();
        return true;
    }

    // ---- output -------------------------------------------------------------------------------

    private static string Pct(int part, int total) =>
        total == 0 ? "-" : (part * 100 / total).ToString(CultureInfo.InvariantCulture) + "%";

    private static void PrintSpeed(long ticks, Stopwatch clock)
    {
        double seconds = Math.Max(clock.Elapsed.TotalSeconds, 1e-9);
        Console.WriteLine($"simulated {ticks} ticks in {seconds:0.00}s ({(long)(ticks / seconds)} ticks/second)");
    }

    private static int WriteOut(Dictionary<string, string> flags, string text)
    {
        if (!flags.TryGetValue("out", out string? path)) return 0;
        try
        {
            string? dir = Path.GetDirectoryName(Path.GetFullPath(path));
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            File.WriteAllText(path, text);
            Console.WriteLine($"wrote {path}");
            return 0;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            Console.Error.WriteLine($"error: {e.Message}");
            return 1;
        }
    }

    private static int UsageError(string message)
    {
        Console.Error.WriteLine($"error: {message}");
        Console.Error.WriteLine(Usage);
        return 2;
    }
}
