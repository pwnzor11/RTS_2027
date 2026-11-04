// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Game.Ui.Llm;
using Rts.Llm;

namespace Rts.Game.Tests.LlmSettings;

public sealed class LlmSettingsTests
{
    private static readonly ProviderDefaults Defaults = ProviderDefaults.Builtin;

    private static (LlmSettingsModel Model, Func<Rts.Game.Settings> Get, MemorySecretStore Store) Build(
        TestConnectionFunc? test = null, Rts.Game.Settings? start = null)
    {
        Rts.Game.Settings s = start ?? Rts.Game.Settings.Default;
        var store = new MemorySecretStore();
        var model = new LlmSettingsModel(() => s, v => s = v, store, Defaults, test ?? ((_, _, _) => Task.FromResult("not wired yet")));
        return (model, () => s, store);
    }

    [Fact]
    public void Defaults_are_off_chat_on_cap_120()
    {
        Rts.Game.Settings d = Rts.Game.Settings.Default;
        Assert.Equal("none", d.LlmProvider);
        Assert.Equal("", d.LlmBaseUrl);
        Assert.Equal("", d.LlmModel);
        Assert.True(d.LlmChatOn);
        Assert.Equal(120, d.LlmCallCap);
        Assert.Equal(2, Rts.Game.Settings.CurrentVersion);
    }

    [Fact]
    public void Llm_fields_round_trip()
    {
        var s = new Rts.Game.Settings(LlmProvider: "openai", LlmBaseUrl: "http://localhost:1234", LlmModel: "my \"model\"", LlmChatOn: false, LlmCallCap: 0);
        Rts.Game.Settings back = Rts.Game.Settings.Parse(s.ToToml());
        Assert.Equal(s, back);
    }

    [Fact]
    public void Old_version_1_file_loads_with_llm_defaults()
    {
        Rts.Game.Settings s = Rts.Game.Settings.Parse("version = 1\nfullscreen = true\nwindow_width = 1920\nwindow_height = 1080\n");
        Assert.True(s.Fullscreen);
        Assert.Equal(1920, s.WindowWidth);
        Assert.Equal("none", s.LlmProvider);
        Assert.True(s.LlmChatOn);
        Assert.Equal(120, s.LlmCallCap);
        Assert.Equal(Rts.Game.Settings.CurrentVersion, s.Version);
    }

    [Theory]
    [InlineData(5000, 1000)]
    [InlineData(-3, 0)]
    public void Call_cap_is_clamped(int written, int expected) =>
        Assert.Equal(expected, Rts.Game.Settings.Parse($"version = 2\nllm_call_cap = {written}").LlmCallCap);

    [Fact]
    public void Bad_llm_values_fall_back()
    {
        Rts.Game.Settings s = Rts.Game.Settings.Parse("version = 2\nllm_provider = \"skynet\"\nllm_chat_on = 3\nllm_model = 7\nllm_call_cap = \"x\"");
        Assert.Equal("none", s.LlmProvider);
        Assert.True(s.LlmChatOn);
        Assert.Equal("", s.LlmModel);
        Assert.Equal(120, s.LlmCallCap);
    }

