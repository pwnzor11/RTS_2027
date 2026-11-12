// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Net;
using Rts.Net.Lockstep;
using Rts.Net.Loopback;
using Rts.Sim.Core;

namespace Net.Tests.Lockstep;

/// <summary>
/// M9-03: host and client rules on a tiny stand-in "sim" (a running checksum of the commands it ran), so pacing, lead, waiting,
/// drops, chat and input checks are tested without the cost of a real game.
/// </summary>
public sealed class LockstepHostClientTests
{
    private sealed class Peer
    {
        public int Tick;
        public ulong State = 1469598103934665603UL;
        public readonly Dictionary<int, ulong> Hashes = new();
        public readonly List<Command> Ran = new();

        public void Step(int tick, IReadOnlyList<Command> bundle)
        {
            Assert.Equal(Tick, tick);
            foreach (Command c in bundle)
            {
                Ran.Add(c);
                State = (State ^ (ulong)(c.Player * 65536 + c.Sequence) ^ (ulong)c.Arg) * 1099511628211UL;
            }
            State = (State ^ (ulong)tick) * 1099511628211UL;
            Tick++;
            if (Tick % SimConstants.HashIntervalTicks == 0) Hashes[Tick] = State;
        }
    }

    private sealed class Rig
    {
        public readonly LoopbackNetwork Net;
        public readonly LockstepHost Host;
        public readonly Peer HostPeer = new();
        public readonly LockstepClient[] Clients;
        public readonly Peer[] ClientPeers;
        public readonly bool[] Frozen;
        public long Now;

        public Rig(int clients, int delay = 3, int latencyMs = 0, int jitterMs = 0)
        {
            Net = new LoopbackNetwork(11) { LatencyMs = latencyMs, JitterMs = jitterMs };
            var transports = Enumerable.Range(0, clients).Select(_ => Net.AddClient()).ToArray();
            Host = new LockstepHost(Net.Host, delay, HostPeer.Step)
            {
                HashAt = t => HostPeer.Hashes.TryGetValue(t, out ulong h) ? h : null,
            };
            ClientPeers = new Peer[clients];
            Clients = new LockstepClient[clients];
            Frozen = new bool[clients];
            for (int i = 0; i < clients; i++)
            {
                Peer p = ClientPeers[i] = new Peer();
                Clients[i] = new LockstepClient(transports[i], delay, p.Step, t => (p.Hashes[t], new ulong[] { p.Hashes[t] }));
            }
        }

        public void Run(long microseconds, long stepUs = 10_000)
        {
            long end = Now + microseconds;
            while (Now < end)
            {
                Now += stepUs;
                Host.Update(Now);
                for (int i = 0; i < Clients.Length; i++) if (!Frozen[i]) Clients[i].Update(Now);
            }
        }
    }

    private static Command Order(byte player, int arg) => new(0, player, 0, CommandKind.Stop, EntityId.None, EntityId.None, default, arg);

    [Fact]
    public void Ticks_run_at_20_Hz_on_the_callers_clock()
    {
        var rig = new Rig(1);
        rig.Run(1_000_000);
        Assert.InRange(rig.HostPeer.Tick, 19, 21);
        Assert.InRange(rig.ClientPeers[0].Tick, 18, 21);
    }

    [Fact]
    public void Client_input_is_stamped_by_the_host_and_runs_on_every_peer_on_the_same_tick()
    {
        var rig = new Rig(2, latencyMs: 15);
        rig.Host.SetSlot(1, 1);
        rig.Host.SetSlot(2, 2);
        rig.Host.SetSlot(0, 0);
        rig.Run(500_000);
        rig.Clients[0].Submit(Order(1, 11));
        rig.Clients[1].Submit(Order(2, 22));
        rig.Host.Submit(Order(0, 33));
        rig.Clients[1].Submit(Order(1, 99));   // another player's slot: refused
        rig.Run(1_000_000);
        Assert.Equal(1, rig.Host.RejectedCommands);
        Assert.Equal(3, rig.HostPeer.Ran.Count);
        foreach (Peer p in rig.ClientPeers) Assert.Equal(rig.HostPeer.Ran, p.Ran);
        Assert.Equal(new[] { 33, 11, 22 }, rig.HostPeer.Ran.Select(c => c.Arg).OrderBy(a => a == 33 ? 0 : a));
        Assert.Equal(0, rig.Host.HashMismatches);
        Assert.True(rig.Host.HashesCompared > 0);
    }

    [Fact]
    public void The_host_waits_for_a_frozen_client_then_offers_and_performs_the_drop()
    {
        var rig = new Rig(2);
        rig.Host.SetSlot(2, 2);
        var drops = new List<LockstepDrop>();
        rig.Host.PeerDropped += drops.Add;
        rig.Run(1_000_000);
        rig.Frozen[1] = true;
        int frozenAt = rig.ClientPeers[1].Tick;
        rig.Run(2_000_000);
        Assert.True(rig.HostPeer.Tick <= frozenAt + rig.Host.MaxLead + 1, "host ran past its lead limit");
        Assert.Equal(new[] { 2 }, rig.Host.WaitingFor);
        Assert.Equal(new[] { 2 }, rig.Clients[0].Waiting.Peers);
        Assert.True(rig.Clients[0].Waiting.WaitedMs >= NetProtocol.WaitOverlayMs);
        Assert.False(rig.Host.CanDrop(2));
        rig.Run(NetProtocol.HostMayDropMs * 1000L);
        Assert.True(rig.Host.CanDrop(2));
        int tickAtDrop = rig.Host.Tick;
        Assert.True(rig.Host.Drop(2, "dropped by host"));
        Assert.Equal(new LockstepDrop(2, 2, tickAtDrop, "dropped by host"), Assert.Single(drops));
        rig.Run(1_000_000);
        Assert.True(rig.HostPeer.Tick > tickAtDrop + 10, "play goes on after the drop");
        Assert.Empty(rig.Clients[0].Waiting.Peers);
        Assert.Equal(new[] { 2 }, rig.Clients[0].DroppedPeers);
    }

