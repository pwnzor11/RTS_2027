// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.AI;

namespace Rts.Tools.Tests;

/// <summary>S38 (M5-15): `ai match --handicap-a N --handicap-b N` (10..200, default 100); the handicap follows its side when the start sides swap.</summary>
[Collection("ToolsConsole")]
public class AiMatchHandicapTests
{
    private static Dictionary<string, string> Flags(params string[] kv)
    {
        var d = new Dictionary<string, string>();
        for (int i = 0; i < kv.Length; i += 2) d[kv[i]] = kv[i + 1];
        return d;
    }

    private static (int Code, string Console, string File) RunMatch(params string[] extra)
    {
        string file = Path.Combine(Path.GetTempPath(), "rts_handicap_match_" + Guid.NewGuid().ToString("N") + ".md");
        var old = Console.Out;
        var sw = new StringWriter();
        try
        {
            Console.SetOut(sw);
            int code = AiCommands.Run(["match", "--a", "tiered:3", "--b", "tiered:3", "--seeds", "1", "--ticks", "2400", "--size", "96", "--out", file, .. extra]);
            return (code, sw.ToString(), File.Exists(file) ? File.ReadAllText(file) : "");
        }
        finally { Console.SetOut(old); if (File.Exists(file)) File.Delete(file); }
    }

    [Fact]
    public void Default_is_100_and_values_in_range_parse()
    {
        Assert.True(AiCommands.ParseHandicap(Flags(), "handicap-a", out int p, out var error));
        Assert.Equal(100, p); Assert.Null(error);
        Assert.True(AiCommands.ParseHandicap(Flags("handicap-a", "10"), "handicap-a", out p, out _)); Assert.Equal(10, p);
        Assert.True(AiCommands.ParseHandicap(Flags("handicap-b", "200"), "handicap-b", out p, out _)); Assert.Equal(200, p);
    }

    [Theory]
    [InlineData("9")]
    [InlineData("201")]
    [InlineData("-5")]
    [InlineData("abc")]
    [InlineData("1.5")]
    public void Invalid_values_are_rejected(string value)
    {
        Assert.False(AiCommands.ParseHandicap(Flags("handicap-a", value), "handicap-a", out _, out var error));
        Assert.Contains("handicap-a", error);
        Assert.Equal(2, RunMatch("--handicap-b", value).Code);   // usage error exit code
    }

    [Fact]
    public void The_handicap_goes_to_the_slot_its_side_plays_in()
    {
        Assert.Null(AiMatchRunner.HandicapList(100, 100, 0));
        Assert.Equal(new[] { 150, 100 }, AiMatchRunner.HandicapList(150, 100, 0));
        Assert.Equal(new[] { 100, 150 }, AiMatchRunner.HandicapList(150, 100, 1));
        Assert.Equal(new[] { 100, 60 }, AiMatchRunner.HandicapList(100, 60, 0));
    }

    [Fact]
    public void A_at_150_plays_a_different_game_than_the_default_and_the_header_says_so()
    {
        var plain = RunMatch();
        var edge = RunMatch("--handicap-a", "150");
        Assert.Equal(0, plain.Code);
        Assert.Equal(0, edge.Code);
        Assert.DoesNotContain("Handicap (percent", plain.File);
        Assert.Contains("A 150, B 100", edge.File);
        // the per-game lines (end tick, health, stats) must differ: the edge changes the game
        string Games(string c) => string.Join("\n", c.Split('\n').Where(l => l.StartsWith("seed ") || l.TrimStart().StartsWith("A:")));
        Assert.NotEqual(Games(plain.Console), Games(edge.Console));
    }
}
