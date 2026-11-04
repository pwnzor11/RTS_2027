// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using System.Diagnostics;
using System.Globalization;
using System.Text;
using Rts.AI;
using Rts.Data;
using Rts.Sim;
using Rts.Sim.Core;
using Rts.Sim.Map;
using Rts.Sim.Rules;
using Rts.Sim.World;

namespace Rts.Tools;

/// <summary>
/// `Tools ai match` and `Tools ai matrix`: headless AI-vs-AI games (M1.5-06). Every seed generates a map and is
/// played twice with the start positions swapped. Exit codes: 0 ok, 1 data error, 2 bad arguments.
/// </summary>
public static class AiCommands
{
    private const int DefaultTier = 2;
    private const string Usage =
        "usage: ai match --a <aiId>[:<tier>] --b <aiId>[:<tier>] [--seeds 8] [--ticks 6000] [--size 64] [--out file.md] [--record-dir dir] [--flat 1] [--starts alt|slot|swap] [--start economy|army] [--first 1] [--faction-a authority|directorate] [--faction-b authority|directorate]\n" +
        "       ai matrix --ai <aiId> [--faction authority|directorate] [--tiers 1-5] [--seeds 4] [--ticks 6000] [--size 64] [--out file.md]\n" +
        "       ai matrix --players 2-4 [--ai tiered] [--tiers 3] [--faction authority|directorate|mixed] [--biome rust_belt|glass_coast|mountains|ash_plains|all] [--size s|m|l|<cells>] [--seeds 2] [--ticks 25000] [--out file.md]\n" +
        "       ai timeline --a <aiId>[:<tier>] --b <aiId>[:<tier>] [--seed 1] [--ticks 30000] [--size 64] [--swap 0|1] [--trace N] [--dump 1]\n" +
        "       ai duel --a <spec> --b <spec> [--count 1] [--count-b N] [--ticks 3000] [--gap 12]   (spec = id[:n][,id[:n]...], e.g. auth_tank:3,auth_turret:1)\n" +
        "  <tier> is 1..5 or recruit|regular|veteran|elite|commissar (default regular). Seeds are first..first+N-1 (first defaults to 1).";

    private static readonly string[] MatchOptions = ["a", "b", "seeds", "ticks", "size", "out", "record-dir", "flat", "starts", "start", "first", "faction-a", "faction-b"];
    private static readonly string[] TimelineOptions = ["a", "b", "seed", "ticks", "size", "swap", "trace", "dump", "faction-a", "faction-b"];
    private static readonly string[] DuelOptions = ["a", "b", "count", "count-b", "ticks", "gap"];
    private static readonly string[] MatrixOptions = ["ai", "tiers", "seeds", "ticks", "size", "out", "faction", "players", "biome"];

