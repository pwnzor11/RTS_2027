// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Llm;

namespace Rts.Game.Ui.Llm;

/// <summary>M5-05: the real "Test connection": one tiny message through <see cref="ProviderFactory"/>. The result line is short and
/// never contains the API key (provider errors are already key-free; the key is also scrubbed here).</summary>
public static class LlmConnectionTest
{
    /// <summary>No provider could be built (None, or Anthropic without a key).</summary>
    public const string NoProvider = "Cannot connect: no provider or key.";
    /// <summary>Success prefix.</summary>
    public const string Ok = "Connected";
    /// <summary>Failure prefix.</summary>
    public const string Failed = "Failed";

    /// <summary>Runs the test. <paramref name="handler"/> is for tests (no network).</summary>
    public static async Task<string> RunAsync(LlmConfig config, string? key, CancellationToken ct, HttpMessageHandler? handler = null)
    {
        if (config.Provider == LlmProviderKind.None) return LlmUiText.NeedProvider;
        return await RunAsync(ProviderFactory.Create(config, key, handler), key, ct).ConfigureAwait(false);
    }

    /// <summary>Runs the test against a provider (null = cannot connect).</summary>
    public static async Task<string> RunAsync(IChatProvider? provider, string? key, CancellationToken ct)
    {
        if (provider is null) return NoProvider;
        try
        {
            var req = new ChatRequest("Reply with the single word: ok", new[] { new ChatMessage(ChatRole.User, "ping") }, 300);
            ChatResult r = await provider.CompleteAsync(req, ct).ConfigureAwait(false);
            return r.Ok ? Ok + " (" + provider.Name + ")." : Scrub(Failed + ": " + (r.Error ?? "no reason given"), key);
        }
        catch (OperationCanceledException)
        {
            return Failed + ": cancelled.";
        }
        catch (Exception)
        {
            return Failed + ".";
        }
    }

    private static string Scrub(string text, string? key)
    {
        if (!string.IsNullOrWhiteSpace(key)) text = text.Replace(key.Trim(), "***", StringComparison.Ordinal);
        return text.Length > 120 ? text[..120] : text;
    }
}
