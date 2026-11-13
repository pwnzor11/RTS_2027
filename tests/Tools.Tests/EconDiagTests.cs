// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Tools;

namespace Rts.Tools.Tests;

/// <summary>S23 (M13-23): `ai match --trace N --econ 1` adds an economy line and a where line per side; without --econ the trace is unchanged.</summary>
[Collection("ToolsConsole")]
public class EconDiagTests
{
    private static string Run(params string[] extra)
    {
        string[] args = ["match", "--a", "tiered:3", "--b", "tiered:3", "--size", "64", "--ticks", "1500", "--seeds", "1", "--first", "1", "--game", "1", "--trace", "1000", ..extra];
        var old = Console.Out;
        var sw = new StringWriter();
        Console.SetOut(sw);
        try { Assert.Equal(0, AiCommands.Run(args)); }
        finally { Console.SetOut(old); }
        return sw.ToString();
    }

    [Fact]
    public void Econ_flag_prints_economy_and_where_lines_for_both_sides()
    {
        string o = Run("--econ", "1");
        Assert.Contains("econ P0", o);
        Assert.Contains("econ P1", o);
        Assert.Contains("ore map", o);
        Assert.Contains("where P0: units", o);
        Assert.Contains("where P1: units", o);
    }

    [Fact]
    public void Without_econ_flag_the_trace_has_no_extra_lines()
    {
        string o = Run();
        Assert.DoesNotContain("econ P0", o);
        Assert.DoesNotContain("where P0", o);
    }
}
