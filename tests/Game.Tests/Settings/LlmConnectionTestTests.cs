// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Game.Ui.Llm;
using Rts.Llm;

namespace Rts.Game.Tests.Settings;

public class LlmConnectionTestTests
{
    [Fact]
    public async Task OkAndFailureLinesAreShortAndNeverEchoTheKey()
    {
        const string key = "sk-SECRET-key-123";
        var good = new FakeChatProvider();
        good.EnqueueText("ok");
        Assert.StartsWith("Connected", await LlmConnectionTest.RunAsync(good, key, default));
        Assert.Single(good.Requests);

        var bad = new FakeChatProvider();
        bad.Enqueue(new ChatResult(false, "", "bad key " + key, 0, 0));
        string r = await LlmConnectionTest.RunAsync(bad, key, default);
        Assert.StartsWith("Failed", r);
        Assert.DoesNotContain(key, r);
    }

    [Fact]
    public async Task NoProviderAndNoneGiveShortNotices()
    {
        Assert.Equal(LlmConnectionTest.NoProvider, await LlmConnectionTest.RunAsync((IChatProvider?)null, null, default));
        Assert.Equal("Choose a provider first.", await LlmConnectionTest.RunAsync(new LlmConfig(LlmProviderKind.None, "", ""), "k", default));
        Assert.Equal(LlmConnectionTest.NoProvider, await LlmConnectionTest.RunAsync(new LlmConfig(LlmProviderKind.Anthropic, "https://x.invalid", "m"), "", default));
    }
}
