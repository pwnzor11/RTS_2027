// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
namespace Rts.Game.Tests.S18;

/// <summary>S18 GAMEREND-03: the diagnostics log is one small size-capped file that never throws.</summary>
public sealed class RenderAppLogTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "rts_s18_log_" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        try { Directory.Delete(_dir, true); }
        catch (IOException) { }
    }

    [Fact]
    public void Lines_are_written_with_a_timestamp_and_the_folder_is_created()
    {
        var log = new AppLog(Path.Combine(_dir, "logs", "x.log"));
        Assert.True(log.Write("audio disabled: no device"));
        string text = File.ReadAllText(log.Path);
        Assert.Contains("audio disabled: no device", text);
        Assert.Matches(@"^\d{4}-\d\d-\d\d \d\d:\d\d:\d\d ", text);
    }

    [Fact]
    public void The_file_never_grows_past_the_cap_plus_one_line_and_one_old_copy_is_kept()
    {
        var log = new AppLog(Path.Combine(_dir, "x.log"), maxBytes: 400);
        for (int i = 0; i < 200; i++) Assert.True(log.Write("line number " + i));
        Assert.True(new FileInfo(log.Path).Length < 400 + 100);
        Assert.True(File.Exists(log.Path + ".old"));
        Assert.True(new FileInfo(log.Path + ".old").Length < 400 + 100);
        Assert.Contains("line number 199", File.ReadAllText(log.Path));
        Assert.False(File.Exists(log.Path + ".old.old"));
    }

    [Fact]
    public void An_unwritable_path_returns_false_instead_of_throwing()
    {
        string file = Path.Combine(_dir, "afile");
        Directory.CreateDirectory(_dir);
        File.WriteAllText(file, "x");
        var log = new AppLog(Path.Combine(file, "sub", "x.log"));   // a folder below a regular file cannot be created
        Assert.False(log.Write("hello"));
    }

    [Fact]
    public void The_default_path_sits_beside_the_settings_file()
    {
        Assert.Equal(Path.GetDirectoryName(Settings.DefaultPath), Path.GetDirectoryName(Path.GetDirectoryName(AppLog.DefaultPath)));
        Assert.Equal("logs", Path.GetFileName(Path.GetDirectoryName(AppLog.DefaultPath)));
    }
}
