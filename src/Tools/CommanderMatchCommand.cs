// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using System.Diagnostics;
using System.Globalization;
using System.Text;
using Rts.AI;
using Rts.Data;
using Rts.Llm;
using Rts.Sim;
using Rts.Sim.Core;
using Rts.Sim.Map;
using Rts.Sim.Replay;
using Rts.Sim.Rules;

namespace Rts.Tools;

/// <summary>
/// `ai commander` (M5-11): one headless AI-vs-AI game in which side A is an LLM commander, set up the way a skirmish slot with a
/// commander plays (standard AI plus LLM directives, <see cref="Commander"/>). The API key comes only from the environment
/// (OPENAI_API_KEY or ANTHROPIC_API_KEY), never from a flag or a file, and is never written to the output. Output in the --out folder:
/// a replay, <c>directives.log</c> (one line per decision) and <c>summary.md</c>. Pacing: the game waits for each reply before it goes on,
/// so a slow network pauses game time instead of making the reply stale. Exit codes: 0 ok, 1 data or I/O error, 2 bad arguments or no key.
/// </summary>
public static class CommanderMatchCommand
{
    private const string Usage =
        "usage: ai commander --a <aiId>[:<tier>] --b <aiId>[:<tier>] --provider openai|anthropic [--model <name>] [--seed 1] [--ticks 30000] [--size 64] [--out ./commander_run]\n" +
        "       the key is read from OPENAI_API_KEY (openai) or ANTHROPIC_API_KEY (anthropic), never from a flag. --model defaults to the provider's default model.";

    private static readonly string[] Options = ["a", "b", "provider", "model", "seed", "ticks", "size", "out"];
    private const int DefaultTier = 2;
    private const int WinCheckInterval = 4;

    // Names as ReplyParser reads them, so a directive in the log uses the words the model was asked for.
    private static readonly string[] PostureNames = ["turtle", "balanced", "aggressive", "allin"];
    private static readonly string[] FocusNames = ["economy", "tech", "army", "air", "superweapon"];
    private static readonly string[] MixNames = ["auto", "infantry", "vehicles", "antiair", "antistructure"];

    /// <summary>Runs `ai commander` (args are the options after the word "commander").</summary>
    public static int Run(string[] args) =>
        RunWith(args, name => Environment.GetEnvironmentVariable(name), (cfg, key) => ProviderFactory.Create(cfg, key));

    /// <summary>The command with its environment and provider injected (tests use a fake provider and a fake environment).</summary>
    internal static int RunWith(string[] args, Func<string, string?> env, Func<LlmConfig, string, IChatProvider?> makeProvider)
    {
        if (!MatchSetup.TryParse(args, Options, out var flags, out var positional, out string? error) || positional.Count > 0)
            return UsageError(error ?? $"unexpected argument '{positional[0]}'");
        if (!flags.TryGetValue("a", out string? aArg) || !flags.TryGetValue("b", out string? bArg)) return UsageError("--a and --b are required");
        if (!flags.TryGetValue("provider", out string? providerArg)) return UsageError("--provider is required (openai or anthropic)");

        LlmProviderKind kind;
        string keyVar;
        switch (providerArg.Trim().ToLowerInvariant())
        {
            case "openai": kind = LlmProviderKind.OpenAiCompatible; keyVar = "OPENAI_API_KEY"; break;
            case "anthropic": kind = LlmProviderKind.Anthropic; keyVar = "ANTHROPIC_API_KEY"; break;
            default: return UsageError("--provider must be openai or anthropic");
        }
        if (!Int(flags, "seed", 1, out int seed) || seed < 1) return UsageError("--seed must be a positive integer");
        if (!Int(flags, "ticks", 30000, out int ticks) || ticks < 1) return UsageError("--ticks must be a positive integer");
        if (!Int(flags, "size", 64, out int size) || size < MapGenerator.MinSize || size > SimConstants.MaxMapCells)
            return UsageError($"--size must be {MapGenerator.MinSize}..{SimConstants.MaxMapCells}");
        string outDir = flags.TryGetValue("out", out string? outArg) ? outArg : "commander_run";

        // The key is checked before anything else runs; a missing key ends the command here.
        string key = (env(keyVar) ?? "").Trim();
        if (key.Length == 0)
        {
            Console.Error.WriteLine($"error: {keyVar} is not set. In this window only, set it with: $env:{keyVar} = \"<your key>\"");
            return 2;
        }

        ProviderDefaults defaults = ProviderDefaults.Load(Path.Combine(AppContext.BaseDirectory, "data", "llm", "providers.toml"));
        string model = flags.TryGetValue("model", out string? modelArg) && modelArg.Trim().Length > 0 ? modelArg.Trim() : defaults.ModelFor(kind);
        if (model.Length == 0) return UsageError("--model is required (any chat model your account has)");
        LlmConfig cfg = defaults.Resolve(kind, "", model);

        RuleSet? rules = MatchSetup.LoadRules(null, out string rulesDir);
        if (rules is null) return 1;
        IReadOnlyList<DifficultyProfile> tiers;
        try { tiers = DifficultyLoader.Load(DifficultyLoader.PathBesideRules(rulesDir)); }
        catch (RulesException ex)
        {
            foreach (RulesProblem p in ex.Problems) Console.Error.WriteLine($"{p.File}: {p.Key}: {p.Message}");
            return 1;
        }
        if (!ParseSide(aArg, tiers, out AiSide? a, out error)) return UsageError(error!);
        if (!ParseSide(bArg, tiers, out AiSide? b, out error)) return UsageError(error!);

        IChatProvider? provider = makeProvider(cfg, key);
        if (provider is null)
        {
            Console.Error.WriteLine("error: could not create the provider");
            return 1;
        }
        var recorder = new CallRecorder(provider);

        // Side A is the commander's slot 0 with its persona and faction name, as the game gives a commander slot.
        const string faction = "Authority";
        IReadOnlyList<CommanderPersona> personas = PersonaCatalog.Load(Path.Combine(AppContext.BaseDirectory, "data", "llm", "personas.toml"));
        using var commander = new Commander(0, recorder, PersonaCatalog.Pick(personas, faction, 0), faction, 0, true, new CommanderChatLog());

        string fullOut;
        try
        {
            Directory.CreateDirectory(outDir);
            fullOut = Path.GetFullPath(outDir);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or ArgumentException)
        {
            Console.Error.WriteLine($"error: cannot create {outDir}: {e.Message}");
            return 1;
        }

        string replayName = $"commander_seed{seed}.rtsreplay";
        try
        {
            using var replayStream = File.Create(Path.Combine(fullOut, replayName));
            using var log = new StreamWriter(Path.Combine(fullOut, "directives.log"), false, new UTF8Encoding(false)) { AutoFlush = true };
            return Play(new PlayPlan(a!, b!, seed, ticks, size, rules, rulesDir, cfg, providerArg.Trim(), model, fullOut, replayName),
                commander, recorder, replayStream, log, key);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            Console.Error.WriteLine($"error: {e.Message}");
            return 1;
        }
    }

