// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Tomlyn;
using Tomlyn.Model;

namespace Rts.Llm;

/// <summary>
/// Per-provider defaults from <c>data/llm/providers.toml</c>: base URL, default (cheap, fast) model and, for Anthropic, a
/// stronger alternative. Loading never throws; a missing or bad file or key gives the built-in values.
/// </summary>
public sealed record ProviderDefaults(
    string AnthropicBaseUrl,
    string AnthropicModel,
    string AnthropicStrongModel,
    string OpenAiBaseUrl,
    string OpenAiModel)
{
    /// <summary>The built-in defaults, used when the data file is missing.</summary>
    // Q-40 (session 7): current Anthropic models from platform.claude.com/docs/en/models/overview (2026-11-04).
    public static ProviderDefaults Builtin { get; } = new(
        "https://api.anthropic.com", "claude-haiku-5-5", "claude-sonnet-5-5", "https://api.openai.com", "");

    /// <summary>Anthropic <c>output_config.effort</c> ("low" keeps thinking short so a decision comes back fast; "" = do not send).</summary>
    public string AnthropicEffort { get; init; } = "low";

    /// <summary>Loads <paramref name="path"/>; never throws.</summary>
    public static ProviderDefaults Load(string path)
    {
        try
        {
            return File.Exists(path) ? Parse(File.ReadAllText(path)) : Builtin;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or System.Security.SecurityException)
        {
            return Builtin;
        }
    }

    /// <summary>Parses providers.toml text; never throws.</summary>
    public static ProviderDefaults Parse(string text)
    {
        TomlTable root;
        try
        {
            root = TomlSerializer.Deserialize<TomlTable>(text) ?? new TomlTable();
        }
        catch (Exception)
        {
            return Builtin;
        }

        ProviderDefaults d = Builtin;
        TomlTable a = Table(root, "anthropic");
        TomlTable o = Table(root, "openai");
        return new ProviderDefaults(
            Str(a, "base_url", d.AnthropicBaseUrl),
            Str(a, "default_model", d.AnthropicModel),
            Str(a, "strong_model", d.AnthropicStrongModel),
            Str(o, "base_url", d.OpenAiBaseUrl),
            Str(o, "default_model", d.OpenAiModel)) { AnthropicEffort = Str(a, "effort", d.AnthropicEffort) };
    }

    /// <summary>The base URL a provider uses when the settings leave it empty ("" for <see cref="LlmProviderKind.None"/>).</summary>
    public string BaseUrlFor(LlmProviderKind kind) => kind switch
    {
        LlmProviderKind.Anthropic => AnthropicBaseUrl,
        LlmProviderKind.OpenAiCompatible => OpenAiBaseUrl,
        _ => "",
    };

    /// <summary>The model a provider uses when the settings leave it empty ("" = the player must type one).</summary>
    public string ModelFor(LlmProviderKind kind) => kind switch
    {
        LlmProviderKind.Anthropic => AnthropicModel,
        LlmProviderKind.OpenAiCompatible => OpenAiModel,
        _ => "",
    };

    /// <summary>Settings values to a <see cref="LlmConfig"/>: empty URL or model take this provider's default.</summary>
    public LlmConfig Resolve(LlmProviderKind kind, string baseUrl, string model) =>
        new(kind,
            string.IsNullOrWhiteSpace(baseUrl) ? BaseUrlFor(kind) : baseUrl.Trim(),
            string.IsNullOrWhiteSpace(model) ? ModelFor(kind) : model.Trim(),
            Effort: kind == LlmProviderKind.Anthropic ? AnthropicEffort : "");

    private static TomlTable Table(TomlTable root, string key) =>
        root.TryGetValue(key, out object? v) && v is TomlTable t ? t : new TomlTable();

    private static string Str(TomlTable t, string key, string fallback) =>
        t.TryGetValue(key, out object? v) && v is string s ? s.Trim() : fallback;
}
