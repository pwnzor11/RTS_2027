// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Game.Update;
using Xunit;

namespace Rts.Game.Tests.M10;

/// <summary>M10-23: the pure parts of the update notice. Fixed JSON; nothing here touches the network.</summary>
public sealed class UpdateCheckTests
{
    private const string Json = """{"tag_name":"v0.2.0","html_url":"https://github.com/pwnzor11/RTS_2027/releases/tag/v0.2.0","name":"x"}""";

    [Theory]
    [InlineData("v0.2.0", "0.1.5+3f2a9c1", true)]
    [InlineData("0.1.6", "0.1.5", true)]
    [InlineData("v1.0.0", "0.9.9", true)]
    [InlineData("v0.1.5", "0.1.5+abc", false)]
    [InlineData("v0.1.4", "0.1.5", false)]
    [InlineData("v0.1.10", "0.1.9", true)]
    [InlineData("v1.0.0", "1.0.0-beta.1", true)]
    [InlineData("v1.0.0-rc1", "0.9.0", false)]
    [InlineData("v0.2.0", "dev", false)]
    [InlineData("latest", "0.1.5", false)]
    [InlineData("", "0.1.5", false)]
    public void IsNewer(string tag, string current, bool expected) => Assert.Equal(expected, UpdateCheck.IsNewer(tag, current));

    [Fact]
    public void ParsesTagAndUrl()
    {
        UpdateInfo? info = UpdateCheck.FromReleaseJson(Json, "0.1.5+3f2a9c1");
        Assert.NotNull(info);
        Assert.Equal("0.2.0", info.Version);
        Assert.Equal("https://github.com/pwnzor11/RTS_2027/releases/tag/v0.2.0", info.Url);
        Assert.Equal("New version 0.2.0 available: https://github.com/pwnzor11/RTS_2027/releases/tag/v0.2.0", UpdateCheck.NoticeText(info));
    }

    [Fact]
    public void NoNoticeWhenNotNewer() => Assert.Null(UpdateCheck.FromReleaseJson(Json, "0.2.0+abc"));

    [Theory]
    [InlineData("")]
    [InlineData("not json")]
    [InlineData("[]")]
    [InlineData("""{"message":"Not Found"}""")]
    [InlineData("""{"tag_name":"v9.0.0"}""")]
    [InlineData("""{"tag_name":5,"html_url":"https://github.com/x"}""")]
    [InlineData("""{"tag_name":"v9.0.0","html_url":"http://evil.example/x"}""")]
    public void BadJsonGivesNoNotice(string json) => Assert.Null(UpdateCheck.FromReleaseJson(json, "0.1.5"));

    [Fact]
    public void DisabledCheckerNeverFindsAnything()
    {
        var checker = new UpdateChecker();
        checker.Start(enabled: false, "0.1.5");
        Assert.Null(checker.Found);
    }
}
