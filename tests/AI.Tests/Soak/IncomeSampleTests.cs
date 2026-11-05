// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using System.Globalization;
using Rts.AI;
using Rts.Data;
using Rts.Sim;
using Rts.Sim.Map;
using Rts.Sim.Rules;
using Xunit.Abstractions;

namespace AI.Tests.Soak;

/// <summary>
/// M6-12 early-game income sampler (docs/design/11_m7_plan.md section 4). Plays mirror games of one faction and records, for both sides,
/// the stats counter CreditsHarvested (harvest, salvage and plunder credits) at minutes 3, 5 and 10 (ticks 3600, 6000, 12000). By default
/// it plays one short game. With <c>RTS_INCOME=1</c> it plays <c>RTS_INCOME_FACTIONS</c> x <c>RTS_INCOME_SIZES</c> (cells per side) x
/// seeds <c>RTS_INCOME_SEEDS</c> (e.g. <c>1-10</c>) x 2 slot orders at tier <c>RTS_INCOME_TIER</c> (2) and appends one line per game to
/// <c>RTS_INCOME_OUT</c>. Lines: <c>faction size seed game ended h3A h3B h5A h5B h10A h10B</c> (-1 when the game ended before that minute).
/// </summary>
public sealed class IncomeSampleTests(ITestOutputHelper log)
{
    private static readonly int[] SampleTicks = [3600, 6000, 12000];

    /// <summary>Smoke: a short mirror game yields sensible harvest counters.</summary>
    [Fact]
    public void AMirrorGameRecordsHarvestedCredits()
    {
        if (Environment.GetEnvironmentVariable("RTS_INCOME") == "1") { Run(); return; }
        int[] h = Play(Faction.Authority, 56, 1, 0, 2, 3700, out _);
        log.WriteLine(string.Join(" ", h));
        Assert.True(h[0] > 0 || h[1] > 0, "somebody harvested in three minutes");
    }

    private void Run()
    {
        string[] factions = (Environment.GetEnvironmentVariable("RTS_INCOME_FACTIONS") ?? "authority").Split(',');
        int[] sizes = (Environment.GetEnvironmentVariable("RTS_INCOME_SIZES") ?? "64").Split(',').Select(int.Parse).ToArray();
        string[] seeds = (Environment.GetEnvironmentVariable("RTS_INCOME_SEEDS") ?? "1-10").Split('-');
        int tier = int.Parse(Environment.GetEnvironmentVariable("RTS_INCOME_TIER") ?? "2", CultureInfo.InvariantCulture);
        string? outFile = Environment.GetEnvironmentVariable("RTS_INCOME_OUT");
        foreach (string f in factions)
        {
            Faction faction = f switch { "authority" => Faction.Authority, "directorate" => Faction.Directorate, "leftbehind" => Faction.LeftBehind, _ => throw new ArgumentException(f) };
            foreach (int size in sizes)
                for (int seed = int.Parse(seeds[0], CultureInfo.InvariantCulture); seed <= int.Parse(seeds[^1], CultureInfo.InvariantCulture); seed++)
                    for (int game = 0; game < 2; game++)
                    {
                        int[] h = Play(faction, size, seed, game, tier, SampleTicks[^1], out int ended);
                        string line = $"{f} {size} {seed} {game} {ended} {string.Join(" ", h)}";
                        log.WriteLine(line);
                        if (outFile is not null) File.AppendAllText(outFile, line + "\n");
                    }
        }
    }

    private static int[] Play(Faction faction, int size, int seed, int game, int tier, int ticks, out int ended)
    {
        DifficultyProfile profile = TestWorld.Tiers[tier - 1];
        var side = new AiSide(AiRegistry.TieredId, profile, faction);
        MapData map = MapGenerator.Generate(size, (ulong)seed);
        var result = new int[SampleTicks.Length * 2];
        Array.Fill(result, -1);
        AiGameResult r = AiMatchRunner.Play(map, TestWorld.Rules.Sim, TestWorld.Rules.RulesHash, (ulong)(seed * 16 + game), side, side,
            game == 0, ticks, null, "", null, seed % 2 == 0, AiStartMode.Economy, (sim, a, b, slotA, slotB) =>
            {
                for (int k = 0; k < SampleTicks.Length; k++)
                    if (sim.Tick == SampleTicks[k])
                    {
                        result[k * 2] = sim.Stats.CreditsHarvested[slotA];
                        result[k * 2 + 1] = sim.Stats.CreditsHarvested[slotB];
                    }
            });
        ended = r.EndTick;
        return result;
    }
}
