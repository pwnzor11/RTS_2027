// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Data;
using Rts.Tools;

namespace Rts.Tools.Tests;

/// <summary>M9-03: `lan soak` plays a short headless LAN match in sync and refuses bad arguments; --udp runs it over real sockets (M9-04).</summary>
[Collection("ToolsConsole")]
public sealed class LanSoakCommandTests
{
    private static (int Code, string Out) Capture(string[] args)
    {
        TextWriter old = Console.Out;
        var sw = new StringWriter();
        Console.SetOut(sw);
        try { return (Cli.Run(args), sw.ToString()); }
        finally { Console.SetOut(old); }
    }

    [Theory]
    [InlineData("2")]
    [InlineData("3")]
    public void A_short_soak_compares_hashes_and_finds_no_desync(string players)
    {
        (int code, string text) = Capture(["lan", "soak", "--players", players, "--ticks", "320", "--latency-ms", "5", "--jitter-ms", "30", "--loss", "10", "--seed", "4"]);
        Assert.True(code == 0, text);
        int clients = int.Parse(players) - 1;
        Assert.Contains($"hashes compared {clients * 320 / 16}, desyncs 0, unchecked 0", text);
        Assert.Contains("all peers agree: yes, bundle replay matches: yes", text);
        Assert.Contains("OK: in sync", text);
    }

    [Fact]
    public void Runs_repeat_for_a_seed()
    {
        string[] args = ["lan", "soak", "--players", "2", "--ticks", "160", "--jitter-ms", "40", "--seed", "2"];
        (int a, string one) = Capture(args);
        (int b, string two) = Capture(args);
        Assert.Equal(0, a);
        Assert.Equal(0, b);
        // other test classes may print to the shared console meanwhile: compare only the soak's own lines
        static string Soak(string t) => string.Join("\n", t.Split('\n').Where(l => l.StartsWith("LAN soak") || l.StartsWith("hashes") || l.StartsWith("final") || l.StartsWith("bundles") || l.StartsWith("OK") || l.StartsWith("FAIL")));
        Assert.Equal(Soak(one), Soak(two));
    }

    [Fact]
    public void The_soak_is_the_same_game_as_a_plain_run_of_the_bundles()
    {
        var rules = RulesLoader.Load(RulesLocator.FindRulesDir(AppContext.BaseDirectory)!);
        var tiers = DifficultyLoader.Load(DifficultyLoader.PathBesideRules(RulesLocator.FindRulesDir(AppContext.BaseDirectory)!));
        LanSoakResult? slow = LanSoak.Run(new LanSoakOptions(2, 200, 40, 40, 20, 6, 4, 3, false), rules.Sim, rules.RulesHash, tiers);
        LanSoakResult? fast = LanSoak.Run(new LanSoakOptions(2, 200, 0, 0, 0, 6, 4, 3, false), rules.Sim, rules.RulesHash, tiers);
        Assert.NotNull(slow);
        Assert.NotNull(fast);
        Assert.True(slow!.Finished && slow.ReplayMatches && slow.AllAgree);
        Assert.Equal(fast!.FinalHash, slow.FinalHash);   // the network's timing never changes the game: the host stamps every tick
    }

    [Theory]
    [InlineData("--players", "5")]
    [InlineData("--players", "1")]
    [InlineData("--delay", "1")]
    [InlineData("--loss", "101")]
    [InlineData("--bogus", "1")]
    public void Bad_arguments_exit_2(string key, string value)
    {
        Assert.Equal(2, Capture(["lan", "soak", key, value]).Code);
    }

    [Theory]
    [InlineData("2")]
    [InlineData("3")]
    public void A_short_soak_over_real_udp_sockets_stays_in_sync(string players)
    {
        (int code, string text) = Capture(["lan", "soak", "--udp", "--players", players, "--ticks", "160", "--seed", "4"]);
        Assert.True(code == 0, text);
        Assert.Contains("transport udp", text);
        Assert.Contains($"hashes compared {(int.Parse(players) - 1) * 160 / 16}, desyncs 0, unchecked 0", text);
        Assert.Contains("all peers agree: yes, bundle replay matches: yes", text);
        Assert.Contains("OK: in sync", text);
    }

    [Fact]
    public void A_missing_subcommand_exits_2()
    {
        Assert.Equal(2, Capture(["lan"]).Code);
    }
}
