// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using System.Diagnostics;
using System.Net;
using Rts.Net;
using Rts.Net.Discovery;

namespace Rts.Net.Tests.Discovery;

public class DiscoveryTests
{
    private static readonly Stopwatch Clock = Stopwatch.StartNew();
    private static long Now => Clock.ElapsedTicks * 1_000_000 / Stopwatch.Frequency;

    private static DiscoveryInfo Info(string name = "Cellar game", int players = 2, bool open = true) =>
        new(DiscoveryInfo.GameName, NetProtocol.Version, name, players, 4, NetProtocol.DefaultGamePort, open, false);

    private static bool Pump(DiscoveryResponder r, DiscoveryProbe p, Func<bool> done)
    {
        long deadline = Now + 10_000_000;
        while (Now < deadline)
        {
            r.Poll();
            p.Poll(Now);
            if (done()) return true;
            Thread.Yield();
        }
        return false;
    }

    [Fact]
    public void ProbeFindsTheHostOnLoopbackAndDeduplicates()
    {
        using var responder = new DiscoveryResponder(Info(), 0, IPAddress.Loopback);
        var target = new IPEndPoint(IPAddress.Loopback, responder.LocalEndPoint.Port);
        // The same host listed twice, as happens when it hears the broadcast and the multicast probe.
        using var probe = new DiscoveryProbe(0, new[] { target, target });
        probe.Start(Now, 60_000);
        Assert.True(Pump(responder, probe, () => probe.Results.Count >= 1));
        for (int i = 0; i < 200; i++) { responder.Poll(); probe.Poll(Now); }
        DiscoveredHost h = Assert.Single(probe.Results);
        Assert.Equal(new IPEndPoint(IPAddress.Loopback, NetProtocol.DefaultGamePort), h.GameEndPoint);
        Assert.Equal("Cellar game", h.Info.LobbyName);
        Assert.Equal(2, h.Info.Players);
        Assert.Equal(4, h.Info.MaxPlayers);
        Assert.True(h.Info.Open);
        Assert.False(h.Info.Started);
        Assert.True(h.Info.Compatible);
        Assert.Equal("RTS_2027", h.Info.GameId);
    }

    [Fact]
    public void RepliesTrackTheCurrentInfoAndWindowCompletes()
    {
        using var responder = new DiscoveryResponder(Info(), 0, IPAddress.Loopback);
        var target = new IPEndPoint(IPAddress.Loopback, responder.LocalEndPoint.Port);
        using var probe = new DiscoveryProbe(0, new[] { target });
        responder.Info = Info("Changed", 3, open: false);
        probe.Start(0, 1000);
        Assert.True(Pump(responder, probe, () => probe.Results.Count == 1));
        Assert.Equal("Changed", probe.Results[0].Info.LobbyName);
        Assert.False(probe.Results[0].Info.Open);
        Assert.False(probe.IsComplete);
        probe.Poll(2_000_000);
        Assert.True(probe.IsComplete);
    }

    [Fact]
    public void LongNamesAreClippedAndJunkIsIgnored()
    {
        using var responder = new DiscoveryResponder(Info(new string('x', 300)), 0, IPAddress.Loopback);
        var target = new IPEndPoint(IPAddress.Loopback, responder.LocalEndPoint.Port);
        using var junk = new System.Net.Sockets.UdpClient();
        var rng = new Random(8);
        for (int i = 0; i < 30; i++) { var b = new byte[rng.Next(1, 100)]; rng.NextBytes(b); junk.Send(b, b.Length, target); }
        using var probe = new DiscoveryProbe(0, new[] { target });
        probe.Start(Now, 60_000);
        Assert.True(Pump(responder, probe, () => probe.Results.Count == 1));
        Assert.Equal(64, probe.Results[0].Info.LobbyName.Length);
    }

    [Fact]
    public void DefaultTargetsIncludeLimitedBroadcastAndMulticast()
    {
        var targets = DiscoveryProbe.DefaultTargets(4711);
        Assert.Contains(new IPEndPoint(IPAddress.Broadcast, 4711), targets);
        Assert.Contains(new IPEndPoint(IPAddress.Parse("239.255.42.71"), 4711), targets);
    }
}
