// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Game.Screens;
using Rts.Game.Ui.Llm;
using Rts.Llm;

namespace Rts.Game.Tests.S18;

/// <summary>S18 AI-07 in the game: switching the provider in the settings never reuses another provider's key, the commander setup reads the
/// key of the chosen provider only, and the connection test obeys the same plain-http rule.</summary>
[Collection("LlmHooks")]
public sealed class SetupLlmKeyTests
{
    private static readonly ProviderDefaults Defaults = ProviderDefaults.Builtin;

    private static (LlmSettingsModel Model, Func<Rts.Game.Settings> Get, ProviderKeyring Keys, List<string?> SeenKeys) Build(string provider)
    {
        Rts.Game.Settings s = new(LlmProvider: provider);
        ProviderKeyring keys = ProviderKeyring.InMemory();
        var seen = new List<string?>();
        var model = new LlmSettingsModel(() => s, v => s = v, keys, Defaults, (_, k, _) => { seen.Add(k); return Task.FromResult("ok"); });
        return (model, () => s, keys, seen);
    }

    private static async Task RunTest(LlmSettingsModel model)
    {
        model.StartTest();
        for (int i = 0; i < 300 && (model.Testing || model.TestResult == LlmUiText.Testing); i++)
        {
            await Task.Delay(10);
            model.Poll();
        }
    }

    [Fact]
    public async Task Switching_the_provider_does_not_carry_the_key_over_and_switching_back_finds_it_again()
    {
        var (model, get, keys, seen) = Build("anthropic");
        Assert.True(model.SetKey("sk-ant-SECRET"));
        Assert.True(model.HasKey);

        model.CycleProvider(1);                       // anthropic -> openai
        Assert.Equal(LlmProviderKind.OpenAiCompatible, get().ProviderKind);
        Assert.False(model.HasKey);
        Assert.Equal("no key", model.KeyStatus);
        await RunTest(model);
        Assert.Equal(new string?[] { null }, seen);   // the test connection was handed no key at all

        model.CycleProvider(-1);                      // back to anthropic
        Assert.True(model.HasKey);
        await RunTest(model);
        Assert.Equal("sk-ant-SECRET", seen[^1]);
    }

    [Fact]
    public void Clearing_the_key_clears_only_the_current_providers()
    {
        var (model, _, keys, _) = Build("anthropic");
        model.SetKey("sk-ant-1");
        model.CycleProvider(1);
        model.SetKey("sk-oa-2");
        model.ClearKey();
        Assert.Null(keys.Load(LlmProviderKind.OpenAiCompatible));
        Assert.Equal("sk-ant-1", keys.Load(LlmProviderKind.Anthropic));
    }

    [Fact]
    public void A_key_cannot_be_saved_while_no_provider_is_chosen()
    {
        var (model, _, keys, _) = Build("none");
        Assert.False(model.SetKey("sk-x"));
        Assert.Null(keys.Load(LlmProviderKind.Anthropic));
        Assert.Null(keys.Load(LlmProviderKind.OpenAiCompatible));
    }

    [Fact]
    public void The_commander_setup_sees_a_key_only_for_the_provider_it_was_saved_for()
    {
        ProviderKeyring old = LlmHooks.Keys;
        try
        {
            LlmHooks.Keys = ProviderKeyring.InMemory();
            LlmHooks.Keys.Save(LlmProviderKind.Anthropic, "sk-ant-1");
            Assert.True(SkirmishSetupScreen.HasCommanderKey(new Rts.Game.Settings(LlmProvider: "anthropic")));
            Assert.False(SkirmishSetupScreen.HasCommanderKey(new Rts.Game.Settings(LlmProvider: "openai")));
        }
        finally { LlmHooks.Keys = old; }
    }

    [Fact]
    public async Task The_connection_test_refuses_to_send_a_key_over_plain_http_to_another_machine()
    {
        int calls = 0;
        var handler = new CountingHandler(() => calls++);
        var cfg = new LlmConfig(LlmProviderKind.OpenAiCompatible, "http://192.168.1.20:1234", "m", 5);
        string line = await LlmConnectionTest.RunAsync(cfg, "sk-SECRET", CancellationToken.None, handler);
        Assert.StartsWith(LlmConnectionTest.Failed, line);
        Assert.Contains("plain http", line);
        Assert.DoesNotContain("SECRET", line);
        Assert.Equal(0, calls);

        // Without a key the same server is fine, and so is the key over https or to localhost.
        await LlmConnectionTest.RunAsync(cfg, null, CancellationToken.None, handler);
        Assert.Equal(1, calls);
        await LlmConnectionTest.RunAsync(cfg with { BaseUrl = "http://localhost:1234" }, "sk-SECRET", CancellationToken.None, handler);
        Assert.Equal(2, calls);
    }

    private sealed class CountingHandler : HttpMessageHandler
    {
        private readonly Action _count;
        public CountingHandler(Action count) => _count = count;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            _count();
            var ok = "{\"choices\":[{\"message\":{\"role\":\"assistant\",\"content\":\"ok\"}}]}";
            return Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK) { Content = new StringContent(ok) });
        }
    }
}
