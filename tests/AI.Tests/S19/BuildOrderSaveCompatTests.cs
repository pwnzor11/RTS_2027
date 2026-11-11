// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.AI.Recruit;
using Xunit;

namespace AI.Tests.S19;

/// <summary>M13-06: a session 18 AI snapshot (no ready-cancel fields) still restores, with the new fields at their defaults.</summary>
public sealed class BuildOrderSaveCompatTests
{
    [Fact]
    public void OldLayoutSnapshot_Restores_WithTheNewFieldsDefaulted_AndLeavesTheReaderAtTheNextPart()
    {
        var ms = new MemoryStream();
        using (var w = new BinaryWriter(ms, System.Text.Encoding.UTF8, true))
        {
            w.Write(2); w.Write("a_hq"); w.Write("a_power");   // order
            w.Write(5); w.Write(3); w.Write(750);             // Placed, Unplaceable, PendingCost
            w.Write(0x5EED);                                  // whatever the next manager writes
        }
        ms.Position = 0;
        var m = new BuildOrderManager(new[] { "a_hq" }, "a_power", "a_hq");
        using var r = new BinaryReader(ms);
        m.Restore(r, legacy: true);
        Assert.Equal((5, 3, 750, 0), (m.Placed, m.Unplaceable, m.PendingCost, m.Cancelled));
        Assert.Equal(0x5EED, r.ReadInt32());
    }

    [Fact]
    public void NewLayoutSnapshot_RoundTrips()
    {
        var a = new BuildOrderManager(new[] { "a_hq" }, "a_power", "a_hq");
        var ms = new MemoryStream();
        using (var w = new BinaryWriter(ms, System.Text.Encoding.UTF8, true)) a.Snapshot(w);
        ms.Position = 0;
        var b = new BuildOrderManager(new[] { "a_hq" }, "a_power", "a_hq");
        using var r = new BinaryReader(ms);
        b.Restore(r);
        Assert.Equal(ms.Length, ms.Position);
    }
}
