// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using System.Net;
using System.Text.Json;
using Rts.Llm;

namespace Rts.Llm.Tests;

public class AnthropicProviderTests
{
    private const string Key = "sk-ant-SECRET-123";
    private static readonly LlmConfig Cfg = new(LlmProviderKind.Anthropic, "", "claude-test", 20);
    private static readonly ChatRequest Req = new("be brief", new[] { new ChatMessage(ChatRole.User, "hi"), new ChatMessage(ChatRole.Assistant, "yo") }, 77);

    [Fact]
    public async Task Request_shape_and_success_parse()
    {
        var h = StubHandler.Json("{\"content\":[{\"type\":\"text\",\"text\":\"hello\"}],\"usage\":{\"input_tokens\":12,\"output_tokens\":5}}");
        var r = await new AnthropicProvider(Cfg, Key, h).CompleteAsync(Req, CancellationToken.None);
        Assert.True(r.Ok);
        Assert.Equal("hello", r.Text);
        Assert.Equal(12, r.InputTokens);
        Assert.Equal(5, r.OutputTokens);
        Assert.Equal("https://api.anthropic.com/v1/messages", h.Request!.RequestUri!.ToString());
        Assert.Equal(HttpMethod.Post, h.Request.Method);
        Assert.Equal(Key, h.Request.Headers.GetValues("x-api-key").Single());
        Assert.Equal("2023-06-01", h.Request.Headers.GetValues("anthropic-version").Single());
        Assert.StartsWith("application/json", h.Request.Content!.Headers.ContentType!.MediaType);
        using var doc = JsonDocument.Parse(h.Body!);
        var root = doc.RootElement;
        Assert.Equal("claude-test", root.GetProperty("model").GetString());
        Assert.Equal(77, root.GetProperty("max_tokens").GetInt32());
        Assert.Equal("be brief", root.GetProperty("system").GetString());
        var msgs = root.GetProperty("messages");
        Assert.Equal(2, msgs.GetArrayLength());
        Assert.Equal("user", msgs[0].GetProperty("role").GetString());
        Assert.Equal("hi", msgs[0].GetProperty("content").GetString());
        Assert.Equal("assistant", msgs[1].GetProperty("role").GetString());
    }

    [Fact]
    public async Task Custom_base_url_is_used()
    {
        var h = StubHandler.Json("{\"content\":[{\"text\":\"x\"}]}");
        var cfg = Cfg with { BaseUrl = "http://localhost:9999/" };
        var r = await new AnthropicProvider(cfg, Key, h).CompleteAsync(Req, CancellationToken.None);
        Assert.True(r.Ok);
        Assert.Equal(0, r.InputTokens);
        Assert.Equal("http://localhost:9999/v1/messages", h.Request!.RequestUri!.ToString());
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized, "HTTP 401")]
    [InlineData(HttpStatusCode.TooManyRequests, "HTTP 429")]
    [InlineData(HttpStatusCode.InternalServerError, "HTTP 500")]
    public async Task Http_errors_fail_without_key(HttpStatusCode code, string reason)
    {
        var h = StubHandler.Json("{\"error\":\"bad key \"}", code);
        var r = await new AnthropicProvider(Cfg, Key, h).CompleteAsync(Req, CancellationToken.None);
        Assert.False(r.Ok);
        Assert.Equal("", r.Text);
        Assert.Equal(reason, r.Error);
        Assert.DoesNotContain(Key, r.Error);
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("{}")]
    [InlineData("{\"content\":[]}")]
    [InlineData("{\"content\":[{\"text\":5}]}")]
    public async Task Malformed_body_fails(string body)
    {
        var r = await new AnthropicProvider(Cfg, Key, StubHandler.Json(body)).CompleteAsync(Req, CancellationToken.None);
        Assert.False(r.Ok);
        Assert.Equal("bad response", r.Error);
    }

    [Fact]
    public async Task Network_exception_message_never_leaks_key()
    {
        var h = new StubHandler((_, _) => throw new HttpRequestException("dns failure for key " + Key));
        var r = await new AnthropicProvider(Cfg, Key, h).CompleteAsync(Req, CancellationToken.None);
        Assert.False(r.Ok);
        Assert.DoesNotContain(Key, r.Error);
    }

    [Fact]
    public async Task Timeout_fails_with_reason()
    {
        var h = new StubHandler(async (_, ct) => { await Task.Delay(Timeout.Infinite, ct); return new HttpResponseMessage(); });
        var cfg = Cfg with { TimeoutSeconds = 1 };
        var r = await new AnthropicProvider(cfg, Key, h).CompleteAsync(Req, CancellationToken.None);
        Assert.False(r.Ok);
        Assert.Equal("timeout", r.Error);
    }

    [Fact]
    public async Task Caller_cancellation_throws()
    {
        var h = new StubHandler(async (_, ct) => { await Task.Delay(Timeout.Infinite, ct); return new HttpResponseMessage(); });
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => new AnthropicProvider(Cfg, Key, h).CompleteAsync(Req, cts.Token));
        Assert.Equal(0, h.Calls);
    }
}
