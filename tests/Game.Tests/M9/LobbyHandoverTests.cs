// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using System.Diagnostics;
using System.Net;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Rts.Data;
using Rts.Game.Audio;
using Rts.Game.Input;
using Rts.Game.Lan;
using Rts.Game.Lan.Lobby;
using Rts.Game.Screens;
using Rts.Net;
using Rts.Net.Udp;

namespace Rts.Game.Tests.M9;

/// <summary>M9-14 review findings 1, 4 and 7: the lobby to match handover (one clock, a failed start, a host that vanishes at the start).</summary>
[Collection("UiStatics")]
public sealed class LobbyHandoverTests
{
    private static readonly string RulesDir = RulesLocator.FindRulesDir(AppContext.BaseDirectory) ?? throw new InvalidOperationException("data/rules not found");

    private sealed class Dummy : IScreen
    {
        public bool BlocksUpdateBelow => true;
        public bool BlocksDrawBelow => true;
        public void Enter() { }
        public void Exit() { }
        public void Update(InputFrame input, GameTime t) { }
        public void Draw(SpriteBatch sb) { }
    }

    private sealed class Recorder : INetTransport
    {
        private readonly INetTransport _inner;
        public Recorder(INetTransport inner) => _inner = inner;
        public long LastPoll = -1;
        public int LocalPeer => _inner.LocalPeer;
        public IReadOnlyList<int> Peers => _inner.Peers;
        public void Poll(long nowMicroseconds) { LastPoll = nowMicroseconds; _inner.Poll(nowMicroseconds); }
        public void Send(int peer, NetMessage message, bool reliable = true) => _inner.Send(peer, message, reliable);
        public void Broadcast(NetMessage message, bool reliable = true) => _inner.Broadcast(message, reliable);
        public bool TryReceive(out int fromPeer, out NetMessage message) => _inner.TryReceive(out fromPeer, out message);
        public bool TryGetEvent(out PeerEvent peerEvent) => _inner.TryGetEvent(out peerEvent);
        public int RoundTripMs(int peer) => _inner.RoundTripMs(peer);
        public void Dispose() => _inner.Dispose();
    }

    private static (ScreenStack Stack, GameServices S) Services()
    {
        var stack = new ScreenStack();
        var s = new GameServices(null!, null!, null!, RulesDir, NullAudio.Instance, new Settings(), stack, () => { })
        {
            ViewportOverride = () => new Viewport(0, 0, 1280, 720),
        };
        return (stack, s);
    }

    private static InputFrame NoInput() => new(new KeyboardState(), new KeyboardState(), new MouseState(), new MouseState());

    [Fact]
    public void The_lobby_polls_the_transport_on_the_shared_network_clock_not_its_own()
    {
        using var rig = new LobbyRig();
        var rec = new Recorder(rig.Net.AddClient());
        var ann = new LobbyClient(rec, LobbyClient.MakeHello("Ann", false, LobbyRig.Hash, LobbyRig.Version), rig.NextGuestUserDir, rig.GuestDownDir(0));
        (ScreenStack stack, GameServices s) = Services();
        s.NetMicros = () => 987_654_321;   // the clock the match will go on with
        stack.Push(new Dummy());
        stack.Push(new LobbyScreen(s, ann));
        stack.Update(NoInput(), new GameTime(TimeSpan.FromHours(2), TimeSpan.FromMilliseconds(16)));
        Assert.Equal(987_654_321, rec.LastPoll);
    }

    [Fact]
    public void The_network_clock_is_monotonic_and_counts_in_microseconds()
    {
        (_, GameServices s) = Services();
        long a = s.NetMicros();
        var sw = Stopwatch.StartNew();
        while (sw.ElapsedMilliseconds < 2) Thread.Yield();
        long b = s.NetMicros();
        Assert.True(b - a >= 1_000, $"{a} -> {b}");
        Assert.True(b - a < 60_000_000);
    }

