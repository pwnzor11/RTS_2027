// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Llm;

namespace Rts.Llm.Tests;

public class FakeChatProviderTests
{
    private static ChatRequest R(string s) => new(s, new[] { new ChatMessage(ChatRole.User, s) });

    [Fact]
    public async Task Returns_queued_in_order_and_records_requests()
    {
        var f = new FakeChatProvider();
        f.EnqueueText("a");
        f.Enqueue(new ChatResult(false, "", "boom", 0, 0));
        var r1 = await f.CompleteAsync(R("one"), CancellationToken.None);
        var r2 = await f.CompleteAsync(R("two"), CancellationToken.None);
        var r3 = await f.CompleteAsync(R("three"), CancellationToken.None);
        Assert.Equal("a", r1.Text);
        Assert.Equal("boom", r2.Error);
        Assert.False(r3.Ok);
        Assert.Equal(new[] { "one", "two", "three" }, f.Requests.Select(x => x.System));
    }

    [Fact]
    public async Task Delay_honours_cancellation()
    {
        var f = new FakeChatProvider { Delay = TimeSpan.FromMinutes(5) };
        f.EnqueueText("a");
        using var cts = new CancellationTokenSource();
        var t = f.CompleteAsync(R("x"), cts.Token);
        cts.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => t);
    }
}