    /// <summary>Runs `ai ...` (args exclude the leading "ai").</summary>
    public static int Run(string[] args)
    {
        if (args.Length == 0) return UsageError("missing subcommand (match or matrix)");
        string[] rest = args[1..];
        return args[0] switch
        {
            "match" => Match(rest),
            "matrix" => Matrix(rest),
            "timeline" => Timeline(rest),
            "duel" => Duel(rest),
            "ffa" => Ffa(rest),
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
        // M3-14: cross-faction matches (default authority for both sides).
        if (flags.TryGetValue("faction-a", out string? fa) && !(Enum.TryParse(fa, true, out Faction facA) && (a = a! with { Faction = facA }) is not null))
            return UsageError("--faction-a must be authority or directorate");
        if (flags.TryGetValue("faction-b", out string? fb) && !(Enum.TryParse(fb, true, out Faction facB) && (b = b! with { Faction = facB }) is not null))
            return UsageError("--faction-b must be authority or directorate");

        bool army = false;
        if (flags.TryGetValue("start", out string? startArg))
        {
            if (startArg == "army") army = true;
            else if (startArg != "economy") return UsageError("--start must be economy or army");
        }
        if (!Int(flags, "first", 1, out int firstSeed) || firstSeed < 1) return UsageError("--first must be a positive integer");
        string? recordDir = flags.TryGetValue("record-dir", out string? rd) ? rd : null;
        var clock = Stopwatch.StartNew();
        MatchTally tally;
        try { tally = PlayPair(world!, a!, b!, seeds, ticks, size, recordDir, verbose: true, flat: flags.ContainsKey("flat"), startMode: flags.TryGetValue("starts", out string? sm) ? sm : "alt", start: army ? AiStartMode.Army : AiStartMode.Economy, firstSeed: firstSeed); }
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
        sb.AppendLine().AppendLine($"Wins by start corner: start 0 (4,4) {t.StartWins[0]}, start 1 (mirror) {t.StartWins[1]}. Wins by player slot: slot 0 {t.SlotWins[0]}, slot 1 {t.SlotWins[1]}.");
        if (t.Draws > 0)
            sb.AppendLine().AppendLine($"Draw tiebreak (remaining health): A ahead in {t.DrawsAAhead}, B ahead in {t.DrawsBAhead}, level in {t.Draws - t.DrawsAAhead - t.DrawsBAhead}.");
        return sb.ToString();
    }

    // ---- ai matrix ----------------------------------------------------------------------------

    private static int Matrix(string[] args)
    {
        if (!MatchSetup.TryParse(args, MatrixOptions, out var flags, out var positional, out string? error) || positional.Count > 0)
            return UsageError(error ?? $"unexpected argument '{positional[0]}'");
        if (flags.ContainsKey("players") || flags.ContainsKey("biome")) return MultiMatrix(flags);
        if (!Common(flags, 4, out int seeds, out int ticks, out int size, out error)) return UsageError(error!);
        if (!flags.TryGetValue("ai", out string? aiId)) return UsageError("--ai is required");
        if (!AiRegistry.Contains(aiId)) return UsageError($"unknown AI '{aiId}' (known: {string.Join(", ", AiRegistry.Ids)})");
        if (!ParseTiers(flags.TryGetValue("tiers", out string? tierArg) ? tierArg : "1-5", out List<int>? list, out error))
            return UsageError(error!);

        Faction faction = Faction.Authority;
        if (flags.TryGetValue("faction", out string? facArg) && !Enum.TryParse(facArg, true, out faction))
            return UsageError("--faction must be authority or directorate");
        if (!LoadData(out var world, out var profiles)) return 1;
        int n = list!.Count;
        var wins = new int[n, n];    // wins of row tier over column tier
        var draws = new int[n, n];
        var ahead = new int[n, n];   // draws in which the row tier has more remaining health
        var games = new int[n, n];
        long totalTicks = 0;
        var clock = Stopwatch.StartNew();
        try
        {
            for (int r = 0; r < n; r++)
                for (int c = r; c < n; c++)
                {
                    var sa = new AiSide(aiId, profiles![list[r] - 1], faction);
                    var sb = new AiSide(aiId, profiles[list[c] - 1], faction);
                    MatchTally t = PlayPair(world!, sa, sb, seeds, ticks, size, null, verbose: false,
                        start: aiId == AiRegistry.SkirmisherId ? AiStartMode.Army : AiStartMode.Economy);
                    totalTicks += t.Ticks;
                    games[r, c] = t.Games; draws[r, c] = t.Draws; wins[r, c] = t.AWins; ahead[r, c] = t.DrawsAAhead;
                    games[c, r] = t.Games; draws[c, r] = t.Draws; ahead[c, r] = c == r ? t.DrawsAAhead : t.DrawsBAhead; wins[c, r] = c == r ? t.AWins : t.BWins;
                }
        }
        catch (Exception e) when (e is ArgumentException or InvalidOperationException)
        {
            Console.Error.WriteLine($"error: {e.Message}");
            return 1;
        }
        clock.Stop();

        var md = new StringBuilder();
        md.AppendLine($"# AI tier matrix: {aiId} ({faction})");
        md.AppendLine();
        md.AppendLine($"Row tier's win % against the column tier. {seeds} seeds x 2 sides per cell, {ticks} ticks per game, map size {size}. Draws count as non-wins.");
        md.AppendLine();
        WriteMatrix(md, "Win %", list, profiles!, (r, c) => Pct(wins[r, c], games[r, c]));
        md.AppendLine();
        WriteMatrix(md, "Draw %", list, profiles!, (r, c) => Pct(draws[r, c], games[r, c]));
        md.AppendLine();
        md.AppendLine("Draws are decided by remaining health (both sides alive at the tick limit); the table below counts a win plus every draw in which the row tier is ahead.");
        md.AppendLine();
        WriteMatrix(md, "Win % incl. draws ahead on health", list, profiles!, (r, c) => Pct(wins[r, c] + ahead[r, c], games[r, c]));
        string text = md.ToString();
        Console.WriteLine(text);
        PrintSpeed(totalTicks, clock);
        return WriteOut(flags, text);
    }

    // ---- ai matrix --players (M4-12) -------------------------------------------------------------

    private static readonly (string Name, Biome Biome)[] BiomeNames =
        [("rust_belt", Biome.RustBelt), ("glass_coast", Biome.GlassCoast), ("mountains", Biome.Mountains), ("ash_plains", Biome.AshPlains)];

    /// <summary>
    /// M4-12: free-for-all games of N equal AIs per biome. Every seed is played N times with the sides rotated over the starts, so each
    /// side (and each faction, with --faction mixed) plays from every start. Reports per biome: decisive games, draws at the tick limit
    /// (stalls), the average end tick and, per START, the win share and the outlast share (of all opponent pairs, how often the player
    /// at that start lasted longer: 50 % is fair for any N).
    /// </summary>
    private static int MultiMatrix(Dictionary<string, string> flags)
    {
        if (!Int(flags, "players", 2, out int players) || players < 2 || players > 4) return UsageError("--players must be 2..4");
        if (!Int(flags, "seeds", 2, out int seeds) || seeds < 1) return UsageError("--seeds must be a positive integer");
        if (!Int(flags, "ticks", 25000, out int ticks) || ticks < 1) return UsageError("--ticks must be a positive integer");
        string aiId = flags.TryGetValue("ai", out string? ai) ? ai : AiRegistry.TieredId;
        if (!AiRegistry.Contains(aiId)) return UsageError($"unknown AI '{aiId}'");
        if (!ParseTiers(flags.TryGetValue("tiers", out string? tierArg) ? tierArg : "3", out List<int>? tierList, out string? error)) return UsageError(error!);
        string facArg = flags.TryGetValue("faction", out string? fa) ? fa : "authority";
        if (facArg is not ("authority" or "directorate" or "mixed")) return UsageError("--faction must be authority, directorate or mixed");
        var biomes = new List<(string Name, Biome Biome)>();
        string biomeArg = flags.TryGetValue("biome", out string? ba) ? ba : "all";
        foreach ((string name, Biome b) in BiomeNames) if (biomeArg == "all" || biomeArg == name) biomes.Add((name, b));
        if (biomes.Count == 0) return UsageError("--biome must be rust_belt, glass_coast, mountains, ash_plains or all");
        string sizeArg = flags.TryGetValue("size", out string? sa) ? sa : "m";
        MapSize? sizeClass = sizeArg switch { "s" => MapSize.Small, "m" => MapSize.Medium, "l" => MapSize.Large, _ => null };
        int cells = 0;
        if (sizeClass is null && (!int.TryParse(sizeArg, NumberStyles.None, CultureInfo.InvariantCulture, out cells) || cells < MapGenerator.MinSize || cells > SimConstants.MaxMapCells))
            return UsageError($"--size must be s, m, l or {MapGenerator.MinSize}..{SimConstants.MaxMapCells}");
        if (!LoadData(out var world, out var profiles)) return 1;

        var md = new StringBuilder();
        md.AppendLine($"# AI free-for-all: {players} x {aiId} ({facArg})");
        md.AppendLine();
        md.AppendLine($"{seeds} seeds x {players} rotations per biome, {ticks} ticks per game, map size {sizeArg}. Win % and outlast % are per start position (outlast: share of opponent pairs the player at that start outlived or, both alive, out-healthed; 50 % is fair).");
        long totalTicks = 0;
        var clock = Stopwatch.StartNew();
        string? mapNote = null;
        foreach (int tier in tierList!)
        {
            DifficultyProfile profile = profiles![tier - 1];
            md.AppendLine();
            md.AppendLine($"## Tier {profile.Id}");
            md.AppendLine();
            md.Append("| Biome | Games | Decisive | Draws | Avg end tick |");
            for (int st = 0; st < players; st++) md.Append($" start {st} win / outlast |");
            md.AppendLine();
            md.Append("|---|---:|---:|---:|---:|");
            for (int st = 0; st < players; st++) md.Append("---:|");
            md.AppendLine();
            foreach ((string bname, Biome biome) in biomes)
            {
                int games = 0, decisive = 0, draws = 0;
                long endSum = 0;
                var startWins = new int[players];
                var outlast = new int[players];
                var pairs = new int[players];
                for (int seed = 1; seed <= seeds; seed++)
                {
                    MapData map = MultiMap(biome, sizeClass, cells, players, (ulong)seed, out string? note);
                    mapNote ??= note;
                    for (int rot = 0; rot < players; rot++)
                    {
                        var sides = new List<AiSide>();
                        for (int i = 0; i < players; i++)
                        {
                            Faction f = facArg == "directorate" || (facArg == "mixed" && (i & 1) == 1) ? Faction.Directorate : Faction.Authority;
                            sides.Add(new AiSide(aiId, profile, f));
                        }
                        AiMultiResult r;
                        // the game seed differs per rotation too: with equal sides a rotation alone would replay the same game
                        try { r = AiMatchRunner.PlayN(map, world!.Rules, world.RulesHash, (ulong)(seed * 16 + rot), sides, rot, ticks); }
                        catch (Exception e) when (e is ArgumentException or InvalidOperationException)
                        {
                            Console.Error.WriteLine($"error: {e.Message}");
                            return 1;
                        }
                        games++;
                        totalTicks += r.EndTick;
                        endSum += r.EndTick;
                        if (r.Winner >= 0) { decisive++; startWins[r.Start[r.Winner]]++; } else draws++;
                        for (int a = 0; a < players; a++)
                            for (int b = 0; b < players; b++)
                            {
                                if (a == b) continue;
                                pairs[r.Start[a]]++;
                                if (r.Outlasted(a, b)) outlast[r.Start[a]]++;
                            }
                        Console.WriteLine($"tier {profile.Id} {bname} seed {seed} rot {rot}: {(r.Winner >= 0 ? $"side {r.Winner} (start {r.Start[r.Winner]}) wins" : "draw")} at tick {r.EndTick}; defeat ticks {string.Join(",", r.DefeatTick)}");
                    }
                }
                md.Append($"| {bname} | {games} | {decisive} | {draws} | {(games == 0 ? 0 : endSum / games)} |");
                for (int st = 0; st < players; st++) md.Append($" {Pct(startWins[st], games)} / {Pct(outlast[st], pairs[st])} |");
                md.AppendLine();
            }
        }
        if (mapNote is not null) md.AppendLine().AppendLine(mapNote);
        clock.Stop();
        string text = md.ToString();
        Console.WriteLine(text);
        PrintSpeed(totalTicks, clock);
        return WriteOut(flags, text);
    }

    /// <summary>
    /// M4-12: the map for an N-player matrix game. Uses <see cref="MapGenerator.Generate(MapGenParams)"/> (biome, size class, players,
    /// seed). While that generator still returns fewer than N starts (the M4-02 stub returns the 2-player map), a stand-in is built:
    /// the top-left quadrant of the generated map mirrored into all four quadrants (4-fold symmetric, a start in every corner), checked
    /// for connectivity, and <paramref name="note"/> says so. An explicit cell size always uses the legacy generator (plus the stand-in).
    /// </summary>
    private static MapData MultiMap(Biome biome, MapSize? sizeClass, int cells, int players, ulong seed, out string? note)
    {
        note = null;
        MapData m = sizeClass is MapSize sc ? MapGenerator.Generate(new MapGenParams(biome, sc, players, seed)) : MapGenerator.Generate(cells, seed);
        if (m.StartPositions.Count >= players) return m;
        note = $"Note: the map generator returned {m.StartPositions.Count} starts for {players} players (M4-02 not merged yet), so these games used a stand-in: the generated map's top-left quadrant mirrored into all four corners. Biome is not used by the stand-in.";
        for (ulong attempt = 0; attempt < 10; attempt++)
        {
            MapData src = attempt == 0 ? m : sizeClass is MapSize sc2 ? MapGenerator.Generate(new MapGenParams(biome, sc2, players, seed + attempt * 1000)) : MapGenerator.Generate(cells, seed + attempt * 1000);
            MapData q = QuadMirror(src, players);
            if (MapGenerator.IsConnected(q, Locomotion.Tracked)) return q;
        }
        throw new InvalidOperationException($"no connected {players}-start stand-in map from seed {seed}");
    }

    /// <summary>Mirrors the top-left quadrant into the other three (x, y and both), so every corner is a copy of the first start's.</summary>
    private static MapData QuadMirror(MapData m, int players)
    {
        int w = m.Width, h = m.Height, n = w * h;
        var tpl = new ushort[n]; var land = new LandType[n]; var hgt = new byte[n]; var ov = new OverlayKind[n]; var den = new byte[n];
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                int sx = x < (w + 1) / 2 ? x : w - 1 - x, sy = y < (h + 1) / 2 ? y : h - 1 - y;
                int d = y * w + x, src = sy * w + sx;
                tpl[d] = m.Template[src]; land[d] = m.Land[src]; hgt[d] = m.HeightLevel[src]; ov[d] = m.Overlay[src]; den[d] = m.Density[src];
            }
        CellCoord s0 = m.StartPositions[0];
        var corners = new List<CellCoord> { s0, new(w - 1 - s0.X, h - 1 - s0.Y), new(w - 1 - s0.X, s0.Y), new(s0.X, h - 1 - s0.Y) };
        return new MapData(m.Name + "_quad", m.Theater, w, h, tpl, land, hgt, ov, den, corners.GetRange(0, Math.Max(2, players)));
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


    // ---- ai timeline --------------------------------------------------------------------------

    /// <summary>Plays one game and prints when each side first got income and first owned each type (M2-14 balance pass).</summary>
    private static int Timeline(string[] args)
    {
        if (!MatchSetup.TryParse(args, TimelineOptions, out var flags, out var positional, out string? error) || positional.Count > 0)
            return UsageError(error ?? $"unexpected argument '{positional[0]}'");
        if (!Common(flags, 1, out _, out int ticks, out int size, out error)) return UsageError(error!);
        if (!Int(flags, "seed", 1, out int seed) || seed < 1) return UsageError("--seed must be a positive integer");
        if (!Int(flags, "swap", 0, out int swap) || swap > 1) return UsageError("--swap must be 0 or 1");
        bool dump = flags.ContainsKey("dump");
        if (!Int(flags, "trace", 0, out int trace)) return UsageError("--trace must be a non-negative integer");
        if (!flags.TryGetValue("a", out string? aArg) || !flags.TryGetValue("b", out string? bArg)) return UsageError("--a and --b are required");
        if (!LoadData(out var world, out var tiers)) return 1;
        if (!ParseSide(aArg, tiers!, out AiSide? a, out error) || !ParseSide(bArg, tiers!, out AiSide? b, out error)) return UsageError(error!);

        SimRules rules = world!.Rules;
        MapData map = MapGenerator.Generate(size, (ulong)seed);
        if (swap == 1) { var st = new List<CellCoord>(map.StartPositions); st.Reverse(); map = new MapData(map.Name, map.Theater, map.Width, map.Height, map.Template.ToArray(), map.Land.ToArray(), map.HeightLevel.ToArray(), map.Overlay.ToArray(), map.Density.ToArray(), st); }
        Faction tfa = Faction.Authority, tfb = Faction.Authority;
        if ((flags.TryGetValue("faction-a", out string? tfaArg) && !Enum.TryParse(tfaArg, true, out tfa))
            || (flags.TryGetValue("faction-b", out string? tfbArg) && !Enum.TryParse(tfbArg, true, out tfb)))
            return UsageError("--faction-a / --faction-b must be authority or directorate");
        var sim = new Simulation(new SimSetup((ulong)seed, map, 2, rules, world.RulesHash)
        { Factions = tfa == Faction.Authority && tfb == Faction.Authority ? null : [tfa, tfb] });
        SkirmishStart.Apply(sim);
        var driver = new AiDriver((ulong)seed);
        if (!AiRegistry.TryCreate(a!.AiId, a.Profile, out IAiPlayer? aiA) || !AiRegistry.TryCreate(b!.AiId, b.Profile, out IAiPlayer? aiB)) return UsageError("unknown AI");
        driver.Add(0, aiA!, a.Profile);
        driver.Add(1, aiB!, b.Profile);

        var firstOwned = new Dictionary<string, int>[2] { new(), new() };
        var firstIncome = new[] { -1, -1 };
        var firstDamage = -1;
        var health = new Dictionary<ulong, int>();
        var prevMoney = new[] { sim.Players.Cash[0] + sim.Players.Stored[0], sim.Players.Cash[1] + sim.Players.Stored[1] };
        var cashLog = new List<string>();
        var income = new long[2];
        int startField = FieldUnits(sim);
        var pending = new List<Command>();
        int winner = -1;
        for (int t = 0; t < ticks; t++)
        {
            driver.Collect(sim, pending);
            sim.Step(pending);
            pending.RemoveAll(c => c.ExecTick < sim.Tick);
            for (int p = 0; p < 2; p++)
            {
                int money = sim.Players.Cash[p] + sim.Players.Stored[p];
                if (money > prevMoney[p] && firstIncome[p] < 0 && sim.Tick > 2) firstIncome[p] = sim.Tick;
                if (money > prevMoney[p] && sim.Tick > 2) income[p] += money - prevMoney[p];
                prevMoney[p] = money;
            }
            if (sim.Tick % 1200 == 0)
                cashLog.Add($"{sim.Tick}: A {sim.Players.Cash[0]}+{sim.Players.Stored[0]} (income {income[0]}) pw {sim.Players.PowerOut[0]}/{sim.Players.PowerUse[0]} | B {sim.Players.Cash[1]}+{sim.Players.Stored[1]} (income {income[1]}) pw {sim.Players.PowerOut[1]}/{sim.Players.PowerUse[1]}");
            if (trace > 0 && sim.Tick % trace == 0)
            {
                Console.WriteLine(TraceLine(sim));
                Console.WriteLine("      " + AiLine(sim, 0, aiA) + " | " + AiLine(sim, 1, aiB));
            }
            if (sim.Tick % 4 != 0) continue;
            ScanFirst(sim, rules, firstOwned, health, ref firstDamage);
            bool aliveA = !sim.Players.Defeated[0], aliveB = !sim.Players.Defeated[1];
            if (aliveA && aliveB) continue;
            winner = aliveA ? 0 : aliveB ? 1 : -1;
            break;
        }
        Console.WriteLine($"# timeline seed {seed}{(swap == 1 ? " (swapped starts)" : "")}: slot 0 = {a.Label}, slot 1 = {b.Label}; 20 ticks = 1 second");
        Console.WriteLine($"end: tick {sim.Tick} ({sim.Tick / 20}s), winner slot {(winner < 0 ? "none" : winner.ToString())}; first damage anywhere: tick {firstDamage}");
        for (int p = 0; p < 2; p++)
        {
            Console.WriteLine($"slot {p}: first income tick {firstIncome[p]}");
            var parts = new List<string>();
            foreach (var kv in firstOwned[p]) parts.Add($"{kv.Key}@{kv.Value}");
            Console.WriteLine("  first owned: " + string.Join(", ", parts));
        }
        foreach (string line in cashLog) Console.WriteLine("  cash " + line);
        for (int i = 1; i < sim.Units.Capacity; i++)
            if (sim.Units.Slots.IsIndexAlive(i) && rules.Vehicles[sim.Units.TypeIndex[i]].HarvestCapacity > 0)
                Console.WriteLine($"  harvester slot {sim.Units.Player[i]}: at {sim.Units.Position[i].X / 256},{sim.Units.Position[i].Y / 256} mission {sim.Units.Mission[i]} step {sim.Economy.HarvestState[i]} goal {sim.Economy.Goal[i].X},{sim.Economy.Goal[i].Y} last {sim.Economy.LastFieldCell[i].X},{sim.Economy.LastFieldCell[i].Y} cargo {sim.Economy.Cargo[i]} hp {sim.Units.Health[i]}");
        if (dump)
            for (int p = 0; p < 2; p++)
            {
                PlayerHud hud = sim.HudFor((byte)p);
                var q = new List<string>();
                foreach (QueueView qv in hud.Queues)
                    q.Add($"{qv.Category}:[{string.Join(",", qv.Items.Select(i => (qv.Category is ProductionCategory.Vehicle ? rules.Vehicles[i.TypeIndex].Id : qv.Category is ProductionCategory.Infantry ? rules.Infantry[i.TypeIndex].Id : rules.Buildings[i.TypeIndex].Id) + (i.Stalled ? "(stalled)" : "") + i.ProgressPermille))}] ready {qv.ReadyTypeIndex}");
                Console.WriteLine($"  P{p} hud: cash {hud.Cash}+{hud.Stored} power {hud.PowerOut}/{hud.PowerUse} queues {string.Join(" ", q)}; buildable {hud.Buildable.Count}");
            }
        if (dump)
        {
            for (int i = 1; i < sim.Units.Capacity; i++)
                if (sim.Units.Slots.IsIndexAlive(i))
                    Console.WriteLine($"  unit P{sim.Units.Player[i]} {rules.Vehicles[sim.Units.TypeIndex[i]].Id} at {sim.Units.Position[i].X / 256},{sim.Units.Position[i].Y / 256} mission {sim.Units.Mission[i]} hp {sim.Units.Health[i]} goal {sim.CombatData.At(ActorKind.Vehicle, i).GuardPoint.X},{sim.CombatData.At(ActorKind.Vehicle, i).GuardPoint.Y} tgt {sim.CombatData.At(ActorKind.Vehicle, i).AttackTarget.Kind}:{sim.CombatData.At(ActorKind.Vehicle, i).AttackTarget.Id.Index} chase {sim.CombatData.At(ActorKind.Vehicle, i).ChaseCell.X},{sim.CombatData.At(ActorKind.Vehicle, i).ChaseCell.Y}");
            for (int i = 1; i < sim.Infantry.Capacity; i++)
                if (sim.Infantry.Slots.IsIndexAlive(i))
                    Console.WriteLine($"  inf P{sim.Infantry.Player[i]} {rules.Infantry[sim.Infantry.TypeIndex[i]].Id} at {sim.Infantry.Position[i].X / 256},{sim.Infantry.Position[i].Y / 256} mission {sim.Infantry.Mission[i]} hp {sim.Infantry.Health[i]} goal {sim.CombatData.At(ActorKind.Infantry, i).GuardPoint.X},{sim.CombatData.At(ActorKind.Infantry, i).GuardPoint.Y} tgt {sim.CombatData.At(ActorKind.Infantry, i).AttackTarget.Kind}:{sim.CombatData.At(ActorKind.Infantry, i).AttackTarget.Id.Index}");
            for (int i = 1; i < sim.Buildings.Capacity; i++)
                if (sim.Buildings.Slots.IsIndexAlive(i))
                    Console.WriteLine($"  bldg #{i} P{sim.Buildings.Player[i]} {rules.Buildings[sim.Buildings.TypeIndex[i]].Id} at {sim.Buildings.Cell[i].X},{sim.Buildings.Cell[i].Y} hp {sim.Buildings.Health[i]}");
        }
        if (dump)
            for (int y = 0; y < 30; y++)
            {
                var row = new StringBuilder();
                for (int x = 0; x < 40; x++)
                {
                    var c = new CellCoord(x, y);
                    char ch = sim.Map.BuildingAt(c) != EntityId.None ? 'B' : sim.Map.VehicleAt(c) != EntityId.None ? 'V' : sim.Map.InfantryAt(c, 0) != EntityId.None ? 'i' :
                        sim.Map.OverlayAt(c) != OverlayKind.None ? (char)('0' + Math.Min(9, (int)sim.Map.DensityAt(c))) : sim.Map.LandAt(c) == LandType.Clear ? '.' : '#';
                    row.Append(ch);
                }
                Console.WriteLine(row);
            }
        Console.WriteLine($"  field (ashfall+pitchblende density units) at start {startField}, at end {FieldUnits(sim)}");
        return 0;
    }

    /// <summary>One line: per slot the mobile actors (infantry+vehicles, harvesters excluded) and their centroid, structures alive.</summary>
    private static string TraceLine(Simulation sim)
    {
        var sb = new StringBuilder($"t{sim.Tick}:");
        for (int p = 0; p < 2; p++)
        {
            int n = 0, hp = 0, b = 0, harvHp = 0; long sx = 0, sy = 0;
            for (int i = 1; i < sim.Units.Capacity; i++)
                if (sim.Units.Slots.IsIndexAlive(i) && sim.Units.Player[i] == p)
                {
                    if (sim.Setup.Rules.Vehicles[sim.Units.TypeIndex[i]].HarvestCapacity > 0) { harvHp += sim.Units.Health[i]; continue; }
                    n++; hp += sim.Units.Health[i]; sx += sim.Units.Position[i].X / 256; sy += sim.Units.Position[i].Y / 256;
                }
            for (int i = 1; i < sim.Infantry.Capacity; i++)
                if (sim.Infantry.Slots.IsIndexAlive(i) && sim.Infantry.Player[i] == p)
                { n++; hp += sim.Infantry.Health[i]; sx += sim.Infantry.Position[i].X / 256; sy += sim.Infantry.Position[i].Y / 256; }
            for (int i = 1; i < sim.Buildings.Capacity; i++)
                if (sim.Buildings.Slots.IsIndexAlive(i) && sim.Buildings.Player[i] == p) b++;
            sb.Append($"  P{p} army {n} hp {hp} at {(n == 0 ? "-" : (sx / n) + "," + (sy / n))} bldg {b} cash {sim.Funds((byte)p)} scraper hp {harvHp};");
        }
        return sb.ToString();
    }

    /// <summary>M4-12: the Recruit framework's own counters for one slot (tier, target, waves, garrison, rescue) and the enemy
    /// buildings its view knows.</summary>
    private static string AiLine(Simulation sim, byte p, IAiPlayer? ai)
    {
        var known = new List<Rts.Sim.View.ViewActor>();
        sim.ViewFor(p).CopyKnownEnemyBuildings(known);
        if (ai is not RecruitAi r) return $"P{p} known {known.Count}";
        return $"P{p} tier {sim.TierOf(p)} known {known.Count} target {r.TargetPlayer} waves {r.Waves} reinf {r.Reinforcements} stale {r.StaleWaves} " +
               $"garrison {r.PeakGarrisoned}/{r.GarrisonOrders} sold {r.StructuresSold} up {r.UpgradesOrdered} air {r.AircraftOrdered} sw {r.SuperweaponsFired} [{r.MilitaryDebug}]";
    }

    private static int FieldUnits(Simulation sim)
    {
        int n = 0;
        for (int y = 0; y < sim.Map.Height; y++)
            for (int x = 0; x < sim.Map.Width; x++)
                if (sim.Map.OverlayAt(new CellCoord(x, y)) != OverlayKind.None) n += sim.Map.DensityAt(new CellCoord(x, y));
        return n;
    }

    private static void ScanFirst(Simulation sim, SimRules rules, Dictionary<string, int>[] first, Dictionary<ulong, int> health, ref int firstDamage)
    {
        for (int i = 1; i < sim.Buildings.Capacity; i++)
            if (sim.Buildings.Slots.IsIndexAlive(i))
                Note(first, sim.Buildings.Player[i], rules.Buildings[sim.Buildings.TypeIndex[i]].Id, sim.Tick);
        for (int i = 1; i < sim.Units.Capacity; i++)
            if (sim.Units.Slots.IsIndexAlive(i))
            {
                Note(first, sim.Units.Player[i], rules.Vehicles[sim.Units.TypeIndex[i]].Id, sim.Tick);
                Damage(health, 2UL << 40 | (ulong)i << 8 | (uint)sim.Units.Slots.GenerationOf(i) & 0xFF, sim.Units.Health[i], sim.Tick, ref firstDamage);
            }
        for (int i = 1; i < sim.Infantry.Capacity; i++)
            if (sim.Infantry.Slots.IsIndexAlive(i))
            {
                Note(first, sim.Infantry.Player[i], rules.Infantry[sim.Infantry.TypeIndex[i]].Id, sim.Tick);
                Damage(health, 3UL << 40 | (ulong)i << 8 | (uint)sim.Infantry.Slots.GenerationOf(i) & 0xFF, sim.Infantry.Health[i], sim.Tick, ref firstDamage);
            }
    }

    private static void Note(Dictionary<string, int>[] first, int player, string id, int tick)
    {
        if (player < 0 || player > 1) return;
        first[player].TryAdd(id, tick);
    }

    private static void Damage(Dictionary<ulong, int> health, ulong key, int hp, int tick, ref int firstDamage)
    {
        if (health.TryGetValue(key, out int prev) && hp < prev && firstDamage < 0) firstDamage = tick;
        health[key] = hp;
    }

    // ---- ai ffa (M4-12) ------------------------------------------------------------------------

    private static readonly string[] FfaOptions = ["players", "tier", "seed", "rot", "ticks", "size", "trace", "faction"];

    /// <summary>M4-12: one traced free-for-all game (the `ai matrix --players` setup for one seed and rotation), to find stalls.</summary>
    private static int Ffa(string[] args)
    {
        if (!MatchSetup.TryParse(args, FfaOptions, out var flags, out var positional, out string? error) || positional.Count > 0)
            return UsageError(error ?? $"unexpected argument '{positional[0]}'");
        if (!Int(flags, "players", 4, out int players) || players < 2 || players > 4) return UsageError("--players must be 2..4");
        if (!Int(flags, "tier", 3, out int tier) || tier < 1 || tier > 5) return UsageError("--tier must be 1..5");
        if (!Int(flags, "seed", 1, out int seed) || !Int(flags, "rot", 0, out int rot) || !Int(flags, "ticks", 25000, out int ticks)
            || !Int(flags, "size", 64, out int size) || !Int(flags, "trace", 2500, out int trace) || trace < 1) return UsageError("bad number");
        string fac = flags.TryGetValue("faction", out string? fa) ? fa : "authority";
        if (!LoadData(out var world, out var profiles)) return 1;
        MapData map = MultiMap(Biome.RustBelt, null, size, players, (ulong)seed, out _);
        int n = players;
        var slotOf = new int[n];
        var factions = new Faction[n];
        var sides = new AiSide[n];
        for (int i = 0; i < n; i++)
        {
            sides[i] = new AiSide(AiRegistry.TieredId, profiles![tier - 1], fac == "directorate" || (fac == "mixed" && (i & 1) == 1) ? Faction.Directorate : Faction.Authority);
            slotOf[i] = (i + rot) % n;
            factions[slotOf[i]] = sides[i].Faction;
        }
        ulong gameSeed = (ulong)(seed * 16 + rot);
        var sim = new Simulation(new SimSetup(gameSeed, map, n, world!.Rules, world.RulesHash) { Factions = factions });
        SkirmishStart.Apply(sim);
        var driver = new AiDriver(gameSeed);
        var ais = new IAiPlayer?[n];
        for (int i = 0; i < n; i++)
        {
            AiRegistry.TryCreate(sides[i].AiId, sides[i].Profile, out IAiPlayer? created);
            ais[slotOf[i]] = created;
            driver.Add((byte)slotOf[i], created!, sides[i].Profile);
        }
        var pending = new List<Command>();
        for (int t = 0; t < ticks; t++)
        {
            driver.Collect(sim, pending);
            sim.Step(pending);
            pending.RemoveAll(c => c.ExecTick < sim.Tick);
            if (sim.Tick % trace != 0) continue;
            Console.WriteLine($"t{sim.Tick}");
            for (byte p = 0; p < n; p++)
            {
                int army = 0, bldg = 0;
                for (int i = 1; i < sim.Units.Capacity; i++) if (sim.Units.Slots.IsIndexAlive(i) && sim.Units.Player[i] == p) army++;
                for (int i = 1; i < sim.Infantry.Capacity; i++) if (sim.Infantry.Slots.IsIndexAlive(i) && sim.Infantry.Player[i] == p) army++;
                for (int i = 1; i < sim.Buildings.Capacity; i++) if (sim.Buildings.Slots.IsIndexAlive(i) && sim.Buildings.Player[i] == p) bldg++;
                Console.WriteLine($"   P{p}{(sim.Players.Defeated[p] ? " DEFEATED" : "")} mobiles {army} bldg {bldg} funds {sim.Funds(p)} {AiLine(sim, p, ais[p])}");
            }
        }
        SimRules rules = world.Rules;
        for (int i = 1; i < sim.Units.Capacity; i++)
            if (sim.Units.Slots.IsIndexAlive(i))
                Console.WriteLine($"  unit P{sim.Units.Player[i]} {rules.Vehicles[sim.Units.TypeIndex[i]].Id} at {sim.Units.Position[i].X / 256},{sim.Units.Position[i].Y / 256} {sim.Units.Mission[i]} hp {sim.Units.Health[i]} goal {sim.CombatData.At(ActorKind.Vehicle, i).GuardPoint}");
        for (int i = 1; i < sim.Infantry.Capacity; i++)
            if (sim.Infantry.Slots.IsIndexAlive(i))
                Console.WriteLine($"  inf P{sim.Infantry.Player[i]} {rules.Infantry[sim.Infantry.TypeIndex[i]].Id} at {sim.Infantry.Position[i].X / 256},{sim.Infantry.Position[i].Y / 256} {sim.Infantry.Mission[i]} hp {sim.Infantry.Health[i]}");
        for (int i = 1; i < sim.Buildings.Capacity; i++)
            if (sim.Buildings.Slots.IsIndexAlive(i))
                Console.WriteLine($"  bldg P{sim.Buildings.Player[i]} {rules.Buildings[sim.Buildings.TypeIndex[i]].Id} at {sim.Buildings.Cell[i].X},{sim.Buildings.Cell[i].Y} hp {sim.Buildings.Health[i]}");
        return 0;
    }

    // ---- ai duel ------------------------------------------------------------------------------

    private static int Duel(string[] args)
    {
        if (!MatchSetup.TryParse(args, DuelOptions, out var flags, out var positional, out string? error) || positional.Count > 0)
            return UsageError(error ?? $"unexpected argument '{positional[0]}'");
        if (!Int(flags, "count", 1, out int countA) || countA < 1 || countA > 40) return UsageError("--count must be 1..40");
        if (!Int(flags, "count-b", countA, out int countB) || countB < 1 || countB > 40) return UsageError("--count-b must be 1..40");
        if (!Int(flags, "ticks", 3000, out int ticks) || ticks < 1) return UsageError("--ticks must be a positive integer");
        if (!Int(flags, "gap", 12, out int gap) || gap < 2 || gap > 40) return UsageError("--gap must be 2..40 (cells between the two groups)");
        if (!flags.TryGetValue("a", out string? specA) || !flags.TryGetValue("b", out string? specB)) return UsageError("--a and --b are required");
        if (!LoadData(out var world, out _)) return 1;
        SimRules rules = world!.Rules;
        MapData map = Flatten(MapGenerator.Generate(64, 1));
        var sim = new Simulation(new SimSetup(1, map, 2, rules, world.RulesHash));
        int cy = map.Height / 2, cxA = map.Width / 2 - gap / 2, cxB = map.Width / 2 + (gap + 1) / 2;
        var groupA = SpawnSpec(sim, rules, 0, specA, countA, cxA, cy, -1, out string? errA);
        var groupB = SpawnSpec(sim, rules, 1, specB, countB, cxB, cy, 1, out string? errB);
        if (errA is not null || errB is not null) return UsageError(errA ?? errB!);
        var cmds = new List<Command>();
        ushort seq = 0;
        foreach (var (id, kind) in groupA) if (kind != ActorKind.Building) cmds.Add(new Command(0, 0, seq++, CommandKind.AttackMove, id, EntityId.None, new CellCoord(cxB, cy), (int)kind));
        foreach (var (id, kind) in groupB) if (kind != ActorKind.Building) cmds.Add(new Command(0, 1, seq++, CommandKind.AttackMove, id, EntityId.None, new CellCoord(cxA, cy), (int)kind));
        int end = ticks;
        int hpA0 = Health(sim, 0), hpB0 = Health(sim, 1);
        for (int t = 0; t < ticks; t++)
        {
            sim.Step(cmds);
            cmds.RemoveAll(c => c.ExecTick < sim.Tick);
            if (sim.Tick % 4 != 0) continue;
            if (Alive(sim, 0) == 0 || Alive(sim, 1) == 0) { end = sim.Tick; break; }
        }
        int aliveA = Alive(sim, 0), aliveB = Alive(sim, 1);
        string result = aliveA > 0 && aliveB == 0 ? "A wins" : aliveB > 0 && aliveA == 0 ? "B wins" : aliveA == 0 ? "mutual kill" : "no result";
        Console.WriteLine($"| {specA} x{countA} vs {specB} x{countB} | {result} | tick {end} ({end / 20}s) | A left {aliveA} ({Health(sim, 0)}/{hpA0} hp) | B left {aliveB} ({Health(sim, 1)}/{hpB0} hp) |");
        return 0;
    }

    private static bool IsStructure(SimRules rules, string part)
    {
        int colon = part.IndexOf(':');
        return SimRules.IndexOf(rules.Buildings, colon < 0 ? part : part[..colon], t => t.Id) >= 0;
    }

    /// <summary>Spawns a side from a spec "id[:n][,id[:n]...]" (n defaults to <paramref name="defaultCount"/>); mobile groups stand in
    /// a 4-row block, a structure stands in front of the block with a power plant behind it.</summary>
    private static List<(EntityId Id, ActorKind Kind)> SpawnSpec(Simulation sim, SimRules rules, byte player, string spec, int defaultCount,
        int cx, int cy, int dir, out string? error)
    {
        error = null;
        var all = new List<(EntityId, ActorKind)>();
        int slot = 0, structures = 0;
        bool plantPlaced = false;
        // structures first, so the mobile block cannot sit on their cells
        var parts = new List<string>(spec.Split(',', StringSplitOptions.RemoveEmptyEntries));
        parts.Sort((x, y) => IsStructure(rules, y).CompareTo(IsStructure(rules, x)));
        foreach (string part in parts)
        {
            int colon = part.IndexOf(':');
            string id = colon < 0 ? part : part[..colon];
            int count = defaultCount;
            if (colon >= 0 && !int.TryParse(part[(colon + 1)..], NumberStyles.None, CultureInfo.InvariantCulture, out count)) { error = $"bad count in '{part}'"; return all; }
            int v = SimRules.IndexOf(rules.Vehicles, id, t => t.Id), inf = SimRules.IndexOf(rules.Infantry, id, t => t.Id), b = SimRules.IndexOf(rules.Buildings, id, t => t.Id);
            if (v < 0 && inf < 0 && b < 0) { error = $"unknown unit or structure '{id}'"; return all; }
            for (int i = 0; i < count; i++, slot++)
            {
                if (b >= 0)
                {
                    BuildingType bt = rules.Buildings[b];
                    EntityId bid = sim.PlaceBuilding(player, b, new CellCoord(cx - dir * 2, cy - 3 + structures++ * (bt.FootprintH + 1)));
                    if (bid == EntityId.None) { error = $"cannot place '{id}'"; return all; }
                    all.Add((bid, ActorKind.Building));
                    int plant = SimRules.IndexOf(rules.Buildings, "auth_power", x => x.Id);
                    if (!plantPlaced && plant >= 0) { plantPlaced = true; sim.PlaceBuilding(player, plant, new CellCoord(cx + dir * 6, cy - 6)); }
                }
                else if (inf >= 0)
                {
                    int cell = slot / InfantryStore.SubSlotCount;
                    all.Add((sim.SpawnInfantry(player, inf, new CellCoord(cx - dir * (cell / 6), cy - 3 + cell % 6), slot % InfantryStore.SubSlotCount), ActorKind.Infantry));
                }
                else all.Add((sim.SpawnVehicle(player, v, new CellCoord(cx - dir * (2 + slot / 6), cy - 3 + slot % 6)), ActorKind.Vehicle));
            }
        }
        return all;
    }

    /// <summary>Mobile actors plus the non-power structures of the side (the duel ends when this is 0).</summary>
    private static int Alive(Simulation sim, byte player)
    {
        int n = 0;
        for (int i = 1; i < sim.Units.Capacity; i++) if (sim.Units.Slots.IsIndexAlive(i) && sim.Units.Player[i] == player) n++;
        for (int i = 1; i < sim.Infantry.Capacity; i++) if (sim.Infantry.Slots.IsIndexAlive(i) && sim.Infantry.Player[i] == player) n++;
        for (int i = 1; i < sim.Buildings.Capacity; i++)
            if (sim.Buildings.Slots.IsIndexAlive(i) && sim.Buildings.Player[i] == player && sim.Setup.Rules.Buildings[sim.Buildings.TypeIndex[i]].PrimaryWeapon >= 0) n++;
        return n;
    }

    private static int Health(Simulation sim, byte player)
    {
        long h = 0;
        for (int i = 1; i < sim.Units.Capacity; i++) if (sim.Units.Slots.IsIndexAlive(i) && sim.Units.Player[i] == player) h += sim.Units.Health[i];
        for (int i = 1; i < sim.Infantry.Capacity; i++) if (sim.Infantry.Slots.IsIndexAlive(i) && sim.Infantry.Player[i] == player) h += sim.Infantry.Health[i];
        for (int i = 1; i < sim.Buildings.Capacity; i++)
            if (sim.Buildings.Slots.IsIndexAlive(i) && sim.Buildings.Player[i] == player && sim.Setup.Rules.Buildings[sim.Buildings.TypeIndex[i]].PrimaryWeapon >= 0) h += sim.Buildings.Health[i];
        return (int)h;
    }

    // ---- playing ------------------------------------------------------------------------------

    private sealed record World(Rts.Sim.Rules.SimRules Rules, ulong RulesHash, string RulesDir);

    private sealed class MatchTally
    {
        public int[] StartWins = new int[2];
        public int[] SlotWins = new int[2];
        public int Games, AWins, BWins, Draws, DrawsAAhead, DrawsBAhead;
        public long Ticks, EndTickSum;
        public long AvgEndTick => Games == 0 ? 0 : EndTickSum / Games;
    }

    /// <summary>Same size and start positions, but all ground clear, flat, no overlays (isolates map features).</summary>
    private static MapData Flatten(MapData m)
    {
        int n = m.Width * m.Height;
        return new MapData(m.Name + "_flat", m.Theater, m.Width, m.Height, new ushort[n], new LandType[n], new byte[n],
            new OverlayKind[n], new byte[n], m.StartPositions);
    }

    private static MatchTally PlayPair(World w, AiSide a, AiSide b, int seeds, int ticks, int size, string? recordDir, bool verbose, bool flat = false, string startMode = "alt", AiStartMode start = AiStartMode.Economy, int firstSeed = 1)
    {
        var tally = new MatchTally();
        if (recordDir is not null) Directory.CreateDirectory(recordDir);
        for (int s = firstSeed; s < firstSeed + seeds; s++)
        {
            MapData map = MapGenerator.Generate(size, (ulong)s);
            if (flat) map = Flatten(map);
            for (int g = 0; g < 2; g++)
            {
                bool aInSlot0 = g == 0;
                // Slot order (who acts first in a tick) must not ride along with the start corner: by default the
                // corners trade slots on even seeds ("alt"); "slot" = corner p for slot p; "swap" = corner 1-p.
                bool swapSlots = startMode == "swap" || (startMode == "alt" && s % 2 == 0);
                FileStream? replay = recordDir is null ? null
                    : File.Create(Path.Combine(recordDir, $"{a.Label}_vs_{b.Label}_seed{s}_g{g + 1}.rtsreplay".Replace(':', '-')));
                AiGameResult r;
                // M4-16: each game of the pair gets its own game seed (same map), so the two games are independent samples
                // rather than one mirrored game counted twice (docs/balance/m4_16_start_bias.md).
                using (replay) r = AiMatchRunner.Play(map, w.Rules, w.RulesHash, (ulong)(s * 16 + g), a, b, aInSlot0, ticks, replay,
                    BuildInfo.Version, replay is null ? null : RulesBundle.ReadDirectory(w.RulesDir), swapSlots, start);
                tally.Games++;
                tally.Ticks += r.EndTick;
                tally.EndTickSum += r.EndTick;
                if (r.Winner >= 0)
                {
                    int slot = r.Winner == 0 ? (aInSlot0 ? 0 : 1) : (aInSlot0 ? 1 : 0);
                    tally.StartWins[swapSlots ? 1 - slot : slot]++;
                    tally.SlotWins[slot]++;
                }
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
                    Console.WriteLine($"seed {s} game {g + 1} (A in slot {(aInSlot0 ? 0 : 1)}, A at start {(aInSlot0 != swapSlots ? 0 : 1)}): {result} at tick {r.EndTick} (health A {r.ValueA}, B {r.ValueB})");
                    if (start == AiStartMode.Economy)
                        Console.WriteLine($"    A: {Stats(r.StatsA)} | B: {Stats(r.StatsB)}");
                }
            }
        }
        return tally;
    }

    private static string Stats(AiSideStats s) =>
        $"structures {s.Structures}, units {s.Units}, funds min {s.CashMin} max {s.CashMax} end {s.CashEnd}";

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