    [Fact]
    public void A_failed_match_start_on_a_guest_is_tried_once_and_leaves_the_lobby_with_a_notice()
    {
        using var rig = new LobbyRig();
        LobbyClient ann = rig.Join("Ann");
        rig.Pump();
        ann.SetReady(true);
        rig.Pump();
        Assert.NotNull(rig.Host.Start(out _));
        rig.Pump();
        Assert.Equal(LobbyClientState.Started, ann.State);

        (ScreenStack stack, GameServices s) = Services();
        int calls = 0;
        stack.Push(new Dummy());
        stack.Push(new LobbyScreen(s, ann) { MatchFactory = (_, _) => { calls++; throw new InvalidDataException("bad map here"); } });
        for (int i = 0; i < 4; i++) stack.Update(NoInput(), new GameTime());
        Assert.Equal(1, calls);
        Assert.IsType<NoticeScreen>(stack.Top);
    }

    [Fact]
    public void A_host_whose_own_start_fails_tells_the_guests()
    {
        string root = Path.Combine(Path.GetTempPath(), "rts_lanfail_" + Guid.NewGuid().ToString("N"));
        var clock = Stopwatch.StartNew();
        long Now() => clock.ElapsedTicks / (Stopwatch.Frequency / 1_000_000);
        try
        {
            UdpTransport transport = UdpTransport.Host(0, 20_000, IPAddress.Loopback);
            var host = new LobbyHost(transport, "Hosty", 5UL, "v", LobbyMap.Curated, Path.Combine(root, "h"), Path.Combine(root, "h", "d"), () => 99);
            LobbyClient ann = LobbyClient.Join(new IPEndPoint(IPAddress.Loopback, transport.LocalEndPoint.Port),
                LobbyClient.MakeHello("Ann", false, 5UL, "v"), Path.Combine(root, "a"), Path.Combine(root, "a", "d"));
            bool Pump(Func<bool> done)
            {
                long deadline = Now() + 15_000_000;
                while (Now() < deadline)
                {
                    host.Poll(Now());
                    ann.Poll(Now());
                    if (done()) return true;
                    Thread.Yield();
                }
                return false;
            }
            Assert.True(Pump(() => ann.State == LobbyClientState.InLobby && ann.Data is not null));
            ann.SetReady(true);
            Assert.True(Pump(() => host.Data.Slots[1].Ready));
            LanMatchSetup setup = host.Start(out string? why) ?? throw new InvalidOperationException(why);
            Assert.True(Pump(() => ann.State == LobbyClientState.Started));

            LanMatchSetup broken = setup with { Settings = setup.Settings with { Map = new MapChoice(Path.Combine(root, "missing.map"), null) } };
            (_, GameServices s) = Services();
            Assert.ThrowsAny<Exception>(() => LanMatch.CreateScreen(s, broken));

            NetMessage? bye = null;
            long deadline = Now() + 15_000_000;
            while (bye is null && Now() < deadline)
            {
                ann.Transport.Poll(Now());
                while (ann.Transport.TryReceive(out _, out NetMessage m)) if (m is NetMessage.Bye) bye = m;
                Thread.Yield();
            }
            Assert.Equal(new NetMessage.Bye(LanText.HostStartFailed), bye);
            ann.Transport.Dispose();
        }
        finally
        {
            try { Directory.Delete(root, true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
        }
    }

    [Fact]
    public void A_host_disconnect_right_after_start_is_left_for_the_match()
    {
        using var rig = new LobbyRig();
        LobbyClient ann = rig.Join("Ann");
        rig.Pump();
        ann.SetReady(true);
        rig.Pump();
        Assert.NotNull(rig.Host.Start(out _));
        rig.Host.Poll(10_000_000);
        ann.Transport.Poll(10_000_000);   // the Start is in; the lobby client has not read it yet
        rig.Net.Disconnect(ann.LocalPeer, "cable pulled");
        ann.Poll(10_010_000);
        Assert.Equal(LobbyClientState.Started, ann.State);
        Assert.True(ann.Transport.TryGetEvent(out PeerEvent e) && e is { Peer: 0, Connected: false }, "the host-lost event was swallowed");
    }
}
