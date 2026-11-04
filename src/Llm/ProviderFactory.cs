// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
namespace Rts.Llm;

/// <summary>Builds the configured <see cref="IChatProvider"/>.</summary>
public static class ProviderFactory
{
    /// <summary>Returns null for provider None, or for Anthropic without a key. OpenAI-compatible works without a key
    /// (local servers). <paramref name="handler"/> is for tests.</summary>
    public static IChatProvider? Create(LlmConfig cfg, string? apiKey, HttpMessageHandler? handler = null)
    {
        switch (cfg.Provider)
        {
            case LlmProviderKind.Anthropic:
                return string.IsNullOrWhiteSpace(apiKey) ? null : new AnthropicProvider(cfg, apiKey.Trim(), handler);
            case LlmProviderKind.OpenAiCompatible:
                return new OpenAiCompatibleProvider(cfg, apiKey?.Trim(), handler);
            default:
                return null;
        }
    }
}
