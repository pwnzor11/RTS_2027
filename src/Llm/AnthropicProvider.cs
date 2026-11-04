// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using System.Text.Json;

namespace Rts.Llm;

/// <summary>Anthropic Messages API provider (POST /v1/messages).</summary>
public sealed class AnthropicProvider : IChatProvider
{
    /// <summary>Default API host.</summary>
    public const string DefaultBaseUrl = "https://api.anthropic.com";

    private readonly HttpClient _http;
    private readonly LlmConfig _cfg;
    private readonly string _key;

    /// <summary>Creates the provider. <paramref name="handler"/> is for tests; it is not disposed.</summary>
    public AnthropicProvider(LlmConfig cfg, string apiKey, HttpMessageHandler? handler = null)
    {
        _cfg = cfg;
        _key = apiKey;
        _http = ProviderHttp.MakeClient(handler);
    }

    /// <inheritdoc />
    public string Name => "Anthropic";

    /// <inheritdoc />
    public Task<ChatResult> CompleteAsync(ChatRequest request, CancellationToken ct)
    {
        string baseUrl = string.IsNullOrWhiteSpace(_cfg.BaseUrl) ? DefaultBaseUrl : _cfg.BaseUrl.Trim().TrimEnd('/');
        return ProviderHttp.SendAsync(_http, _cfg.TimeoutSeconds, () =>
        {
            var req = new HttpRequestMessage(HttpMethod.Post, baseUrl + "/v1/messages");
            req.Headers.TryAddWithoutValidation("x-api-key", _key);
            req.Headers.TryAddWithoutValidation("anthropic-version", "2023-06-01");
            req.Content = ProviderHttp.Json(w =>
            {
                w.WriteStartObject();
                w.WriteString("model", _cfg.Model);
                w.WriteNumber("max_tokens", request.MaxTokens);
                if (request.System.Length > 0) w.WriteString("system", request.System);
                w.WriteStartArray("messages");
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
        if (root.ValueKind == JsonValueKind.Object && root.TryGetProperty("content", out var content)
            && content.ValueKind == JsonValueKind.Array && content.GetArrayLength() > 0
            && content[0].ValueKind == JsonValueKind.Object && content[0].TryGetProperty("text", out var t)
            && t.ValueKind == JsonValueKind.String)
        {
            root.TryGetProperty("usage", out var usage);
            return new ChatResult(true, t.GetString() ?? "", null,
                ProviderHttp.Tokens(usage, "input_tokens"), ProviderHttp.Tokens(usage, "output_tokens"));
        }
        return ProviderHttp.Fail("bad response");
    }
}
