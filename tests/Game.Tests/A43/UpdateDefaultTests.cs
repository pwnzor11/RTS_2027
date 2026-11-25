// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Game.Update;

namespace Rts.Game.Tests.A43;

/// <summary>A43 GAMESYS-02 (D-312): the update check is off by default, "Check now" runs one check on request, and off means no request at start.</summary>
public sealed class UpdateDefaultTests
{
    private const string NewerJson = "{\"tag_name\":\"v9.0.0\",\"html_url\":\"https://github.com/pwnzor11/RTS_2027/releases/tag/v9.0.0\"}";
    private const string SameJson = "{\"tag_name\":\"v0.1.0\",\"html_url\":\"https://github.com/pwnzor11/RTS_2027/releases/tag/v0.1.0\"}";

    [Fact]
    public void CheckForUpdates_DefaultsToFalse()
    {
        Assert.False(Settings.Default.CheckForUpdates);
        Assert.False(Settings.Parse("version = 1\nfullscreen = true").CheckForUpdates);
    }

    [Fact]
    public void CheckForUpdates_StoredValueIsKept()
    {
        Assert.True(Settings.Parse("check_for_updates = true").CheckForUpdates);
        Assert.False(Settings.Parse("check_for_updates = false").CheckForUpdates);
    }

    [Fact]
    public void Start_WhenOff_MakesNoRequest()
    {
        int calls = 0;
        var checker = new UpdateChecker(_ => { calls++; return Task.FromResult<string?>(NewerJson); });
        checker.Start(false, "0.1.0");
        Assert.Equal(0, calls);
        Assert.Equal(UpdateCheckStatus.Idle, checker.Status);
        Assert.Null(checker.Found);
    }

    [Fact]
    public async Task CheckAsync_NewerRelease_IsAvailableAndFound()
    {
        var checker = new UpdateChecker(_ => Task.FromResult<string?>(NewerJson));
        await checker.CheckAsync("0.1.0");
        Assert.Equal(UpdateCheckStatus.Available, checker.Status);
        Assert.Equal("9.0.0", checker.Found?.Version);
    }

    [Fact]
    public async Task CheckAsync_SameRelease_IsUpToDate()
    {
        var checker = new UpdateChecker(_ => Task.FromResult<string?>(SameJson));
        await checker.CheckAsync("0.1.0");
        Assert.Equal(UpdateCheckStatus.UpToDate, checker.Status);
        Assert.Null(checker.Found);
    }

    [Fact]
    public async Task CheckAsync_NoAnswer_Fails()
    {
        var checker = new UpdateChecker(_ => Task.FromResult<string?>(null));
        await checker.CheckAsync("0.1.0");
        Assert.Equal(UpdateCheckStatus.Failed, checker.Status);
    }

    [Fact]
    public async Task CheckAsync_UnreadableVersion_MakesNoRequest()
    {
        int calls = 0;
        var checker = new UpdateChecker(_ => { calls++; return Task.FromResult<string?>(NewerJson); });
        await checker.CheckAsync("dev");
        Assert.Equal(0, calls);
        Assert.Equal(UpdateCheckStatus.NoVersion, checker.Status);
    }

    [Fact]
    public void StatusText_GivesTheLineForEachState()
    {
        Assert.Equal("", UpdateCheck.StatusText(UpdateCheckStatus.Idle, null, "0.1.0"));
        Assert.Equal("Up to date: version 0.1.0 is the latest release.", UpdateCheck.StatusText(UpdateCheckStatus.UpToDate, null, "0.1.0"));
        Assert.Equal("Could not reach GitHub. Try again later.", UpdateCheck.StatusText(UpdateCheckStatus.Failed, null, "0.1.0"));
    }
}
