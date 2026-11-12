// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using System.Globalization;
using Rts.Data;
using Rts.Net;

namespace Rts.Tools;

/// <summary>
/// `Tools lan soak` (M9-03): plays a headless LAN match (host plus clients, every slot a host-run AI) through the lockstep layer and
/// reports how many state hashes were compared and how many disagreed (0 expected). Exit codes: 0 in sync, 1 desync, stall or data
/// error, 2 bad arguments or transport not available.
/// </summary>
public static class LanCommands
{
    private const string Usage =
        "usage: lan soak [--players 2..4] [--ticks 4000] [--latency-ms 0] [--jitter-ms 40] [--loss 0] [--seed 1] [--delay 2|3|4] [--tier 1..5] [--udp]\n" +
        "         Runs one host and players-1 clients over the in-process loopback network (latency and jitter in ms, loss in % of\n" +
        "         unreliable messages). --udp will use real sockets on 127.0.0.1 once the UDP transport is in.";

    private static readonly string[] SoakOptions = ["players", "ticks", "latency-ms", "jitter-ms", "loss", "seed", "delay", "tier", "udp"];

    /// <summary>Runs `lan ...` (args exclude the leading "lan").</summary>
    public static int Run(string[] args)
    {
        if (args.Length == 0) return UsageError("missing subcommand (soak)");
        return args[0] switch
        {
            "soak" => Soak(args[1..]),
            _ => UsageError($"unknown subcommand '{args[0]}'"),
        };
    }

    private static int Soak(string[] args)
    {
        // --udp is a bare switch; MatchSetup.TryParse wants a value after every option, so take it out first
        bool udp = args.Contains("--udp");
        string[] rest = args.Where(a => a != "--udp").ToArray();
        if (!MatchSetup.TryParse(rest, SoakOptions, out var flags, out var positional, out string? error) || positional.Count > 0)
            return UsageError(error ?? $"unexpected argument '{positional[0]}'");
        if (!Int(flags, "players", 4, out int players) || players < 2 || players > 4) return UsageError("--players must be 2..4");
        if (!Int(flags, "ticks", 4000, out int ticks) || ticks < 1) return UsageError("--ticks must be a positive integer");
        if (!Int(flags, "latency-ms", 0, out int latency) || latency > 10_000) return UsageError("--latency-ms must be 0..10000");
        if (!Int(flags, "jitter-ms", 40, out int jitter) || jitter > 10_000) return UsageError("--jitter-ms must be 0..10000");
        if (!Int(flags, "loss", 0, out int loss) || loss > 100) return UsageError("--loss must be 0..100");
        if (!Int(flags, "seed", 1, out int seed) || seed < 1) return UsageError("--seed must be a positive integer");
        if (!Int(flags, "delay", NetProtocol.DefaultDelay, out int delay) || !NetProtocol.DelayChoices.Contains(delay))
            return UsageError("--delay must be 2, 3 or 4");
        if (!Int(flags, "tier", 3, out int tier) || tier < 1 || tier > 5) return UsageError("--tier must be 1..5");

        var rules = MatchSetup.LoadRules(null, out string rulesDir);
        if (rules is null) return 1;
        IReadOnlyList<DifficultyProfile> tiers;
        try { tiers = DifficultyLoader.Load(DifficultyLoader.PathBesideRules(rulesDir)); }
        catch (RulesException ex)
        {
            foreach (RulesProblem p in ex.Problems) Console.Error.WriteLine($"{p.File}: {p.Key}: {p.Message}");
            return 1;
        }

        var options = new LanSoakOptions(players, ticks, latency, jitter, loss, (ulong)seed, delay, tier, udp);
        Console.WriteLine($"LAN soak: {players} players (host + {players - 1} clients, all AI tier {tier}), {ticks} ticks, delay {delay}, " +
            $"latency {latency} ms + jitter 0..{jitter} ms, loss {loss} %, seed {seed}, transport {(udp ? "udp" : "loopback")}");
        LanSoakResult? r;
        try { r = LanSoak.Run(options, rules.Sim, rules.RulesHash, tiers); }
        catch (InvalidOperationException e)
        {
            Console.Error.WriteLine($"error: {e.Message}");
            return 1;
        }
        if (r is null)
        {
            Console.Error.WriteLine("error: the UDP transport is not available yet; run without --udp");
            return 2;
        }

        Console.WriteLine($"hashes compared {r.HashesCompared}, desyncs {r.Desyncs}, unchecked {r.Unchecked}");
        Console.WriteLine($"final hash 0x{r.FinalHash:x16}, all peers agree: {YesNo(r.AllAgree)}, bundle replay matches: {YesNo(r.ReplayMatches)}");
        Console.WriteLine($"bundles {r.Bundles}, commands {r.Commands}, rejected {r.Rejected}{(r.Finished ? "" : ", STOPPED EARLY (desync or stall)")}");
        bool ok = r.Finished && r.Desyncs == 0 && r.AllAgree && r.ReplayMatches;
        Console.WriteLine(ok ? "OK: in sync" : "FAIL: out of sync");
        return ok ? 0 : 1;
    }

    private static string YesNo(bool value) => value ? "yes" : "no";

    private static bool Int(Dictionary<string, string> flags, string name, int fallback, out int value)
    {
        value = fallback;
        return !flags.TryGetValue(name, out string? text) || int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out value);
    }

    private static int UsageError(string message)
    {
        Console.Error.WriteLine($"error: {message}");
        Console.Error.WriteLine(Usage);
        return 2;
    }
}
