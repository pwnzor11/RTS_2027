// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Llm;

namespace Rts.Llm.Tests.S18;

/// <summary>S18 AI-07: one key per provider, no key over plain http except to this machine, and the old single key is never reused for another provider.</summary>
public sealed class LlmKeyPolicyTests
{
    private const string Ok = "{\"choices\":[{\"message\":{\"role\":\"assistant\",\"content\":\"hey\"}}],\"usage\":{\"prompt_tokens\":3,\"completion_tokens\":1}}";
    private const string AnthropicOk = "{\"content\":[{\"type\":\"text\",\"text\":\"hey\"}],\"usage\":{\"input_tokens\":3,\"output_tokens\":1}}";
    private static readonly ChatRequest Req = new("sys", new[] { new ChatMessage(ChatRole.User, "hi") }, 50);

    // ---- one key per provider ----

    [Fact]
    public void A_key_saved_for_one_provider_is_never_found_for_another()
    {
        ProviderKeyring keys = ProviderKeyring.InMemory();
        Assert.True(keys.Save(LlmProviderKind.Anthropic, "sk-ant-1"));
        Assert.Equal("sk-ant-1", keys.Load(LlmProviderKind.Anthropic));
        Assert.Null(keys.Load(LlmProviderKind.OpenAiCompatible));
        Assert.Null(keys.Load(LlmProviderKind.None));

        Assert.True(keys.Save(LlmProviderKind.OpenAiCompatible, "sk-oa-2"));
        Assert.Equal("sk-ant-1", keys.Load(LlmProviderKind.Anthropic));
        keys.Clear(LlmProviderKind.OpenAiCompatible);
        Assert.Null(keys.Load(LlmProviderKind.OpenAiCompatible));
        Assert.Equal("sk-ant-1", keys.Load(LlmProviderKind.Anthropic));
    }

    [Fact]
    public void Provider_none_has_no_key_to_save()
    {
        ProviderKeyring keys = ProviderKeyring.InMemory();
        Assert.False(keys.Save(LlmProviderKind.None, "sk-x"));
        Assert.Null(keys.Load(LlmProviderKind.None));
    }

    // ---- where a key may go ----

    [Theory]
    [InlineData("https://api.anthropic.com", true)]
    [InlineData("https://some.proxy.example/v1", true)]
    [InlineData("HTTPS://UPPER.EXAMPLE", true)]
    [InlineData("http://localhost:1234", true)]
    [InlineData("http://LOCALHOST", true)]
    [InlineData("http://127.0.0.1:8080/v1", true)]
    [InlineData("http://127.1.2.3", true)]
    [InlineData("http://[::1]:8080", true)]
    [InlineData("http://192.168.1.20:1234", false)]
    [InlineData("http://lan-box:1234", false)]
    [InlineData("http://example.test", false)]
    [InlineData("http://localhost.evil.example", false)]
    [InlineData("http://api.openai.com", false)]
    [InlineData("ftp://localhost", false)]
    [InlineData("localhost:1234", false)]
    [InlineData("not a url", false)]
    public void A_key_goes_over_https_anywhere_and_over_http_only_to_this_machine(string url, bool allowed) =>
        Assert.Equal(allowed, KeyPolicy.MaySendKey(url, "https://default.example"));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void An_empty_url_means_the_providers_default(string? url)
    {
        Assert.True(KeyPolicy.MaySendKey(url, "https://api.anthropic.com"));
        Assert.False(KeyPolicy.MaySendKey(url, "http://plain.example"));
    }

    private static LlmConfig OpenAi(string url) => new(LlmProviderKind.OpenAiCompatible, url, "m", 5);

    private static LlmConfig Anthropic(string url) => new(LlmProviderKind.Anthropic, url, "m", 5);

    [Fact]
    public async Task OpenAi_compatible_never_sends_a_key_to_a_plain_http_lan_server()
    {
        var h = StubHandler.Json(Ok);
        ChatResult r = await new OpenAiCompatibleProvider(OpenAi("http://lan-box:1234"), "sk-SECRET", h).CompleteAsync(Req, CancellationToken.None);
        Assert.False(r.Ok);
        Assert.Equal(KeyPolicy.RefusedReason, r.Error);
        Assert.Equal(0, h.Calls);
        Assert.DoesNotContain("SECRET", r.Error);
    }

    [Fact]
    public async Task Anthropic_never_sends_a_key_to_a_plain_http_server()
    {
        var h = StubHandler.Json(AnthropicOk);
        ChatResult r = await new AnthropicProvider(Anthropic("http://proxy.example"), "sk-ant-SECRET", h).CompleteAsync(Req, CancellationToken.None);
        Assert.False(r.Ok);
        Assert.Equal(KeyPolicy.RefusedReason, r.Error);
        Assert.Equal(0, h.Calls);
    }

