// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Llm;
using Rts.Tools;

namespace Rts.Tools.Tests;

/// <summary>S24 (M5-12): `ai commander --random-directives` runs without a provider or a key, logs seeded random decisions on the
/// commander's schedule, and one seed gives one log.</summary>
public class CommanderRandomDirectivesTests
{
    private static string FreshDir() => Path.Combine(Path.GetTempPath(), "rts_random_directives_" + Guid.NewGuid().ToString("N"));

    private static string[] Args(string outDir, int ticks, int seed) =>
        new[] { "--random-directives", "--a", "skirmisher", "--b", "skirmisher", "--ticks", ticks.ToString(), "--seed", seed.ToString(), "--out", outDir };

    [Fact]
    public void Random_mode_needs_no_provider_or_key_and_logs_decisions_on_the_schedule()
    {
        string dir = FreshDir();
        try
        {
            // no provider factory call and no key: a provider here would be a bug
            int code = CommanderMatchCommand.RunWith(Args(dir, 1500, 7), _ => null,
                (_, _) => throw new InvalidOperationException("random mode must not create a provider"));
            Assert.Equal(0, code);

            string log = File.ReadAllText(Path.Combine(dir, "directives.log"));
            // decisions at tick 600 and 1200 only (the commander's cadence)
            Assert.Contains("tick=600 random directive:", log);
            Assert.Contains("tick=1200 random directive:", log);
            Assert.DoesNotContain("tick=1800", log);
            Assert.Equal(2, log.Split('\n', StringSplitOptions.RemoveEmptyEntries).Length);

            string summary = File.ReadAllText(Path.Combine(dir, "summary.md"));
            Assert.Contains("--random-directives", summary);
            Assert.Contains("LLM calls: 0", summary);
        }
        finally { if (Directory.Exists(dir)) Directory.Delete(dir, true); }
    }

    [Fact]
    public void Same_seed_gives_the_same_decisions()
    {
        string a = FreshDir(), b = FreshDir();
        try
        {
            Assert.Equal(0, CommanderMatchCommand.RunWith(Args(a, 1500, 3), _ => null, (_, _) => null));
            Assert.Equal(0, CommanderMatchCommand.RunWith(Args(b, 1500, 3), _ => null, (_, _) => null));
            Assert.Equal(File.ReadAllText(Path.Combine(a, "directives.log")), File.ReadAllText(Path.Combine(b, "directives.log")));
        }
        finally
        {
            if (Directory.Exists(a)) Directory.Delete(a, true);
            if (Directory.Exists(b)) Directory.Delete(b, true);
        }
    }

    [Fact]
    public void Random_mode_still_checks_the_arguments()
    {
        Assert.Equal(2, CommanderMatchCommand.RunWith(new[] { "--random-directives", "--a", "skirmisher" }, _ => null, (_, _) => null));
    }
}
