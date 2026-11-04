// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using System.Net;
using System.Text.Json;
using Rts.Llm;

namespace Rts.Llm.Tests;

public class OpenAiCompatibleProviderTests
{
    private const string Key = "sk-OPENAI-SECRET-9";
    private const string Ok = "{\"choices\":[{\"message\":{\"role\":\"assistant\",\"content\":\"hey\"}}],\"usage\":{\"prompt_tokens\":30,\"completion_tokens\":8}}";
    private static readonly ChatRequest Req = new("sys", new[] { new ChatMessage(ChatRole.User, "hi") }, 50);

    private static LlmConfig Cfg(string url, int timeout = 20) => new(LlmProviderKind.OpenAiCompatible, url, "gpt-test", timeout);

    [Fact]
    public async Task Request_shape_and_success_parse()
    {
        var h = StubHandler.Json(Ok);
        var r = await new OpenAiCompatibleProvider(Cfg("https://example.test"), Key, h).CompleteAsync(Req, CancellationToken.None);
        Assert.True(r.Ok);
        Assert.Equal("hey", r.Text);
        Assert.Equal(30, r.InputTokens);
        Assert.Equal(8, r.OutputTokens);
        Assert.Equal("https://example.test/v1/chat/completions", h.Request!.RequestUri!.ToString());
        Assert.Equal("Bearer " + Key, h.Request.Headers.GetValues("Authorization").Single());
        using var doc = JsonDocument.Parse(h.Body!);
        var root = doc.RootElement;
        Assert.Equal("gpt-test", root.GetProperty("model").GetString());
        Assert.Equal(50, root.GetProperty("max_tokens").GetInt32());
        var msgs = root.GetProperty("messages");
        Assert.Equal(2, msgs.GetArrayLength());
        Assert.Equal("system", msgs[0].GetProperty("role").GetString());
        Assert.Equal("sys", msgs[0].GetProperty("content").GetString());
        Assert.Equal("user", msgs[1].GetProperty("role").GetString());
    }

    [Theory]
    [InlineData("http://localhost:1234/v1", "http://localhost:1234/v1/chat/completions")]
    [InlineData("http://localhost:1234/v1/", "http://localhost:1234/v1/chat/completions")]
    [InlineData("http://localhost:1234", "http://localhost:1234/v1/chat/completions")]
    [InlineData("", "https://api.openai.com/v1/chat/completions")]
    public async Task Url_does_not_double_v1(string baseUrl, string expected)
    {
        var h = StubHandler.Json(Ok);
        await new OpenAiCompatibleProvider(Cfg(baseUrl), "", h).CompleteAsync(Req, CancellationToken.None);
        Assert.Equal(expected, h.Request!.RequestUri!.ToString());
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public async Task Empty_key_omits_authorization(string? key)
    {
        var h = StubHandler.Json(Ok);
        await new OpenAiCompatibleProvider(Cfg("http://localhost:1"), key, h).CompleteAsync(Req, CancellationToken.None);
        Assert.False(h.Request!.Headers.Contains("Authorization"));
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized, "HTTP 401")]
    [InlineData(HttpStatusCode.TooManyRequests, "HTTP 429")]
    [InlineData(HttpStatusCode.InternalServerError, "HTTP 500")]
    public async Task Http_errors_fail_without_key(HttpStatusCode code, string reason)
    {
        var h = StubHandler.Json("{\"error\":\"\"}", code);
        var r = await new OpenAiCompatibleProvider(Cfg("http://x.test"), Key, h).CompleteAsync(Req, CancellationToken.None);
        Assert.False(r.Ok);
        Assert.Equal(reason, r.Error);
        Assert.DoesNotContain(Key, r.Error);
    }

    [Theory]
    [InlineData("<html>")]
    [InlineData("{}")]
    [InlineData("{\"choices\":[]}")]
    [InlineData("{\"choices\":[{\"message\":{\"content\":null}}]}")]
    public async Task Malformed_body_fails(string body)
    {
        var r = await new OpenAiCompatibleProvider(Cfg("http://x.test"), Key, StubHandler.Json(body)).CompleteAsync(Req, CancellationToken.None);
        Assert.False(r.Ok);
        Assert.Equal("bad response", r.Error);
    }

    [Fact]
    public async Task Exception_message_never_leaks_key()
    {
        var h = new StubHandler((_, _) => throw new InvalidOperationException("Bearer " + Key));
        var r = await new OpenAiCompatibleProvider(Cfg("http://x.test"), Key, h).CompleteAsync(Req, CancellationToken.None);
        Assert.False(r.Ok);
        Assert.DoesNotContain(Key, r.Error);
    }

    [Fact]
    public async Task Timeout_fails_with_reason()
    {
        var h = new StubHandler(async (_, ct) => { await Task.Delay(Timeout.Infinite, ct); return new HttpResponseMessage(); });
        var r = await new OpenAiCompatibleProvider(Cfg("http://x.test", 1), Key, h).CompleteAsync(Req, CancellationToken.None);
        Assert.False(r.Ok);
        Assert.Equal("timeout", r.Error);
    }

    [Fact]
    public async Task Invalid_url_fails_cleanly()
    {
        var r = await new OpenAiCompatibleProvider(Cfg("ht!tp://\u0001bad"), Key, StubHandler.Json(Ok)).CompleteAsync(Req, CancellationToken.None);
        Assert.False(r.Ok);
    }
}
