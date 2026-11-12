// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using System.Diagnostics;
using System.Net;
using Rts.Net;
using Rts.Net.Udp;

namespace Rts.Net.Tests.Udp;

/// <summary>Real sockets on 127.0.0.1 with ephemeral ports. Loops only wait for a condition and fail after a generous deadline.</summary>
public class UdpTransportTests
{
    private static readonly Stopwatch Clock = Stopwatch.StartNew();
    private static long Now => Clock.ElapsedTicks * 1_000_000 / Stopwatch.Frequency;

    private static bool Pump(Func<bool> done, params UdpTransport[] all)
    {
        long deadline = Now + 10_000_000;
        while (Now < deadline)
        {
            foreach (UdpTransport t in all) t.Poll(Now);
            if (done()) return true;
            Thread.Yield();
        }
        return false;
    }

    private static NetMessage.Hello Hi(string name) => new(NetProtocol.Version, "test", 42UL, name, false);

    private static UdpTransport Join(UdpTransport host, string name) =>
        UdpTransport.Connect(new IPEndPoint(IPAddress.Loopback, host.LocalEndPoint.Port), Hi(name), 5_000);

    [Fact]
    public void HandshakeAcceptAndWelcome()
    {
        using var host = UdpTransport.Host(0, 5_000, IPAddress.Loopback);
        using var client = Join(host, "Ann");
        int pending = 0;
        Assert.True(Pump(() => host.TryReceive(out pending, out NetMessage m) && m is NetMessage.Hello { PlayerName: "Ann" }, host, client));
        Assert.True(pending < 0);
        Assert.True(host.Accept(pending, 1));
        host.Send(1, new NetMessage.Welcome(1));
        Assert.True(Pump(() => client.LocalPeer == 1, host, client));
        Assert.Equal(new[] { 1 }, host.Peers);
        Assert.Equal(new[] { 0 }, client.Peers);
        Assert.True(host.TryGetEvent(out PeerEvent ev) && ev is { Peer: 1, Connected: true });
        Assert.True(client.TryGetEvent(out ev) && ev is { Peer: 0, Connected: true });
        Assert.False(host.Accept(pending, 2));   // already promoted
    }

    [Fact]
    public void RejectReachesTheClientThenCloses()
    {
        using var host = UdpTransport.Host(0, 5_000, IPAddress.Loopback);
        using var client = Join(host, "Bob");
        int pending = 0;
        Assert.True(Pump(() => host.TryReceive(out pending, out _), host, client));
        host.Reject(pending, "Lobby full");
        NetMessage? got = null;
        Assert.True(Pump(() => client.TryReceive(out _, out got), host, client));
        Assert.Equal(new NetMessage.Reject("Lobby full"), got);
        Assert.True(Pump(() => client.TryGetEvent(out PeerEvent ev) && !ev.Connected, host, client));
        Assert.Empty(host.Peers);
    }

    [Fact]
    public void HostAndTwoClientsExchange500MessagesEachWayInOrder()
    {
        using var host = UdpTransport.Host(0, 5_000, IPAddress.Loopback);
        using var c1 = Join(host, "One");
        using var c2 = Join(host, "Two");
        var clients = new[] { c1, c2 };
        var ids = new Dictionary<string, int>();
        Assert.True(Pump(() =>
        {
            while (host.TryReceive(out int from, out NetMessage m))
                if (m is NetMessage.Hello h)
                {
                    int id = ids.Count + 1;
                    Assert.True(host.Accept(from, id));
                    ids[h.PlayerName] = id;
                    host.Send(id, new NetMessage.Welcome(id));
                }
            return ids.Count == 2 && c1.LocalPeer > 0 && c2.LocalPeer > 0;
        }, host, c1, c2));

        const int N = 500;
        for (int i = 0; i < N; i++)
        {
            host.Broadcast(new NetMessage.TurnBundle(i, Array.Empty<Rts.Sim.Core.Command>()));
            foreach (UdpTransport c in clients) c.Send(0, new NetMessage.Progress(i), reliable: false);   // may be lost
            foreach (UdpTransport c in clients) c.Send(0, new NetMessage.Ping(i));
        }
        var bundles = new[] { new List<int>(), new List<int>() };
        var pings = new Dictionary<int, List<long>> { [c1.LocalPeer] = new(), [c2.LocalPeer] = new() };
        bool Done()
        {
            for (int k = 0; k < 2; k++)
                while (clients[k].TryReceive(out _, out NetMessage m))
                    if (m is NetMessage.TurnBundle b) bundles[k].Add(b.Tick);
            while (host.TryReceive(out int from, out NetMessage m))
                if (m is NetMessage.Ping p) pings[from].Add(p.Stamp);
            return bundles[0].Count == N && bundles[1].Count == N && pings.Values.All(l => l.Count == N);
        }
        Assert.True(Pump(Done, host, c1, c2));
        Assert.Equal(Enumerable.Range(0, N), bundles[0]);
        Assert.Equal(Enumerable.Range(0, N), bundles[1]);
        foreach (var l in pings.Values) Assert.Equal(Enumerable.Range(0, N).Select(i => (long)i), l);
        Assert.True(host.RoundTripMs(1) >= 0);
    }

    [Fact]
    public void ByeEndsTheConnectionAndSilenceTimesOut()
    {
        using var host = UdpTransport.Host(0, 300, IPAddress.Loopback);
        var client = Join(host, "Cy");
        int pending = 0;
        Assert.True(Pump(() => host.TryReceive(out pending, out _), host, client));
        Assert.True(host.Accept(pending, 1));
        host.Send(1, new NetMessage.Welcome(1));
        Assert.True(Pump(() => client.LocalPeer == 1, host, client));
        while (host.TryGetEvent(out _)) { }

        client.Disconnect(0, "leaving");
        NetMessage? m = null;
        Assert.True(Pump(() => host.TryReceive(out _, out m) && m is NetMessage.Bye, host, client));
        Assert.True(Pump(() => host.TryGetEvent(out PeerEvent ev) && ev is { Peer: 1, Connected: false }, host, client));
        client.Dispose();

        // A second client that just goes quiet is dropped by the injected 300 ms timeout.
        var quiet = Join(host, "Quiet");
        Assert.True(Pump(() => host.TryReceive(out pending, out _), host, quiet));
        Assert.True(host.Accept(pending, 2));
        while (host.TryGetEvent(out _)) { }
        Assert.True(Pump(() => host.TryGetEvent(out PeerEvent ev) && ev is { Peer: 2, Connected: false, Reason: "timeout" }, host));
        quiet.Dispose();
    }

    [Fact]
    public void GarbageDatagramsDoNotCreatePeersOrThrow()
    {
        using var host = UdpTransport.Host(0, 5_000, IPAddress.Loopback);
        using var raw = new System.Net.Sockets.UdpClient();
        var target = new IPEndPoint(IPAddress.Loopback, host.LocalEndPoint.Port);
        var rng = new Random(3);
        for (int i = 0; i < 50; i++)
        {
            var junk = new byte[rng.Next(1, 200)];
            rng.NextBytes(junk);
            raw.Send(junk, junk.Length, target);
        }
        for (int i = 0; i < 2000; i++) host.Poll(Now);
        Assert.Empty(host.Peers);
        Assert.False(host.TryReceive(out _, out _));
    }
}
