// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace Rts.Llm;

/// <summary>Shared HTTP plumbing for the providers. Failure reasons are fixed short strings: never exception
/// messages, URLs, headers or keys.</summary>
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
                return Fail("HTTP " + (int)resp.StatusCode);
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
