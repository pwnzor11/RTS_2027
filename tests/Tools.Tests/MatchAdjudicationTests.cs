// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Tools;

namespace Rts.Tools.Tests;

/// <summary>S24 (M13-23, D-242): the score adjudication of `ai match` games that reach the time limit.</summary>
public class MatchAdjudicationTests
{
    [Theory]
    [InlineData(110, 100, 0)]     // exactly 10 % above the other side: A wins
    [InlineData(100, 110, 1)]     // and B the same way round
    [InlineData(1000, 900, 0)]    // 1000 >= 900 * 1.1 = 990
    [InlineData(109, 100, -1)]    // 9 % above: even
    [InlineData(100, 100, -1)]    // equal: even
    [InlineData(0, 0, -1)]        // nothing left on either side: even
    [InlineData(1, 0, 0)]         // any score beats an empty side by the margin
    [InlineData(0, 1, 1)]
    [InlineData(5000, 4000, 0)]   // 25 % above
    public void Higher_score_wins_only_when_it_beats_the_other_by_ten_percent(long a, long b, int expected)
    {
        Assert.Equal(expected, MatchAdjudication.Adjudicate(a, b));
    }

    [Fact]
    public void The_margin_is_read_from_the_lower_score()
    {
        // 1100 vs 1000: 10 % of the lower score is exactly 100, so the higher side wins; 1099 vs 1000 is even
        Assert.Equal(0, MatchAdjudication.Adjudicate(1100, 1000));
        Assert.Equal(MatchAdjudication.Even, MatchAdjudication.Adjudicate(1099, 1000));
    }

    [Fact]
    public void Labels_are_A_B_or_even()
    {
        Assert.Equal("A", MatchAdjudication.Label(0));
        Assert.Equal("B", MatchAdjudication.Label(1));
        Assert.Equal("even", MatchAdjudication.Label(MatchAdjudication.Even));
    }

    // The laptop summary script (tools/measure/run_s19_after.ps1) reads `ai match` output with these patterns. Old lines and the new
    // lines (with the adjudication suffix and columns) must both match, so older result files keep parsing.
    [Theory]
    [InlineData("| 64 | 20 | 18 | 26 | 25000 | 31% |")]
    [InlineData("| 64 | 20 | 18 | 26 | 25000 | 31% | 3 | 1 | 22 |")]
    public void Laptop_table_pattern_reads_old_and_new_rows(string row)
    {
        var m = System.Text.RegularExpressions.Regex.Match(row, @"^\|\s*(\d+)\s*\|\s*(\d+)\s*\|\s*(\d+)\s*\|\s*(\d+)\s*\|\s*(\d+)\s*\|");
        Assert.True(m.Success);
        Assert.Equal("64", m.Groups[1].Value);
        Assert.Equal("20", m.Groups[2].Value);
        Assert.Equal("18", m.Groups[3].Value);
        Assert.Equal("26", m.Groups[4].Value);
        Assert.Equal("25000", m.Groups[5].Value);
    }

    [Theory]
    [InlineData("seed 1 game 2 (A in slot 1, A at start 1): draw at tick 30000 (health A 3520, B 3520)")]
    [InlineData("seed 1 game 2 (A in slot 1, A at start 1): draw at tick 30000 (health A 3520, B 3520) adj=even score=5428/5428")]
    public void Laptop_game_line_pattern_reads_old_and_new_draw_lines(string line)
    {
        var m = System.Text.RegularExpressions.Regex.Match(line, @"^seed \d+ game \d+ \(A in slot \d, A at start (\d)\): .* at tick (\d+)");
        Assert.True(m.Success);
        Assert.Equal("1", m.Groups[1].Value);
        Assert.Equal("30000", m.Groups[2].Value);
    }

    [Fact]
    public void Breakdown_total_is_army_plus_structures_plus_credits()
    {
        var b = new MatchAdjudication.Breakdown(Army: 1200, Structures: 800, Credits: 300);
        Assert.Equal(2300, b.Total);
    }

    // M13-30: the laptop summary (run_s19_after.ps1) reads the AdjW / AdjL / AdjEven columns of each row's .md with its own pattern.
    [Fact]
    public void Laptop_summary_script_reads_the_adjudication_columns()
    {
        string root = AppContext.BaseDirectory;
        while (root != null && !File.Exists(Path.Combine(root, "RTS_2027.slnx"))) root = Path.GetDirectoryName(root)!;
        string script = File.ReadAllText(Path.Combine(root!, "tools", "measure", "run_s19_after.ps1"));
        Assert.Contains("AdjW | AdjL | AdjEven", script);
        Assert.DoesNotContain("Session 19 after series", script);
        var line = script.Split('\n').First(l => l.StartsWith("$adjRow = ")).Trim();
        string pattern = line.Substring(line.IndexOf('\'') + 1).TrimEnd('\'');
        var m = System.Text.RegularExpressions.Regex.Match("| 64 | 20 | 18 | 26 | 25000 | 31% | 3 | 1 | 22 |", pattern);
        Assert.True(m.Success);
        Assert.Equal("3", m.Groups[1].Value);
        Assert.Equal("1", m.Groups[2].Value);
        Assert.Equal("22", m.Groups[3].Value);
        Assert.DoesNotMatch(pattern, "| 64 | 20 | 18 | 26 | 25000 | 31% |");   // old rows: no adj columns
    }
}
