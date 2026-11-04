// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Llm;

namespace Rts.Llm.Tests;

public class ProviderFactoryTests
{
    [Fact]
    public void None_gives_null() =>
        Assert.Null(ProviderFactory.Create(new LlmConfig(LlmProviderKind.None, "", ""), "key"));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void Anthropic_without_key_gives_null(string? key) =>
        Assert.Null(ProviderFactory.Create(new LlmConfig(LlmProviderKind.Anthropic, "", "m"), key));

    [Fact]
    public void Anthropic_with_key() =>
        Assert.IsType<AnthropicProvider>(ProviderFactory.Create(new LlmConfig(LlmProviderKind.Anthropic, "", "m"), "k"));

    [Theory]
    [InlineData(null)]
    [InlineData("k")]
    public void OpenAi_compatible_works_with_or_without_key(string? key) =>
        Assert.IsType<OpenAiCompatibleProvider>(ProviderFactory.Create(new LlmConfig(LlmProviderKind.OpenAiCompatible, "http://l", "m"), key));

    [Fact]
    public async Task Handler_is_passed_through()
    {
        var h = StubHandler.Json("{\"content\":[{\"text\":\"ok\"}]}");
        var p = ProviderFactory.Create(new LlmConfig(LlmProviderKind.Anthropic, "", "m"), "k", h)!;
        var r = await p.CompleteAsync(new ChatRequest("", Array.Empty<ChatMessage>()), CancellationToken.None);
        Assert.True(r.Ok);
        Assert.Equal(1, h.Calls);
    }
}
