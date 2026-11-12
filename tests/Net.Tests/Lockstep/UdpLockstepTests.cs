// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using System.Net;
using Rts.AI;
using Rts.Net;
using Rts.Net.Lockstep;
using Rts.Net.Udp;
using Rts.Sim;
using Rts.Sim.Core;

namespace Net.Tests.Lockstep;

/// <summary>M9-04: a lockstep host and two clients over real UdpTransport sockets on 127.0.0.1, driven by a virtual clock.</summary>
public sealed class UdpLockstepTests
{
    private const int Ticks = 1500;
    private const long StepUs = 10_000;

    [Fact]
    public void Three_players_over_real_udp_stay_in_sync_and_replay()
    {
        const ulong seed = 11;
        using UdpTransport hostT = UdpTransport.Host(0, NetProtocol.TimeoutMs, IPAddress.Loopback);
        var ep = new IPEndPoint(IPAddress.Loopback, hostT.LocalEndPoint.Port);
        var clientT = new UdpTransport[2];
        for (int i = 0; i < 2; i++)
            clientT[i] = UdpTransport.Connect(ep, new NetMessage.Hello(NetProtocol.Version, "test", LanTestMatch.Rules.RulesHash, "c" + i, false));
        try
        {
            long now = 0;
            int accepted = 0;
            for (int step = 0; step < 6000 && !(accepted == 2 && clientT.All(c => c.LocalPeer >= 1)); step++)
            {
                now += StepUs;
                hostT.Poll(now);
                foreach (UdpTransport c in clientT) c.Poll(now);
                while (hostT.TryReceive(out int from, out NetMessage m))
                {
                    if (m is not NetMessage.Hello) continue;
                    int id = ++accepted;
                    Assert.True(hostT.Accept(from, id));
                    hostT.Send(id, new NetMessage.Welcome(id));
                }
                foreach (UdpTransport c in clientT) while (c.TryReceive(out _, out _)) { }
            }
            Assert.Equal(2, accepted);
            Assert.All(clientT, c => Assert.True(c.LocalPeer >= 1));

            Simulation hostSim = LanTestMatch.BuildSim(seed, 3);
            var clientSims = new[] { LanTestMatch.BuildSim(seed, 3), LanTestMatch.BuildSim(seed, 3) };
            var driver = new AiDriver(seed);
            var tier = LanTestMatch.Tiers[LanTestMatch.AiTier - 1];
            for (int p = 0; p < 3; p++)
            {
                Assert.True(AiRegistry.TryCreate(AiRegistry.TieredId, tier, out IAiPlayer? ai));
                driver.Add((byte)p, ai!, tier);
            }
            var bundles = new List<NetMessage.TurnBundle>();
            var host = new LockstepHost(hostT, NetProtocol.DefaultDelay, (_, b) => hostSim.Step(b))
            {
                EndTick = Ticks,
                CollectAi = (_, list) => driver.Collect(hostSim, list),
                HashAt = t =>
                {
                    foreach ((int Tick, ulong Hash) h in hostSim.RecentHashes) if (h.Tick == t) return h.Hash;
                    return null;
                },
            };
            host.BundleSealed += bundles.Add;
            var clients = new LockstepClient[2];
            for (int i = 0; i < 2; i++)
            {
                Simulation sim = clientSims[i];
                clients[i] = new LockstepClient(clientT[i], NetProtocol.DefaultDelay, (_, b) => sim.Step(b), _ =>
                {
                    IReadOnlyList<(string Part, ulong Hash)> subs = sim.ComputeHashes(out ulong full);
                    return (full, subs.Select(s => s.Hash).ToArray());
                });
            }

            int expected = 2 * (Ticks / SimConstants.HashIntervalTicks);
            bool finished = false;
            for (int step = 0; step < 400_000 && !finished; step++)
            {
                now += StepUs;
                host.Update(now);
                foreach (LockstepClient c in clients) c.Update(now);
                Assert.Null(host.Desync);
                finished = host.Tick == Ticks && clients.All(c => c.Tick == Ticks) && host.HashesCompared + host.HashesUnchecked >= expected;
            }
            Assert.True(finished, $"stalled at host tick {host.Tick}");
            Assert.Equal(0, host.HashMismatches);
            Assert.True(host.HashesCompared > 0);

            ulong final = hostSim.ComputeStateHash();
            Assert.All(clientSims, s => Assert.Equal(final, s.ComputeStateHash()));
            Simulation replay = LanTestMatch.BuildSim(seed, 3);
            var byTick = new Dictionary<int, Command[]>();
            foreach (NetMessage.TurnBundle b in bundles) byTick[b.Tick] = b.Commands;
            for (int t = 0; t < Ticks; t++) replay.Step(byTick.TryGetValue(t, out Command[]? c) ? c : Array.Empty<Command>());
            Assert.Equal(final, replay.ComputeStateHash());
        }
        finally
        {
            foreach (UdpTransport c in clientT) c.Dispose();
        }
    }
}