    private sealed record PlayPlan(AiSide A, AiSide B, int Seed, int Ticks, int Size, RuleSet Rules, string RulesDir, LlmConfig Config,
        string Provider, string Model, string OutDir, string ReplayName);

    private static int Play(PlayPlan p, Commander commander, CallRecorder recorder, Stream replayStream, StreamWriter log, string key)
    {
        // Redacts the key from everything written, as a second guard on top of the providers' own error texts.
        string Redact(string s) => key.Length == 0 ? s : s.Replace(key, "[redacted]", StringComparison.Ordinal);

        MapData map = MapGenerator.Generate(p.Size, (ulong)p.Seed);
        var sim = new Simulation(MatchSetup.MakeSetup((ulong)p.Seed, map, p.Rules, MatchSetup.Players));
        SkirmishStart.Apply(sim);

        if (!AiRegistry.TryCreate(p.A.AiId, p.A.Profile, out IAiPlayer? aiA) || !AiRegistry.TryCreate(p.B.AiId, p.B.Profile, out IAiPlayer? aiB))
        {
            Console.Error.WriteLine("error: unknown AI");
            return 2;
        }
        var driver = new AiDriver((ulong)p.Seed);
        driver.Add(0, aiA!, p.A.Profile);
        driver.Add(1, aiB!, p.B.Profile);
        driver.SetDirectiveSource(0, commander);

        var replay = new ReplayWriter(replayStream, sim, BuildInfo.Version, RulesBundle.ReadDirectory(p.RulesDir));
        var pending = new List<Command>();
        int fresh = 0, winner = -1, callsSeen = 0, withDirective = 0, errors = 0;
        for (int t = 0; t < p.Ticks; t++)
        {
            driver.Collect(sim, pending);
            if (commander.CallsMade > callsSeen)
            {
                // A request started on this tick: wait for its reply so the game does not run past the 600-tick reply window.
                callsSeen = commander.CallsMade;
                CallRecord call = recorder.WaitForReply();
                double ms = call.LatencyMs;
                if (!call.Result.Ok)
                {
                    errors++;
                    log.WriteLine(Redact($"tick={sim.Tick} error latency_ms={ms:0} reason: {(call.Result.Error ?? "unknown").ReplaceLineEndings(" ")}"));
                }
                else
                {
                    CommanderReply reply = ReplyParser.Parse(call.Result.Text);
                    if (reply.Directive is not null) withDirective++;
                    string say = reply.Say is { } s ? $" say=\"{s.ReplaceLineEndings(" ")}\"" : "";
                    log.WriteLine(Redact($"tick={sim.Tick} ok latency_ms={ms:0} {Describe(reply)}{say}"));
                }
            }

            for (; fresh < pending.Count; fresh++) replay.RecordCommand(pending[fresh]);
            sim.Step(pending);
            pending.RemoveAll(c => c.ExecTick < sim.Tick);
            fresh = pending.Count;
            if (sim.Tick % SimConstants.HashIntervalTicks == 0) replay.RecordHash(sim);
            if (sim.Tick % WinCheckInterval != 0) continue;
            bool aliveA = !sim.Players.Defeated[0], aliveB = !sim.Players.Defeated[1];
            if (aliveA && aliveB) continue;
            winner = aliveA ? 0 : aliveB ? 1 : -1;
            break;
        }
        replay.Finish();

        string winnerText = winner == 0 ? "A (commander side)" : winner == 1 ? "B (standard AI)" : "none (draw at the tick limit)";
        var md = new StringBuilder();
        md.AppendLine("# Commander match");
        md.AppendLine();
        md.AppendLine($"- Side A (LLM commander {commander.Speaker}): {p.A.Label}");
        md.AppendLine($"- Side B (standard AI): {p.B.Label}");
        md.AppendLine($"- Provider: {p.Provider}, model: {p.Model}");
        md.AppendLine($"- Seed {p.Seed}, map {p.Size} cells, tick limit {p.Ticks}");
        md.AppendLine($"- Winner: {winnerText}");
        md.AppendLine($"- Ended at tick {sim.Tick} ({sim.Tick / 20} s of game time)");
        md.AppendLine($"- LLM calls: {callsSeen}; replies with a directive: {withDirective}; errors: {errors}");
        md.AppendLine($"- Files: {p.ReplayName} (replay), directives.log (one line per decision)");
        File.WriteAllText(Path.Combine(p.OutDir, "summary.md"), Redact(md.ToString()), new UTF8Encoding(false));

        Console.WriteLine($"wrote {p.OutDir}");
        Console.WriteLine($"winner: {winnerText}; ticks {sim.Tick}; LLM calls {callsSeen}; errors {errors}");
        return 0;
    }