    [Fact]
    public void Settings_toml_never_contains_a_key()
    {
        const string key = "sk-ant-SECRET-1234567890";
        var (model, get, _) = Build();
        model.CycleProvider(1);
        Assert.True(model.SetKey(key));
        string toml = get().ToToml();
        Assert.DoesNotContain(key, toml);
        Assert.DoesNotContain("api_key", toml, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(key, get().ToString());
    }

    [Fact]
    public void Provider_cycles_and_resets_url_and_model()
    {
        var (model, get, _) = Build(start: new Rts.Game.Settings(LlmProvider: "openai", LlmModel: "x", LlmBaseUrl: "http://h"));
        model.CycleProvider(1);
        Assert.Equal("none", get().LlmProvider);
        Assert.Equal("", get().LlmModel);
        model.CycleProvider(1);
        Assert.Equal("anthropic", get().LlmProvider);
        model.CycleProvider(-1);
        model.CycleProvider(-1);
        Assert.Equal("openai", get().LlmProvider);
    }

    [Fact]
    public void Strong_toggle_works_for_anthropic_only()
    {
        var (model, get, _) = Build(start: new Rts.Game.Settings(LlmProvider: "anthropic"));
        Assert.True(model.StrongAvailable);
        Assert.False(model.StrongOn);
        model.ToggleStrong();
        Assert.True(model.StrongOn);
        Assert.Equal(Defaults.AnthropicStrongModel, get().LlmModel);
        model.ToggleStrong();
        Assert.Equal("", get().LlmModel);

        var (other, getOther, _) = Build(start: new Rts.Game.Settings(LlmProvider: "openai"));
        other.ToggleStrong();
        Assert.Equal("", getOther().LlmModel);
        Assert.False(other.StrongAvailable);
    }

    [Fact]
    public void Cap_steps_and_chat_toggle()
    {
        var (model, get, _) = Build();
        model.StepCap(1);
        Assert.Equal(240, get().LlmCallCap);
        model.StepCap(-1);
        model.StepCap(-1);
        Assert.Equal(60, get().LlmCallCap);
        for (int i = 0; i < 10; i++) model.StepCap(-1);
        Assert.Equal(0, get().LlmCallCap);
        Assert.Equal("no cap", model.CapText());
        model.ToggleChat();
        Assert.False(get().LlmChatOn);
    }

    [Fact]
    public void Key_status_shows_only_set_or_none()
    {
        var (model, _, store) = Build();
        Assert.Equal("no key", model.KeyStatus);
        Assert.False(model.SetKey("   "));
        Assert.True(model.SetKey("  sk-abc  "));
        Assert.Equal("key set", model.KeyStatus);
        Assert.Equal("sk-abc", store.Load());
        model.ClearKey();
        Assert.Equal("no key", model.KeyStatus);
    }

    [Fact]
    public async Task Test_connection_uses_the_injected_function_and_scrubs_the_key()
    {
        LlmConfig? seen = null;
        string? seenKey = null;
        var (model, _, _) = Build(
            (c, k, _) => { seen = c; seenKey = k; return Task.FromResult($"echo {k}"); },
            new Rts.Game.Settings(LlmProvider: "anthropic"));
        model.SetKey("sk-SECRET");
        model.StartTest();
        for (int i = 0; i < 200 && (model.Testing || !model.TestResult.StartsWith("echo", StringComparison.Ordinal)); i++)
        {
            await Task.Delay(10);
            model.Poll();
        }

        Assert.Equal(LlmProviderKind.Anthropic, seen!.Provider);
        Assert.Equal(Defaults.AnthropicModel, seen.Model);
        Assert.Equal(Defaults.AnthropicBaseUrl, seen.BaseUrl);
        Assert.Equal("sk-SECRET", seenKey);
        Assert.DoesNotContain("SECRET", model.TestResult);
    }

    [Fact]
    public async Task Test_connection_failure_hides_the_exception_text()
    {
        var (model, _, _) = Build((_, _, _) => throw new InvalidOperationException("sk-LEAK"), new Rts.Game.Settings(LlmProvider: "openai"));
        model.StartTest();
        for (int i = 0; i < 200 && model.TestResult == "Testing..."; i++)
        {
            await Task.Delay(10);
            model.Poll();
        }

        Assert.Equal("Test failed.", model.TestResult);
    }

    [Fact]
    public async Task Default_hook_needs_a_provider()
    {
        Assert.Equal("Choose a provider first.", await LlmHooks.TestConnection(new LlmConfig(LlmProviderKind.None, "", ""), null, default));
        var (model, _, _) = Build();
        model.StartTest();
        Assert.Equal("Choose a provider first.", model.TestResult);
    }

    [Fact]
    public void Text_buffer_filters_caps_and_hides_its_text()
    {
        var b = new TextEditBuffer("", 5);
        Assert.Equal(5, b.Append("ab\tcdefgh"));
        Assert.Equal("abcde", b.Text);
        b.Backspace();
        Assert.Equal("abcd", b.Text);
        Assert.Equal("****", b.Masked());
        Assert.DoesNotContain("abcd", b.ToString());
    }

    [Fact]
    public void Provider_defaults_parse_and_fall_back()
    {
        ProviderDefaults p = ProviderDefaults.Parse("[anthropic]\ndefault_model = \"m1\"\nstrong_model = \"m2\"\n[openai]\nbase_url = \"http://x\"");
        Assert.Equal("m1", p.AnthropicModel);
        Assert.Equal("m2", p.AnthropicStrongModel);
        Assert.Equal("http://x", p.OpenAiBaseUrl);
        Assert.Equal(Defaults.AnthropicBaseUrl, p.AnthropicBaseUrl);
        Assert.Equal(Defaults, ProviderDefaults.Parse("not = = toml [["));
        LlmConfig c = Defaults.Resolve(LlmProviderKind.OpenAiCompatible, "", " gpt-x ");
        Assert.Equal("https://api.openai.com", c.BaseUrl);
        Assert.Equal("gpt-x", c.Model);
    }

    [Fact]
    public void Shipped_providers_file_parses_to_the_expected_shape()
    {
        string? dir = AppContext.BaseDirectory;
        string? file = null;
        while (dir is not null && file is null)
        {
            string candidate = Path.Combine(dir, "data", "llm", "providers.toml");
            if (File.Exists(candidate)) file = candidate;
            dir = Path.GetDirectoryName(dir);
        }

        Assert.NotNull(file);
        ProviderDefaults p = ProviderDefaults.Load(file!);
        Assert.StartsWith("https://", p.AnthropicBaseUrl);
        Assert.NotEqual("", p.AnthropicModel);
        Assert.NotEqual(p.AnthropicModel, p.AnthropicStrongModel);
        Assert.Equal("https://api.openai.com", p.OpenAiBaseUrl);
        Assert.Equal("", p.OpenAiModel);
    }
}
