// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using System.Diagnostics;
using System.Net;
using Rts.Net;
using Rts.Net.Udp;

namespace Rts.Net.Tests.Udp;

/// <summary>
/// M9-14 finding 1: the lobby and the match poll one transport; if the caller's clock jumps (the lobby's clock started at 0, the match's
/// at app start) by more than the timeout between two polls, nobody may be dropped and the RTT must stay sane. Real sockets on 127.0.0.1;
/// the transport clock is a virtual one the test moves, loops wait for a condition with a generous real-time deadline.
/// </summary>
public sealed class UdpClockJumpTests
{
    private static readonly Stopwatch Real = Stopwatch.StartNew();
    private long _now = 1_000;

    private bool Pump(Func<bool> done, params UdpTransport[] all)
    {
        long deadline = Real.ElapsedMilliseconds + 10_000;
        while (Real.ElapsedMilliseconds < deadline)
        {
            _now += 1_000;
            foreach (UdpTransport t in all) t.Poll(_now);
            if (done()) return true;
            Thread.Yield();
        }
        return false;
    }

    [Fact]
    public void A_clock_jump_longer_than_the_timeout_drops_nobody_and_keeps_the_rtt_sane()
    {
        using var host = UdpTransport.Host(0, 5_000, IPAddress.Loopback);
        using var client = UdpTransport.Connect(new IPEndPoint(IPAddress.Loopback, host.LocalEndPoint.Port),
            new NetMessage.Hello(NetProtocol.Version, "test", 1UL, "Ann", false), 5_000);
        int pending = 0;
        Assert.True(Pump(() => host.TryReceive(out pending, out _), host, client));
        Assert.True(host.Accept(pending, 1));
        host.Send(1, new NetMessage.Welcome(1));
        Assert.True(Pump(() => client.LocalPeer == 1, host, client));
        for (int i = 0; i < 200; i++) Pump(() => true, host, client);   // the "lobby": a while of normal traffic on the small clock
        while (host.TryGetEvent(out _)) { }
        while (client.TryGetEvent(out _)) { }
        while (host.TryReceive(out _, out _)) { }
        while (client.TryReceive(out _, out _)) { }

        // the host sends something reliable just before the handover, then goes quiet; the client reads all of it on the small clock
        host.Send(1, new NetMessage.Ping(7));
        host.Poll(_now);
        for (int i = 0; i < 50; i++) client.Poll(_now);

        // the "match" polls on a clock 60 s ahead (> 5 s timeout): the client polls first, so nothing from the host arrives in that poll
        _now += 60_000_000;
        client.Poll(_now);
        Assert.False(client.TryGetEvent(out PeerEvent early), $"client dropped the host at the handover: {early.Reason}");

        for (int i = 0; i < 300; i++) Pump(() => true, host, client);
        Assert.False(host.TryGetEvent(out PeerEvent ev), $"host dropped a peer: {ev.Reason}");
        Assert.False(client.TryGetEvent(out ev), $"client dropped the host: {ev.Reason}");
        Assert.Equal(new[] { 1 }, host.Peers);
        Assert.Equal(new[] { 0 }, client.Peers);
        Assert.InRange(host.RoundTripMs(1), 0, 1_000);
        Assert.InRange(client.RoundTripMs(0), 0, 1_000);
    }
}
