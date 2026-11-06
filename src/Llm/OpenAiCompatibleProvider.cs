// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using System.Text.Json;

namespace Rts.Llm;

/// <summary>OpenAI-compatible chat provider (POST /v1/chat/completions); also used for local servers.</summary>
public sealed class OpenAiCompatibleProvider : IChatProvider
{
    /// <summary>Host used when the config has no base URL.</summary>
    public const string DefaultBaseUrl = "https://api.openai.com";

    private readonly HttpClient _http;
    private readonly LlmConfig _cfg;
    private readonly string _key;

    /// <summary>Creates the provider. An empty key omits the Authorization header. <paramref name="handler"/> is for tests.</summary>
    public OpenAiCompatibleProvider(LlmConfig cfg, string? apiKey, HttpMessageHandler? handler = null)
    {
        _cfg = cfg;
        _key = apiKey ?? "";
        _http = ProviderHttp.MakeClient(handler);
    }

    /// <inheritdoc />
    public string Name => "OpenAI-compatible";

    /// <summary>The full endpoint URL for a base URL (does not double a trailing /v1).</summary>
    internal static string EndpointFor(string baseUrl)
    {
        string b = string.IsNullOrWhiteSpace(baseUrl) ? DefaultBaseUrl : baseUrl.Trim().TrimEnd('/');
        return b.EndsWith("/v1", StringComparison.OrdinalIgnoreCase) ? b + "/chat/completions" : b + "/v1/chat/completions";
    }

    /// <summary>Extra completion tokens allowed on api.openai.com for a reasoning model's hidden reasoning.</summary>
    internal const int ReasoningHeadroomTokens = 4000;

    /// <summary>True when the endpoint is OpenAI's own API (host api.openai.com).</summary>
    internal static bool IsOfficialOpenAi(string url) =>
        Uri.TryCreate(url, UriKind.Absolute, out var u) && u.Host.Equals("api.openai.com", StringComparison.OrdinalIgnoreCase);

    /// <inheritdoc />
    public Task<ChatResult> CompleteAsync(ChatRequest request, CancellationToken ct)
    {
        string url = EndpointFor(_cfg.BaseUrl);
        return ProviderHttp.SendAsync(_http, _cfg.TimeoutSeconds, () =>
        {
            var req = new HttpRequestMessage(HttpMethod.Post, url);
            if (_key.Length > 0) req.Headers.TryAddWithoutValidation("Authorization", "Bearer " + _key);
            req.Content = ProviderHttp.Json(w =>
            {
                w.WriteStartObject();
                w.WriteString("model", _cfg.Model);
                // api.openai.com rejects max_tokens for newer models (HTTP 400) and wants max_completion_tokens, which also
                // counts hidden reasoning tokens, so it gets headroom on top of the reply budget. Local servers keep max_tokens.
                if (IsOfficialOpenAi(url)) w.WriteNumber("max_completion_tokens", request.MaxTokens + ReasoningHeadroomTokens);
                else w.WriteNumber("max_tokens", request.MaxTokens);
                w.WriteStartArray("messages");
                w.WriteStartObject();
                w.WriteString("role", "system");
                w.WriteString("content", request.System);
                w.WriteEndObject();
                foreach (var m in request.Messages)
                {
                    w.WriteStartObject();
                    w.WriteString("role", ProviderHttp.RoleName(m.Role));
                    w.WriteString("content", m.Text);
                    w.WriteEndObject();
                }
                w.WriteEndArray();
                w.WriteEndObject();
            });
            return req;
        }, Parse, ct);
    }

    private static ChatResult Parse(JsonElement root)
    {
        if (root.ValueKind == JsonValueKind.Object && root.TryGetProperty("choices", out var ch)
            && ch.ValueKind == JsonValueKind.Array && ch.GetArrayLength() > 0
            && ch[0].ValueKind == JsonValueKind.Object && ch[0].TryGetProperty("message", out var msg)
            && msg.ValueKind == JsonValueKind.Object && msg.TryGetProperty("content", out var t)
            && t.ValueKind == JsonValueKind.String)
        {
            root.TryGetProperty("usage", out var usage);
            return new ChatResult(true, t.GetString() ?? "", null,
                ProviderHttp.Tokens(usage, "prompt_tokens"), ProviderHttp.Tokens(usage, "completion_tokens"));
        }
        return ProviderHttp.Fail("bad response");
    }
}
