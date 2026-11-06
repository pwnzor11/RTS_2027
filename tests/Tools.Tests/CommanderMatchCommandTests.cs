// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Llm;
using Rts.Tools;

namespace Rts.Tools.Tests;

public class CommanderMatchCommandTests
{
    private static string FreshDir()
    {
        string dir = Path.Combine(Path.GetTempPath(), "rts_commander_test_" + Guid.NewGuid().ToString("N"));
        return dir;
    }

    private static string[] Args(string outDir, int ticks, params string[] extra) =>
        new[] { "--a", "skirmisher", "--b", "skirmisher", "--provider", "openai", "--model", "test-model", "--ticks", ticks.ToString(), "--out", outDir }
            .Concat(extra).ToArray();

    [Fact]
    public void Missing_key_exits_2_and_creates_no_output()
    {
        string dir = FreshDir();
        try
        {
            int code = CommanderMatchCommand.RunWith(Args(dir, 600), _ => null,
                (_, _) => throw new InvalidOperationException("provider must not be created without a key"));
            Assert.Equal(2, code);
            Assert.False(Directory.Exists(dir));
        }
        finally { if (Directory.Exists(dir)) Directory.Delete(dir, true); }
    }

    [Fact]
    public void Anthropic_missing_key_names_the_right_variable_and_exits_2()
    {
        string dir = FreshDir();
        var env = new Dictionary<string, string> { ["OPENAI_API_KEY"] = "sk-test-not-used" };
        int code = CommanderMatchCommand.RunWith(new[] { "--a", "skirmisher", "--b", "skirmisher", "--provider", "anthropic", "--model", "m", "--out", dir },
            name => env.TryGetValue(name, out string? v) ? v : null,
            (_, _) => throw new InvalidOperationException("provider must not be created without a key"));
        Assert.Equal(2, code);
        Assert.False(Directory.Exists(dir));
    }

    [Fact]
    public void Short_game_with_fake_provider_writes_replay_log_and_summary()
    {
        string dir = FreshDir();
        try
        {
            // The first decision is requested at tick 600 (Commander.CadenceTicks), so a directive needs more than 600 ticks.
            var fake = new FakeChatProvider();
            for (int i = 0; i < 4; i++)
                fake.EnqueueText("{\"directive\":{\"posture\":\"aggressive\",\"focus\":\"army\",\"mix\":\"vehicles\",\"expand\":false},\"say\":\"Advance.\"}");

            int code = CommanderMatchCommand.RunWith(Args(dir, 1200), name => name == "OPENAI_API_KEY" ? "sk-test-not-used" : null,
                (_, _) => fake);

            Assert.Equal(0, code);
            string log = File.ReadAllText(Path.Combine(dir, "directives.log"));
            Assert.Contains("ok", log);
            Assert.Contains("directive: posture=aggressive", log);
            Assert.DoesNotContain("sk-test-not-used", log);

            string summary = File.ReadAllText(Path.Combine(dir, "summary.md"));
            Assert.Contains("Winner:", summary);
            Assert.Contains("LLM calls:", summary);
            Assert.DoesNotContain("sk-test-not-used", summary);

            Assert.NotEmpty(Directory.GetFiles(dir, "*.rtsreplay"));
            Assert.NotEmpty(fake.Requests);
        }
        finally { if (Directory.Exists(dir)) Directory.Delete(dir, true); }
    }
}
