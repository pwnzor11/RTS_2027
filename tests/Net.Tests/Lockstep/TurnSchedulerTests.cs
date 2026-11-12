// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Net.Lockstep;
using Rts.Sim.Core;

namespace Net.Tests.Lockstep;

/// <summary>M9-03: the host stamps ExecTick and Sequence, seals in a fixed order and refuses commands for someone else's slot.</summary>
public sealed class TurnSchedulerTests
{
    private static Command Cmd(byte player, int execTick = 999, ushort seq = 777, int arg = 0) =>
        new(execTick, player, seq, CommandKind.Stop, EntityId.None, EntityId.None, default, arg);

    [Fact]
    public void Submit_stamps_tick_plus_delay_and_host_owned_sequences()
    {
        var s = new TurnScheduler(3);
        s.SetSlot(1, 1);
        Assert.True(s.Submit(1, Cmd(1), 10));
        Assert.True(s.Submit(1, Cmd(1, 0, 5), 10));
        for (int t = 3; t < 13; t++) Assert.Empty(s.Seal(t));
        Command[] sealedCmds = s.Seal(13);
        Assert.Equal(2, sealedCmds.Length);
        Assert.All(sealedCmds, c => Assert.Equal(13, c.ExecTick));
        Assert.Equal(new ushort[] { 0, 1 }, sealedCmds.Select(c => c.Sequence).ToArray());
    }

    [Fact]
    public void Seal_orders_by_player_then_sequence_whatever_the_arrival_order()
    {
        var s = new TurnScheduler(2);
        s.SubmitTrusted(Cmd(3, arg: 1), 0);
        s.SubmitTrusted(Cmd(0, arg: 2), 0);
        s.SubmitTrusted(Cmd(3, arg: 3), 0);
        s.SubmitTrusted(Cmd(1, arg: 4), 0);
        s.SubmitTrusted(Cmd(0, arg: 5), 0);
        Command[] b = s.Seal(2);
        Assert.Equal(new[] { 2, 5, 4, 1, 3 }, b.Select(c => c.Arg).ToArray());
        Assert.Equal(new byte[] { 0, 0, 1, 3, 3 }, b.Select(c => c.Player).ToArray());
    }

    [Fact]
    public void A_peer_may_only_command_its_own_slot()
    {
        var s = new TurnScheduler(3);
        s.SetSlot(1, 1);
        Assert.False(s.Submit(1, Cmd(2), 0));   // someone else's player
        Assert.False(s.Submit(2, Cmd(2), 0));   // no slot at all
        Assert.True(s.Submit(1, Cmd(1), 0));
        s.ClearSlot(1);
        Assert.False(s.Submit(1, Cmd(1), 0));   // dropped
        Assert.Equal(3, s.Rejected);
        Assert.Equal(1, s.PendingCount);
    }

    [Fact]
    public void A_command_never_lands_on_a_sealed_tick()
    {
        var s = new TurnScheduler(2);
        for (int t = 2; t <= 9; t++) s.Seal(t);   // e.g. pause drained, host still at an older tick
        Command c = s.SubmitTrusted(Cmd(0), 4);
        Assert.Equal(10, c.ExecTick);
    }

    [Fact]
    public void Seal_must_go_in_order()
    {
        var s = new TurnScheduler(3);
        Assert.Equal(2, s.SealedThrough);
        Assert.Throws<InvalidOperationException>(() => s.Seal(4));
        s.Seal(3);
        Assert.Throws<InvalidOperationException>(() => s.Seal(3));
    }
}
