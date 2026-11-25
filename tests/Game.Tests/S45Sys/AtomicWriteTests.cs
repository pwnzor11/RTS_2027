// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Game.Campaign;

namespace Rts.Game.Tests.S45Sys;

/// <summary>U-10 (GAMESYS-08): atomic writes are flushed to disk before the rename, and leave no temporary file behind.</summary>
public sealed class AtomicWriteTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "rts_s45aw_" + Guid.NewGuid().ToString("N"));

    public AtomicWriteTests() => Directory.CreateDirectory(_dir);

    public void Dispose()
    {
        if (Directory.Exists(_dir)) Directory.Delete(_dir, recursive: true);
    }

    [Fact]
    public void The_temporary_file_is_complete_on_disk_before_the_rename()
    {
        string file = Path.Combine(_dir, "target.toml");
        File.WriteAllText(file, "old");
        string? seenTmp = null;
        string? seenText = null;
        bool oldStillThere = false;

        AtomicFile.Write(file, "new text\n", tmp =>
        {
            seenTmp = tmp;
            seenText = File.ReadAllText(tmp);   // the data is already there when the rename is about to happen
            oldStillThere = File.ReadAllText(file) == "old";
        });

        Assert.NotNull(seenTmp);
        Assert.Equal("new text\n", seenText);
        Assert.True(oldStillThere, "the target is untouched until the rename");
        Assert.Equal("new text\n", File.ReadAllText(file));
        Assert.False(File.Exists(seenTmp));
    }

    [Fact]
    public void Writing_without_a_hook_replaces_the_file_and_writes_no_byte_order_mark()
    {
        string file = Path.Combine(_dir, "plain.toml");
        AtomicFile.WriteAllText(file, "é");
        byte[] bytes = File.ReadAllBytes(file);
        Assert.Equal(new byte[] { 0xC3, 0xA9 }, bytes);
        Assert.False(File.Exists(file + ".tmp"));
    }

    [Fact]
    public void Campaign_progress_saves_through_the_atomic_write_and_reads_back()
    {
        string file = Path.Combine(_dir, "campaign_authority.toml");
        CampaignProgress p = CampaignProgress.New("authority").WithWin("m1", 2, new[] { "tech_a" });

        Assert.True(p.Save(file));

        CampaignProgress back = CampaignProgress.Load("authority", file);
        Assert.Equal(new[] { "m1" }, back.Completed);
        Assert.Equal(2, back.Difficulty["m1"]);
        Assert.False(File.Exists(file + ".tmp"));
    }
}
