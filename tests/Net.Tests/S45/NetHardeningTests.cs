// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using System.Net;
using System.Net.Sockets;
using System.Text;
using Rts.Net;
using Rts.Net.Diagnostics;
using Rts.Net.Discovery;
using Rts.Net.Lockstep;
using Rts.Net.Udp;

namespace Net.Tests.S45;

/// <summary>S45 net hardening: pending expiry (N-02), late Hello reject (N-03), discovery source filter (N-04), desync report limits (N-05). Loopback only.</summary>
public class NetHardeningTests
{
    private static NetMessage.Hello Hi(string name) => new(NetProtocol.Version, "test", 42UL, name, false);

    private static Socket RawSocket() => new(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);

    // A datagram that is a valid first packet of a fresh channel: use a real client transport's bytes instead of hand-building one.
    private static UdpTransport Join(UdpTransport host) =>
        UdpTransport.Connect(new IPEndPoint(IPAddress.Loopback, host.LocalEndPoint.Port), Hi("x"), 60_000);

    private static void PumpUntil(Func<bool> done, long fromMicros, params UdpTransport[] all)
    {
        long now = fromMicros;
        for (int i = 0; i < 4000 && !done(); i++)
        {
            foreach (UdpTransport t in all) t.Poll(now);
            Thread.Sleep(1);
        }
    }

    [Fact]
    public void PendingPeerExpiresWithoutHandshakeProgress()
    {
        using var host = UdpTransport.Host(0, 60_000, IPAddress.Loopback, pendingTimeoutMs: 10_000);
        using var client = Join(host);
        long t0 = 1_000_000;
        // Hello arrives and is surfaced, but nobody accepts it: the peer sits pending.
        PumpUntil(() => host.TryReceive(out int _, out NetMessage _), t0, host, client);
        host.Poll(t0 + 11_000_000);
        Assert.False(host.Accept(-2, 1));   // expired: no pending peer left
    }

    [Fact]
    public void PendingPeerIsStillAcceptableBeforeTheTimeout()
    {
        using var host = UdpTransport.Host(0, 60_000, IPAddress.Loopback, pendingTimeoutMs: 10_000);
        using var client = Join(host);
        PumpUntil(() => host.TryReceive(out int _, out NetMessage _), 1_000_000, host, client);
        host.Poll(1_000_000 + 9_000_000);
        Assert.True(host.Accept(-2, 1));
    }

    [Fact]
    public void OneSourceAddressTakesOnePendingSlot()
    {
        using var host = UdpTransport.Host(0, 60_000, IPAddress.Loopback);
        using var a = Join(host);
        using var b = Join(host);   // same source address (127.0.0.1), different port
        long now = 1_000_000;
        var hellos = new List<int>();
        for (int i = 0; i < 300; i++)
        {
            host.Poll(now); a.Poll(now); b.Poll(now);
            while (host.TryReceive(out int from, out NetMessage m)) if (m is NetMessage.Hello) hellos.Add(from);
            Thread.Sleep(1);
        }
        Assert.Single(hellos.Distinct());
    }

    [Fact]
    public void LateHelloGetsAMatchInProgressReject()
    {
        using var host = UdpTransport.Host(0, 60_000, IPAddress.Loopback);
        var lockstep = new LockstepHost(host, 3, (_, _) => { });
        using var client = Join(host);
        NetMessage.Reject? reject = null;
        long now = 1_000_000;
        for (int i = 0; i < 4000 && reject is null; i++)
        {
            lockstep.Update(now);
            client.Poll(now);
            while (client.TryReceive(out int _, out NetMessage m)) if (m is NetMessage.Reject r) reject = r;
            now += 1000;
            Thread.Sleep(1);
        }
        Assert.NotNull(reject);
        Assert.Equal(LockstepHost.MatchInProgressText, reject!.Reason);
        Assert.Contains("started", reject.Reason);
    }

