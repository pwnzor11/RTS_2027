// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using System.Diagnostics;
using System.Net;
using Rts.Game.Lan;
using Rts.Game.Lan.Lobby;
using Rts.Game.Screens;
using Rts.Net;
using Rts.Net.Udp;
using Xunit;

namespace Rts.Game.Tests.M9;

/// <summary>M9-05: join and start over real UDP sockets on 127.0.0.1 (ephemeral port). Loops wait for a condition and fail after a generous deadline.</summary>
public sealed class LobbyUdpTests : IDisposable
{
    private static readonly Stopwatch Clock = Stopwatch.StartNew();
    private readonly string _root = Path.Combine(Path.GetTempPath(), "rts_lobbyudp_" + Guid.NewGuid().ToString("N"));

    private static long Now => Clock.ElapsedTicks * 1_000_000 / Stopwatch.Frequency;

    public void Dispose()
    {
        try { Directory.Delete(_root, true); } catch (IOException) { }
    }

    private static bool Pump(Func<bool> done, LobbyHost host, params LobbyClient[] clients)
    {
        long deadline = Now + 15_000_000;
        while (Now < deadline)
        {
            host.Poll(Now);
            foreach (LobbyClient c in clients) c.Poll(Now);
            if (done()) return true;
            Thread.Yield();
        }

        return false;
    }

    private LobbyClient Connect(UdpTransport host, string name, ulong hash, string version) =>
        LobbyClient.Join(new IPEndPoint(IPAddress.Loopback, host.LocalEndPoint.Port),
            LobbyClient.MakeHello(name, false, hash, version), Path.Combine(_root, name, "maps"), Path.Combine(_root, name, "maps", "downloaded"));

    [Fact]
    public void JoinReadyAndStartOverUdp()
    {
        using UdpTransport transport = UdpTransport.Host(0, 20_000, IPAddress.Loopback);
        var host = new LobbyHost(transport, "Hosty", 5UL, "v", LobbyMap.Curated, Path.Combine(_root, "h"), Path.Combine(_root, "h", "d"), () => 99);
        LobbyClient ann = Connect(transport, "Ann", 5UL, "v");
        Assert.True(Pump(() => ann.State == LobbyClientState.InLobby && ann.Data is not null, host, ann), "joined");
        Assert.Equal(1, ann.LocalPeer);
        Assert.Equal("Ann", host.Data.Slots[1].Name);

        ann.Request(new SlotChange(Rts.Sim.Rules.Faction.Directorate, 4, 1));
        ann.SetReady(true);
        Assert.True(Pump(() => host.Data.Slots[1].Ready && host.Data.Slots[1].Slot.Faction == Rts.Sim.Rules.Faction.Directorate, host, ann), "slot request and ready");
        // the request cleared ready and the ready flag followed it; wait for the final state on the guest too
        Assert.True(Pump(() => ann.Data!.Slots[1].Ready, host, ann));

        LanMatchSetup? hostMatch = host.Start(out string? why);
        Assert.True(hostMatch is not null, why);
        Assert.True(Pump(() => ann.State == LobbyClientState.Started, host, ann), "start reached the guest");
        Assert.Equal(1, ann.Setup!.PeerSlots[1]);
        Assert.Equal(99UL, ann.Setup.Settings.SimSeed);
        Assert.Equal(hostMatch!.Settings.Slots, ann.Setup.Settings.Slots);
        ann.Transport.Dispose();
    }

    [Fact]
    public void ContentMismatchIsRejectedOverUdp()
    {
        using UdpTransport transport = UdpTransport.Host(0, 20_000, IPAddress.Loopback);
        var host = new LobbyHost(transport, "Hosty", 5UL, "v", LobbyMap.Curated, Path.Combine(_root, "h"), Path.Combine(_root, "h", "d"));
        LobbyClient bad = Connect(transport, "Bad", 6UL, "v");
        Assert.True(Pump(() => bad.State == LobbyClientState.Rejected, host, bad));
        Assert.Contains($"{5UL:x16}", bad.Reason);
        Assert.Contains($"{6UL:x16}", bad.Reason);
        Assert.Equal(1, host.Data.People);
        bad.Transport.Dispose();
    }
}