    [Fact]
    public void A_client_that_lags_a_minute_is_dropped_automatically()
    {
        var rig = new Rig(1);
        var drops = new List<LockstepDrop>();
        rig.Host.PeerDropped += drops.Add;
        rig.Frozen[0] = true;
        rig.Run((NetProtocol.AutoDropMs + 2_000) * 1000L);
        LockstepDrop d = Assert.Single(drops);
        Assert.Equal(1, d.Peer);
        Assert.Equal(-1, d.Player);
        Assert.Equal("timed out", d.Reason);
        Assert.True(rig.HostPeer.Tick > d.Tick + 20);
    }

    [Fact]
    public void A_disconnect_or_a_bye_is_a_drop()
    {
        var rig = new Rig(2);
        var drops = new List<LockstepDrop>();
        rig.Host.PeerDropped += drops.Add;
        rig.Run(300_000);
        rig.Net.Disconnect(1, "lost");
        rig.Clients[1].Leave("bed time");
        rig.Run(300_000);
        Assert.Equal(new[] { (1, "lost"), (2, "bed time") }, drops.Select(d => (d.Peer, d.Reason)));
        Assert.True(rig.Clients[0].HostLost);
    }

    [Fact]
    public void Chat_is_relayed_to_all_or_to_the_team_with_the_sender_set_by_the_host()
    {
        var rig = new Rig(3);
        rig.Host.TeamOf = p => p % 2;   // host and peer 2 on team 0; peers 1 and 3 on team 1
        var heard = new List<(int Who, NetMessage.Chat Chat)>();
        rig.Host.ChatReceived += c => heard.Add((0, c));
        for (int i = 0; i < 3; i++) { int who = i + 1; rig.Clients[i].ChatReceived += c => heard.Add((who, c)); }
        rig.Clients[0].SendChat(0, "hello all");
        rig.Clients[0].SendChat(1, "team only");
        rig.Host.SendChat(1, "host team");
        rig.Run(100_000);
        Assert.Equal(new[] { 0, 1, 2, 3 }, heard.Where(h => h.Chat.Text == "hello all").Select(h => h.Who).OrderBy(x => x));
        Assert.Equal(new[] { 1, 3 }, heard.Where(h => h.Chat.Text == "team only").Select(h => h.Who).OrderBy(x => x));
        Assert.Equal(new[] { 0, 2 }, heard.Where(h => h.Chat.Text == "host team").Select(h => h.Who).OrderBy(x => x));
        Assert.All(heard.Where(h => h.Chat.Text != "host team"), h => Assert.Equal(1, h.Chat.FromPeer));
    }

    [Fact]
    public void Pause_stops_every_peer_on_the_same_tick_and_resume_waits_for_the_countdown()
    {
        var rig = new Rig(2, latencyMs: 10, jitterMs: 20);
        rig.Run(2_000_000);
        rig.Clients[1].RequestPause();
        rig.Run(1_000_000);
        int stop = rig.HostPeer.Tick;
        Assert.True(rig.Host.Paused);
        Assert.Equal(2, rig.Host.PausedBy);
        Assert.Equal(rig.Host.SealedThrough + 1, stop);
        Assert.All(rig.ClientPeers, p => Assert.Equal(stop, p.Tick));
        Assert.All(rig.Clients, c => Assert.True(c.Paused));
        rig.Run(5_000_000);
        Assert.Equal(stop, rig.HostPeer.Tick);
        Assert.Empty(rig.Host.WaitingFor);   // a pause is not a lagging peer

        rig.Clients[0].RequestResume();
        rig.Run(2_000_000);
        Assert.Equal(stop, rig.HostPeer.Tick);
        Assert.True(rig.Clients[0].ResumeCountdownMs > 0);
        rig.Run(2_000_000);
        Assert.False(rig.Host.Paused);
        Assert.All(rig.Clients, c => Assert.False(c.Paused));
        Assert.True(rig.HostPeer.Tick > stop + 5);
        rig.Run(500_000);
        Assert.Equal(0, rig.Host.HashMismatches);
    }

    [Fact]
    public void A_hash_mismatch_is_broadcast_as_a_desync_and_pauses_the_match()
    {
        var rig = new Rig(2);
        int[]? reported = null;
        rig.Host.Desynced += (t, peers) => reported = peers;
        rig.Run(1_000_000);
        rig.ClientPeers[0].State ^= 0xDEAD;   // corrupt client 1's state
        rig.Run(2_000_000);
        Assert.Equal(new[] { 1 }, reported);
        Assert.True(rig.Host.Paused);
        Assert.NotNull(rig.Host.Desync);
        Assert.All(rig.Clients, c => Assert.Equal(rig.Host.Desync!.Value.Tick, c.Desync!.Value.Tick));
        int stop = rig.HostPeer.Tick;
        Assert.All(rig.ClientPeers, p => Assert.Equal(stop, p.Tick));
    }

    [Fact]
    public void An_oversized_input_is_refused_whole()
    {
        var net = new LoopbackNetwork();
        INetTransport client = net.AddClient();
        var peer = new Peer();
        var host = new LockstepHost(net.Host, 2, peer.Step);
        host.SetSlot(1, 1);
        client.Send(0, new NetMessage.Input(Enumerable.Range(0, LockstepHost.MaxCommandsPerInput + 1).Select(i => Order(1, i)).ToArray()));
        host.Update(0);
        Assert.Equal(LockstepHost.MaxCommandsPerInput + 1, host.RejectedCommands);
    }
}