    private static string Describe(CommanderReply reply)
    {
        if (reply.Directive is not { } d) return "directive: none (last one kept)";
        string target = d.TargetPlayer == 255 ? "ai" : $"slot{d.TargetPlayer}";
        string cell = d.FocusX < 0 || d.FocusY < 0 ? "none" : $"{d.FocusX},{d.FocusY}";
        return $"directive: posture={Name(PostureNames, d.Posture)} focus={Name(FocusNames, d.Focus)} target={target} " +
               $"mix={Name(MixNames, d.Mix)} expand={(d.Expand ? "yes" : "no")} cell={cell}";
    }

    private static string Name(string[] names, byte index) => index < names.Length ? names[index] : "?";

    /// <summary>Holds one provider call's result and how long it took. Written by the call's thread, read by the game thread after the wait.</summary>
    private sealed record CallRecord(ChatResult Result, double LatencyMs);

    /// <summary>
    /// Wraps the real provider: times each call and lets the game loop wait for the reply of the call that just started. Calls are
    /// one at a time (the commander never has two in flight), so the wait always matches the call it follows.
    /// </summary>
    private sealed class CallRecorder : IChatProvider
    {
        private readonly IChatProvider _inner;
        private readonly SemaphoreSlim _finished = new(0);
        private CallRecord? _last;

        public CallRecorder(IChatProvider inner) => _inner = inner;

        public string Name => _inner.Name;

        public async Task<ChatResult> CompleteAsync(ChatRequest request, CancellationToken ct)
        {
            var clock = Stopwatch.StartNew();
            ChatResult result = new(false, "", "request failed", 0, 0);
            try
            {
                result = await _inner.CompleteAsync(request, ct).ConfigureAwait(false);
            }
            catch (Exception e) when (e is not OperationCanceledException)
            {
                // keep the failed result; the reason never carries the key or headers
            }
            finally
            {
                _last = new CallRecord(result, clock.Elapsed.TotalMilliseconds);
                _finished.Release();
            }
            return result;
        }

        /// <summary>Blocks until the call in progress has finished, then returns its record.</summary>
        public CallRecord WaitForReply()
        {
            _finished.Wait();
            return _last!;
        }
    }

    // ---- arguments (same rules as `ai match`) -------------------------------------------------

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

    private static int UsageError(string message)
    {
        Console.Error.WriteLine($"error: {message}");
        Console.Error.WriteLine(Usage);
        return 2;
    }
}
