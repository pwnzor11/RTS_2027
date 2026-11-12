// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Net;
using Rts.Net.Lockstep;
using Rts.Net.Loopback;
using Rts.Sim.Core;

namespace Net.Tests.Lockstep;

/// <summary>
/// M9-14 review findings 3, 5 and 6: an input flood cannot make a bundle too big to send, a client's Resume cannot slip past an open
/// desync, and hostile Hash messages cannot grow the host's pending list. A tiny stand-in "sim" on the loopback network, virtual clock.
/// </summary>
public sealed class LockstepHardeningTests
{
    private sealed class Peer
    {
        public int Tick;
        public ulong State = 1469598103934665603UL;
        public readonly Dictionary<int, ulong> Hashes = new();
        public readonly List<Command> Ran = new();
        public int Largest;

        public void Step(int tick, IReadOnlyList<Command> bundle)
        {
            Assert.Equal(Tick, tick);
            Largest = Math.Max(Largest, bundle.Count);
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
        public readonly LoopbackNetwork Net = new(5);
        public readonly LockstepHost Host;
        public readonly Peer HostPeer = new();
        public readonly LockstepClient[] Clients;
        public readonly INetTransport[] Transports;
        public readonly Peer[] ClientPeers;
        public long Now;

        public Rig(int clients, int delay = 3)
        {
            Transports = Enumerable.Range(0, clients).Select(_ => Net.AddClient()).ToArray();
            Host = new LockstepHost(Net.Host, delay, HostPeer.Step) { HashAt = t => HostPeer.Hashes.TryGetValue(t, out ulong h) ? h : null };
            ClientPeers = new Peer[clients];
            Clients = new LockstepClient[clients];
            for (int i = 0; i < clients; i++)
            {
                Peer p = ClientPeers[i] = new Peer();
                Clients[i] = new LockstepClient(Transports[i], delay, p.Step, t => (p.Hashes[t], new ulong[] { p.Hashes[t] }));
            }
        }

        public void Run(long microseconds, long stepUs = 10_000)
        {
            long end = Now + microseconds;
            while (Now < end)
            {
                Now += stepUs;
                Host.Update(Now);
                foreach (LockstepClient c in Clients) c.Update(Now);
            }
        }
    }

    private static Command Order(byte player, int arg) => new(0, player, 0, CommandKind.Stop, EntityId.None, EntityId.None, default, arg);

    [Fact]
    public void An_input_flood_during_a_pause_never_makes_a_bundle_too_big_to_send()
    {
        var rig = new Rig(1);
        rig.Host.SetSlot(1, 1);
        rig.Run(500_000);
        rig.Host.RequestPause();
        rig.Run(1_000_000);
        for (int i = 0; i < 100 * LockstepHost.MaxCommandsPerInput; i++) rig.Clients[0].Submit(Order(1, i));   // 6400 orders while paused
        rig.Run(500_000);
        rig.Host.RequestResume();
        rig.Run(20_000_000);

        Assert.False(rig.Host.Paused);
        Assert.InRange(rig.HostPeer.Largest, 1, TurnScheduler.MaxCommandsPerTick);
        Assert.Equal(rig.HostPeer.Ran, rig.ClientPeers[0].Ran);
        int ran = rig.HostPeer.Ran.Count;
        Assert.True(ran > 0);
        Assert.Equal(100 * LockstepHost.MaxCommandsPerInput, ran + rig.Host.RejectedCommands);   // every order either ran or was counted
        Assert.Equal(0, rig.Host.HashMismatches);
    }

    [Fact]
    public void The_scheduler_spills_a_crowded_tick_into_later_ones_and_bounds_what_waits()
    {
        var s = new TurnScheduler(2);
        s.SetSlot(1, 1);
        int accepted = 0;
        for (int i = 0; i < TurnScheduler.MaxPendingPerPlayer + 500; i++) if (s.Submit(1, Order(1, i), 0)) accepted++;
        Assert.Equal(TurnScheduler.MaxPendingPerPlayer, accepted);
        Assert.Equal(500, s.Rejected);
        int total = 0, lastSeq = -1;
        for (int tick = 2; s.PendingCount > 0; tick++)
        {
            Command[] bundle = s.Seal(tick);
            Assert.InRange(bundle.Length, 0, TurnScheduler.MaxCommandsPerPlayerPerTick);
            foreach (Command c in bundle)
            {
                Assert.True(c.Sequence > lastSeq);   // spilled orders keep their order
                lastSeq = c.Sequence;
                Assert.Equal(tick, c.ExecTick);
            }
            total += bundle.Length;
        }
        Assert.Equal(accepted, total);
    }

    [Fact]
    public void A_clients_resume_is_refused_while_a_desync_is_open()
    {
        var rig = new Rig(2);
        rig.Run(1_000_000);
        rig.ClientPeers[0].State ^= 0xBEEF;   // corrupt client 1
        rig.Run(2_000_000);
        Assert.NotNull(rig.Host.Desync);
        Assert.True(rig.Host.Paused);

        rig.Clients[1].RequestResume();   // a Resume straight to the lockstep host, past any game-side filter
        rig.Run(5_000_000);
        Assert.True(rig.Host.Paused);
        Assert.Equal(0, rig.Host.ResumeCountdownMs);

        rig.Host.ContinueAfterDesync();   // the host answered: now a resume is taken (client 1 stays corrupt, so no long run after it)
        rig.Clients[1].RequestResume();
        rig.Run(100_000);
        Assert.True(rig.Host.ResumeCountdownMs > 0);
    }

    [Fact]
    public void Hostile_hash_messages_do_not_pile_up_on_the_host()
    {
        var net = new LoopbackNetwork();
        INetTransport client = net.AddClient();
        var peer = new Peer();
        var host = new LockstepHost(net.Host, 2, peer.Step) { HashAt = _ => 1UL };
        host.Update(0);
        for (int i = 0; i < 5_000; i++) client.Send(0, new NetMessage.Hash(1_000_000 + i * SimConstants.HashIntervalTicks, 2UL, Array.Empty<ulong>()));   // far future
        for (int i = 0; i < 5_000; i++) client.Send(0, new NetMessage.Hash(SimConstants.HashIntervalTicks + 1, 2UL, Array.Empty<ulong>()));   // off the interval
        for (int i = 0; i < 5_000; i++) client.Send(0, new NetMessage.Hash(SimConstants.HashIntervalTicks, 2UL, Array.Empty<ulong>()));   // one tick, many times
        host.Update(1);
        Assert.InRange(host.PendingHashCount, 0, 1);
    }
}
