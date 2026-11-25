// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Sim.Core;
using Rts.Sim.Replay;
using Rts.Sim.Tests.Determinism;
using Rts.Sim.Tests.Replay;

namespace Rts.Sim.Tests.S45;

/// <summary>N-01 (Q-M9-g): chat lines are kept in the replay (format 16) outside the sim; playback and hashes are unaffected.</summary>
public class ReplayChatTests
{
    private static byte[] Record(SimSetup setup, int ticks, bool withChat)
    {
        var script = ScriptedGame.Script(ticks);
        var sim = ScriptedGame.NewSim(setup);
        var ms = new MemoryStream();
        var rec = new ReplayWriter(ms, sim, "test-build", ReplayTests.RulesFiles);
        foreach (var c in script) rec.RecordCommand(c);
        for (int i = 0; i < ticks; i++)
        {
            sim.Step(script);
            if (withChat && sim.Tick == 30) rec.RecordChat(sim.Tick, 1, "gg wp");
            if (withChat && sim.Tick == 31) rec.RecordChat(sim.Tick, -1, "spectator says hi");
            if (withChat && sim.Tick == 200) rec.RecordChat(sim.Tick, 0, new string('x', 2000));
            if (sim.Tick % SimConstants.HashIntervalTicks == 0) rec.RecordHash(sim);
        }
        rec.Finish();
        return ms.ToArray();
    }

    [Fact]
    public void ChatLines_RoundTripThroughAReplay()
    {
        var setup = ScriptedGame.Setup();
        ReplayData data = ReplayReader.Read(new MemoryStream(Record(setup, 300, withChat: true)));
        Assert.Equal(16, data.Version);
        Assert.Equal(3, data.Chat.Count);
        Assert.Equal(new ReplayChatLine(30, 1, "gg wp"), data.Chat[0]);
        Assert.Equal(new ReplayChatLine(31, ReplayWriter.NoSlot, "spectator says hi"), data.Chat[1]);
        Assert.Equal(ReplayWriter.MaxChatChars, data.Chat[2].Text.Length);
        Assert.DoesNotContain(data.Records, r => r.Type == ReplayWriter.TypeChat);   // chat is not a sim record
        Assert.False(data.Truncated);
    }

    [Fact]
    public void ChatDoesNotChangePlaybackOrHashes()
    {
        var setup = ScriptedGame.Setup();
        ReplayResult plain = ReplayPlayer.Run(new MemoryStream(Record(setup, 400, withChat: false)), setup);
        ReplayResult chatty = ReplayPlayer.Run(new MemoryStream(Record(setup, 400, withChat: true)), setup);
        Assert.Equal(-1, chatty.FirstMismatchTick);
        Assert.Equal(plain.FinalHash, chatty.FinalHash);
        Assert.Equal(plain.TicksRun, chatty.TicksRun);
    }

    [Fact]
    public void ReplayCutInsideAChatLine_StillReadsTheEarlierRecords()
    {
        var setup = ScriptedGame.Setup();
        byte[] bytes = Record(setup, 100, withChat: true);
        ReplayData data = ReplayReader.Read(new MemoryStream(bytes[..^40]));
        Assert.True(data.Truncated);
        Assert.NotEmpty(data.Chat);
    }

    [Fact]
    public void Version15Replay_IsRefused()
    {
        byte[] bytes = Record(ScriptedGame.Setup(), 40, withChat: false);
        bytes[4] = 15;
        var ex = Assert.Throws<InvalidDataException>(() => ReplayReader.Read(new MemoryStream(bytes)));
        Assert.Contains("version 15", ex.Message);
    }
}