    [Theory]
    [InlineData("http://localhost:1234")]
    [InlineData("http://127.0.0.1:1234")]
    [InlineData("https://lan-box.example")]
    public async Task OpenAi_compatible_sends_the_key_to_localhost_or_over_https(string url)
    {
        var h = StubHandler.Json(Ok);
        ChatResult r = await new OpenAiCompatibleProvider(OpenAi(url), "sk-KEY", h).CompleteAsync(Req, CancellationToken.None);
        Assert.True(r.Ok);
        Assert.Equal("Bearer sk-KEY", h.Request!.Headers.GetValues("Authorization").Single());
    }

    [Fact]
    public async Task A_plain_http_server_still_works_without_a_key()
    {
        var h = StubHandler.Json(Ok);
        ChatResult r = await new OpenAiCompatibleProvider(OpenAi("http://lan-box:1234"), null, h).CompleteAsync(Req, CancellationToken.None);
        Assert.True(r.Ok);
        Assert.False(h.Request!.Headers.Contains("Authorization"));
    }

    [Fact]
    public async Task Anthropic_sends_the_key_over_https_and_to_localhost()
    {
        foreach (string url in new[] { "", "https://proxy.example", "http://localhost:9999" })
        {
            var h = StubHandler.Json(AnthropicOk);
            ChatResult r = await new AnthropicProvider(Anthropic(url), "sk-ant-K", h).CompleteAsync(Req, CancellationToken.None);
            Assert.True(r.Ok, url);
            Assert.Equal("sk-ant-K", h.Request!.Headers.GetValues("x-api-key").Single());
        }
    }

    // ---- the old single key ----

    [Fact]
    public void The_old_key_becomes_the_Anthropic_key_only_when_the_saved_provider_is_Anthropic()
    {
        var legacy = new MemorySecretStore();
        legacy.Save("sk-ant-OLD");
        ProviderKeyring keys = ProviderKeyring.InMemory(legacy);
        Assert.Equal(LegacyKeyOutcome.MovedToAnthropic, keys.MigrateLegacy(LlmProviderKind.Anthropic));
        Assert.Equal("sk-ant-OLD", keys.Load(LlmProviderKind.Anthropic));
        Assert.Null(keys.Load(LlmProviderKind.OpenAiCompatible));
        Assert.Null(legacy.Load());
        Assert.Equal(LegacyKeyOutcome.NoLegacyKey, keys.MigrateLegacy(LlmProviderKind.Anthropic));
    }

    [Theory]
    [InlineData(LlmProviderKind.OpenAiCompatible)]
    [InlineData(LlmProviderKind.None)]
    public void The_old_key_is_dropped_and_must_be_entered_again_when_the_saved_provider_is_anything_else(LlmProviderKind saved)
    {
        var legacy = new MemorySecretStore();
        legacy.Save("sk-ant-OLD");
        ProviderKeyring keys = ProviderKeyring.InMemory(legacy);
        Assert.Equal(LegacyKeyOutcome.NeedsReentry, keys.MigrateLegacy(saved));
        Assert.Null(keys.Load(LlmProviderKind.Anthropic));
        Assert.Null(keys.Load(LlmProviderKind.OpenAiCompatible));
        Assert.Null(legacy.Load());
        // Switching to Anthropic later must not bring it back.
        Assert.Equal(LegacyKeyOutcome.NoLegacyKey, keys.MigrateLegacy(LlmProviderKind.Anthropic));
        Assert.Null(keys.Load(LlmProviderKind.Anthropic));
    }

    [Fact]
    public void A_key_already_entered_for_Anthropic_is_not_replaced_by_the_old_one()
    {
        var legacy = new MemorySecretStore();
        legacy.Save("sk-ant-OLD");
        ProviderKeyring keys = ProviderKeyring.InMemory(legacy);
        keys.Save(LlmProviderKind.Anthropic, "sk-ant-NEW");
        keys.MigrateLegacy(LlmProviderKind.Anthropic);
        Assert.Equal("sk-ant-NEW", keys.Load(LlmProviderKind.Anthropic));
    }

    [Fact]
    public void Without_an_old_key_there_is_nothing_to_migrate()
    {
        Assert.Equal(LegacyKeyOutcome.NoLegacyKey, ProviderKeyring.InMemory().MigrateLegacy(LlmProviderKind.Anthropic));
        Assert.Equal(LegacyKeyOutcome.NoLegacyKey, ProviderKeyring.InMemory(new MemorySecretStore()).MigrateLegacy(LlmProviderKind.Anthropic));
    }
}
