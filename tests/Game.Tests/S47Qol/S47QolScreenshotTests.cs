// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
namespace Rts.Game.Tests.S47Qol;

/// <summary>S47 U-13: screenshot file naming (the capture itself needs a graphics device).</summary>
public sealed class S47QolScreenshotTests
{
    [Fact]
    public void The_name_is_the_local_time()
    {
        Assert.Equal("rts2027_20261126_142530.png", Screenshots.FileName(new DateTime(2026, 11, 26, 14, 25, 30)));
    }

    [Fact]
    public void A_second_shot_in_the_same_second_gets_a_number()
    {
        string dir = Path.Combine(Path.GetTempPath(), "s47qol_shots_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            var t = new DateTime(2026, 11, 26, 14, 25, 30);
            string first = Screenshots.FreePath(dir, t);
            Assert.Equal("rts2027_20261126_142530.png", Path.GetFileName(first));
            File.WriteAllBytes(first, new byte[] { 1 });
            Assert.Equal("rts2027_20261126_142530_2.png", Path.GetFileName(Screenshots.FreePath(dir, t)));
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }
}
