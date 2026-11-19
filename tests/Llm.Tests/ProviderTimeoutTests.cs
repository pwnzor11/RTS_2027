// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Llm;

namespace Rts.Llm.Tests;

/// <summary>Session 35: providers.toml may set <c>timeout_seconds</c> per provider (5..600) for slow local model servers.</summary>
public class ProviderTimeoutTests
{
    [Fact]
    public void Default_timeout_is_twenty_seconds_for_both_providers()
    {
        ProviderDefaults d = ProviderDefaults.Parse("[openai]\nbase_url = \"http://localhost:11434\"\n");
        Assert.Equal(20, d.Resolve(LlmProviderKind.OpenAiCompatible, "", "m").TimeoutSeconds);
        Assert.Equal(20, d.Resolve(LlmProviderKind.Anthropic, "", "").TimeoutSeconds);
        Assert.Equal(20, ProviderDefaults.Builtin.Resolve(LlmProviderKind.OpenAiCompatible, "", "m").TimeoutSeconds);
    }

    [Fact]
    public void Timeout_is_read_per_provider()
    {
        ProviderDefaults d = ProviderDefaults.Parse("[openai]\ntimeout_seconds = 90\n[anthropic]\ntimeout_seconds = 30\n");
        Assert.Equal(90, d.Resolve(LlmProviderKind.OpenAiCompatible, "", "m").TimeoutSeconds);
        Assert.Equal(30, d.Resolve(LlmProviderKind.Anthropic, "", "").TimeoutSeconds);
    }

    [Theory]
    [InlineData("timeout_seconds = 4")]
    [InlineData("timeout_seconds = 601")]
    [InlineData("timeout_seconds = \"90\"")]
    [InlineData("timeout_seconds = 90.5")]
    public void Out_of_range_or_non_integer_timeouts_keep_the_default(string line) =>
        Assert.Equal(20, ProviderDefaults.Parse("[openai]\n" + line + "\n").Resolve(LlmProviderKind.OpenAiCompatible, "", "m").TimeoutSeconds);

    [Fact]
    public void The_shipped_file_keeps_the_default() =>
        Assert.Equal(20, ProviderDefaults.Load(Path.Combine(RepoRoot(), "data", "llm", "providers.toml"))
            .Resolve(LlmProviderKind.OpenAiCompatible, "", "m").TimeoutSeconds);

    private static string RepoRoot()
    {
        string dir = AppContext.BaseDirectory;
        while (!File.Exists(Path.Combine(dir, "RTS_2027.slnx"))) dir = Path.GetDirectoryName(dir) ?? throw new DirectoryNotFoundException("repo root");
        return dir;
    }
}
