// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace Rts.Llm;

/// <summary>Shared HTTP plumbing for the providers. Failure reasons are fixed short strings: never exception
/// messages, URLs, headers or keys. One exception: a 400/404/422 reply appends the API's own <c>error.message</c>
/// (one line, at most 200 characters), because those name the bad request field; 401/403 messages can quote part of a
/// key, so they are never appended.</summary>
internal static class ProviderHttp
{
    internal const int DefaultTimeoutSeconds = 20;

    internal static ChatResult Fail(string reason) => new(false, "", reason, 0, 0);

    internal static HttpClient MakeClient(HttpMessageHandler? handler) =>
        handler is null ? new HttpClient() { Timeout = Timeout.InfiniteTimeSpan }
                        : new HttpClient(handler, disposeHandler: false) { Timeout = Timeout.InfiniteTimeSpan };

    internal static string RoleName(ChatRole role) => role == ChatRole.Assistant ? "assistant" : "user";

    internal static async Task<ChatResult> SendAsync(HttpClient http, int timeoutSeconds, Func<HttpRequestMessage> build,
        Func<JsonElement, ChatResult> parse, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(TimeSpan.FromSeconds(timeoutSeconds > 0 ? timeoutSeconds : DefaultTimeoutSeconds));
        try
        {
            using HttpRequestMessage req = build();
            using HttpResponseMessage resp = await http.SendAsync(req, cts.Token).ConfigureAwait(false);
            if (!resp.IsSuccessStatusCode)
            {
                int code = (int)resp.StatusCode;
                if (code is 400 or 404 or 422)
                {
                    string err = await resp.Content.ReadAsStringAsync(cts.Token).ConfigureAwait(false);
                    string? detail = ErrorMessage(err);
                    if (detail is not null) return Fail("HTTP " + code + ": " + detail);
                }
                return Fail("HTTP " + code);
            }
            string body = await resp.Content.ReadAsStringAsync(cts.Token).ConfigureAwait(false);
            using JsonDocument doc = JsonDocument.Parse(body);
            return parse(doc.RootElement);
        }
        catch (OperationCanceledException)
        {
            if (ct.IsCancellationRequested) throw;
            return Fail("timeout");
        }
        catch (JsonException) { return Fail("bad response"); }
        catch (HttpRequestException) { return Fail("network error"); }
        catch (Exception) { return Fail("request failed"); }
    }

    /// <summary>The <c>error.message</c> (or string <c>error</c>) of an API error body, one line, at most 200 chars; null if absent.</summary>
    internal static string? ErrorMessage(string body)
    {
        try
        {
            using JsonDocument doc = JsonDocument.Parse(body);
            JsonElement root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("error", out var e)) return null;
            string? m = e.ValueKind == JsonValueKind.String ? e.GetString()
                : e.ValueKind == JsonValueKind.Object && e.TryGetProperty("message", out var mm) && mm.ValueKind == JsonValueKind.String ? mm.GetString()
                : null;
            if (string.IsNullOrWhiteSpace(m)) return null;
            m = m.ReplaceLineEndings(" ").Trim();
            return m.Length > 200 ? m[..200] : m;
        }
        catch (JsonException) { return null; }
    }

    internal static StringContent Json(Action<Utf8JsonWriter> write)
    {
        using var ms = new MemoryStream();
        using (var w = new Utf8JsonWriter(ms)) write(w);
        var c = new StringContent(Encoding.UTF8.GetString(ms.ToArray()), Encoding.UTF8);
        c.Headers.ContentType = new MediaTypeHeaderValue("application/json");
        return c;
    }

    internal static int Tokens(JsonElement parent, string name)
    {
        if (parent.ValueKind == JsonValueKind.Object && parent.TryGetProperty(name, out var v)
            && v.ValueKind == JsonValueKind.Number && v.TryGetInt32(out int n) && n >= 0) return n;
        return 0;
    }
}
