// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Tools;

namespace Rts.Tools.Tests;

/// <summary>S47 (M16-A-08): `ai commander --random-directives --random-counters` is the random control for the counter-building directive.</summary>
public class S47LlmRandomCountersTests
{
    private static string FreshDir() => Path.Combine(Path.GetTempPath(), "rts_random_counters_" + Guid.NewGuid().ToString("N"));

    [Fact]
    public void Random_counters_run_without_a_provider_and_say_so_in_the_summary()
    {
        string dir = FreshDir();
        try
        {
            int code = CommanderMatchCommand.RunWith(
                new[] { "--random-directives", "--random-counters", "--a", "skirmisher", "--b", "skirmisher", "--ticks", "1500", "--seed", "5", "--out", dir },
                _ => null, (_, _) => throw new InvalidOperationException("random mode must not create a provider"));
            Assert.Equal(0, code);
            Assert.Contains("counter holds: on", File.ReadAllText(Path.Combine(dir, "summary.md")));
        }
        finally { if (Directory.Exists(dir)) Directory.Delete(dir, true); }
    }

    [Fact]
    public void Random_counters_without_random_directives_is_a_usage_error()
    {
        string dir = FreshDir();
        Assert.Equal(2, CommanderMatchCommand.RunWith(
            new[] { "--random-counters", "--a", "skirmisher", "--b", "skirmisher", "--out", dir }, _ => null, (_, _) => null));
        Assert.False(Directory.Exists(dir));
    }
}
