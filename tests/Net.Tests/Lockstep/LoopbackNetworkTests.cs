// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Net;
using Rts.Net.Loopback;

namespace Net.Tests.Lockstep;

/// <summary>M9-03: the in-process network delivers on the caller's clock, keeps reliable order, and is repeatable for a seed.</summary>
public sealed class LoopbackNetworkTests
{
    private static List<(long At, int From, NetMessage Msg)> Drain(INetTransport t, long now, List<(long, int, NetMessage)> into)
    {
        t.Poll(now);
        while (t.TryReceive(out int from, out NetMessage m)) into.Add((now, from, m));
        return into;
    }

    [Fact]
    public void Clients_get_ids_in_join_order_and_both_sides_see_the_connection()
    {
        var net = new LoopbackNetwork();
        INetTransport a = net.AddClient(), b = net.AddClient();
        Assert.Equal(0, net.Host.LocalPeer);
        Assert.Equal(1, a.LocalPeer);
        Assert.Equal(2, b.LocalPeer);
        Assert.Equal(new[] { 1, 2 }, net.Host.Peers);
        Assert.True(net.Host.TryGetEvent(out PeerEvent e) && e.Connected && e.Peer == 1);
        Assert.True(a.TryGetEvent(out e) && e.Connected && e.Peer == 0);
    }

    [Fact]
    public void Messages_arrive_after_the_latency_on_the_callers_clock()
    {
        var net = new LoopbackNetwork { LatencyMs = 20 };
        INetTransport a = net.AddClient();
        net.Host.Poll(0);
        net.Host.Send(1, new NetMessage.Ping(1));
        var got = new List<(long, int, NetMessage)>();
        Drain(a, 19_999, got);
        Assert.Empty(got);
        Drain(a, 20_000, got);
        Assert.Single(got);
        Assert.Equal(0, got[0].Item2);
    }

    [Fact]
    public void Reliable_messages_keep_their_order_under_jitter_and_runs_repeat_for_a_seed()
    {
        static List<long> Run(ulong seed)
        {
            var net = new LoopbackNetwork(seed) { JitterMs = 40 };
            INetTransport a = net.AddClient();
            var got = new List<(long, int, NetMessage)>();
            for (int i = 0; i < 200; i++)
            {
                long now = i * 5_000L;
                net.Host.Poll(now);
                net.Host.Send(1, new NetMessage.Ping(i));
                Drain(a, now, got);
            }
            Drain(a, 10_000_000, got);
            return got.Select(g => ((NetMessage.Ping)g.Item3).Stamp * 1_000_000_000L + g.Item1).ToList();
        }
        List<long> one = Run(5), two = Run(5);
        Assert.Equal(one, two);
        Assert.Equal(200, one.Count);
        Assert.Equal(Enumerable.Range(0, 200).Select(i => (long)i), one.Select(v => v / 1_000_000_000L));
    }

    [Fact]
    public void Unreliable_loss_drops_only_unreliable_messages()
    {
        var net = new LoopbackNetwork(3) { UnreliableLossPercent = 50 };
        INetTransport a = net.AddClient();
        for (int i = 0; i < 100; i++)
        {
            a.Send(0, new NetMessage.Progress(i), reliable: false);
            a.Send(0, new NetMessage.Hash(i, 0, Array.Empty<ulong>()));
        }
        var got = Drain(net.Host, 0, new List<(long, int, NetMessage)>());
        Assert.Equal(100, got.Count(g => g.Msg is NetMessage.Hash));
        int progress = got.Count(g => g.Msg is NetMessage.Progress);
        Assert.InRange(progress, 20, 80);
        Assert.Equal(100 - progress, net.Lost);
    }

    [Fact]
    public void Disconnect_tells_both_sides_and_discards_messages_in_flight()
    {
        var net = new LoopbackNetwork { LatencyMs = 10 };
        INetTransport a = net.AddClient();
        while (net.Host.TryGetEvent(out _)) { }
        while (a.TryGetEvent(out _)) { }
        a.Send(0, new NetMessage.Ping(1));
        net.Disconnect(1, "gone");
        Assert.True(net.Host.TryGetEvent(out PeerEvent e) && !e.Connected && e.Peer == 1 && e.Reason == "gone");
        Assert.True(a.TryGetEvent(out e) && !e.Connected && e.Peer == 0);
        Assert.Empty(Drain(net.Host, 1_000_000, new List<(long, int, NetMessage)>()));
        net.Host.Send(1, new NetMessage.Ping(2));
        Assert.Empty(Drain(a, 2_000_000, new List<(long, int, NetMessage)>()));
        Assert.Empty(net.Host.Peers);
    }
}