    [Theory]
    [InlineData("8.8.8.8", false)]
    [InlineData("203.0.113.9", false)]
    [InlineData("172.32.0.1", false)]
    [InlineData("127.0.0.1", true)]
    [InlineData("192.168.1.20", true)]
    [InlineData("10.1.2.3", true)]
    [InlineData("172.20.0.5", true)]
    [InlineData("169.254.7.7", true)]
    [InlineData("192.168.9.9", false)]   // private, but not on a local subnet below
    public void DiscoveryAnswersOnlyLocalPrivateSources(string source, bool expected)
    {
        var locals = new List<(IPAddress, IPAddress)>
        {
            (IPAddress.Parse("192.168.1.5"), IPAddress.Parse("255.255.255.0")),
            (IPAddress.Parse("10.0.0.2"), IPAddress.Parse("255.0.0.0")),
            (IPAddress.Parse("172.16.0.2"), IPAddress.Parse("255.240.0.0")),
            (IPAddress.Parse("169.254.1.1"), IPAddress.Parse("255.255.0.0")),
        };
        Assert.Equal(expected, DiscoveryResponder.IsAllowedSource(IPAddress.Parse(source), locals));
    }

    [Fact]
    public void DiscoveryRejectsIpv6AndMappedPublicSources()
    {
        var none = new List<(IPAddress, IPAddress)>();
        Assert.False(DiscoveryResponder.IsAllowedSource(IPAddress.IPv6Loopback, none));
        Assert.False(DiscoveryResponder.IsAllowedSource(IPAddress.Parse("::ffff:8.8.8.8"), none));
        Assert.True(DiscoveryResponder.IsAllowedSource(IPAddress.Parse("::ffff:127.0.0.1"), none));
    }

    [Fact]
    public void DiscoveryReplyHasOnlyTheChosenNameAndRepliesAreRateLimited()
    {
        string os = Environment.UserName;
        var info = new DiscoveryInfo(DiscoveryInfo.GameName, NetProtocol.Version, "Chosen Name", 1, 4, NetProtocol.DefaultGamePort, true, false);
        using var responder = new DiscoveryResponder(info, 0, IPAddress.Loopback);
        using var s = RawSocket();
        s.ReceiveTimeout = 2000;
        var target = new IPEndPoint(IPAddress.Loopback, responder.LocalEndPoint.Port);
        byte[] probe = new byte[] { (byte)'R', (byte)'T', (byte)'S', (byte)'Q', 8 }.Concat(Encoding.UTF8.GetBytes(DiscoveryInfo.GameName)).ToArray();
        for (int i = 0; i < 3; i++) s.SendTo(probe, target);
        Thread.Sleep(100);
        Assert.Equal(1, responder.Poll());   // three probes at once from one source: one reply
        var buf = new byte[512];
        int n = s.Receive(buf);
        string text = Encoding.UTF8.GetString(buf, 0, n);
        Assert.Contains("Chosen Name", text);
        if (os.Length >= 3 && !"Chosen Name RTS_2027".Contains(os, StringComparison.OrdinalIgnoreCase))
            Assert.DoesNotContain(os, text, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(0, responder.Poll());
    }

    [Fact]
    public void DesyncReportIsWrittenOncePerTick()
    {
        var gate = new DesyncReportFolders();
        Assert.True(gate.IsFirstReport(160));
        Assert.False(gate.IsFirstReport(160));
        Assert.True(gate.IsFirstReport(176));
    }

    [Fact]
    public void DesyncReportFoldersAreCappedToTheNewest()
    {
        string root = Path.Combine(Path.GetTempPath(), "rts-s45-desync-" + Guid.NewGuid().ToString("N"));
        try
        {
            for (int i = 0; i < 15; i++) Directory.CreateDirectory(Path.Combine(root, $"20261125-1000{i:00}"));
            Directory.CreateDirectory(Path.Combine(root, "20261125-100014-2"));
            Directory.CreateDirectory(Path.Combine(root, "not-a-report"));
            DesyncReportFolders.Prune(root, 10);
            string[] left = Directory.GetDirectories(root).Select(d => Path.GetFileName(d)!).OrderBy(x => x, StringComparer.Ordinal).ToArray();
            Assert.Equal(11, left.Length);   // 10 reports + the unrelated folder
            Assert.Contains("not-a-report", left);
            Assert.Contains("20261125-100014-2", left);
            Assert.DoesNotContain("20261125-100000", left);
            Assert.DoesNotContain("20261125-100005", left);
            Assert.Contains("20261125-100006", left);
        }
        finally { try { Directory.Delete(root, true); } catch (IOException) { } }
    }
}
