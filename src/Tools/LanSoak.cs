// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.AI;
using Rts.Data;
using Rts.Net;
using Rts.Net.Lockstep;
using Rts.Net.Loopback;
using Rts.Sim;
using Rts.Sim.Core;
using Rts.Sim.Map;
using Rts.Sim.Rules;

namespace Rts.Tools;

/// <summary>Settings of one `lan soak` run.</summary>
internal sealed record LanSoakOptions(int Players, int Ticks, int LatencyMs, int JitterMs, int LossPercent, ulong Seed, int Delay, int Tier,
    bool Udp);

/// <summary>What a `lan soak` run found.</summary>
internal sealed record LanSoakResult(int HashesCompared, int Desyncs, int Unchecked, bool AllAgree, bool ReplayMatches, bool Finished,
    ulong FinalHash, int Bundles, int Commands, int Rejected);

/// <summary>
/// M9-03: a whole LAN match in one process. The host and players-1 clients each build the same Simulation (the `ai match` map for two
/// players, the biome generator's small map for three or four; real rules; skirmish start), the host runs one tiered AI per slot, and
/// the lockstep layer carries everything over a transport with injected latency, jitter and loss. Driven by a virtual clock in 10 ms
/// steps, so a run is repeatable. Afterwards every peer's final hash and a replay of the host's bundle stream are compared.
/// </summary>
internal static class LanSoak
{
    private const long StepMicroseconds = 10_000;

    /// <summary>Plays the soak. Null when the transport is not available (the UDP hook before M9-A lands).</summary>
    public static LanSoakResult? Run(LanSoakOptions o, SimRules rules, ulong rulesHash, IReadOnlyList<DifficultyProfile> tiers)
    {
        int clients = o.Players - 1;
        (INetTransport host, INetTransport[] peers)? transports = o.Udp ? CreateUdp(clients) : CreateLoopback(o, clients);
        if (transports is null) return null;
        (INetTransport hostTransport, INetTransport[] clientTransports) = transports.Value;

        MapData map = BuildMap(o.Players, o.Seed);
        Simulation hostSim = BuildSim(map, rules, rulesHash, o);
        var clientSims = new Simulation[clients];
        for (int i = 0; i < clients; i++) clientSims[i] = BuildSim(map, rules, rulesHash, o);

        DifficultyProfile tier = tiers[o.Tier - 1];
        var driver = new AiDriver(o.Seed);
        for (int p = 0; p < o.Players; p++)
        {
            if (!AiRegistry.TryCreate(AiRegistry.TieredId, tier, out IAiPlayer? ai)) throw new InvalidOperationException("tiered AI missing");
            driver.Add((byte)p, ai, tier);
        }

        var bundles = new List<NetMessage.TurnBundle>();
        var host = new LockstepHost(hostTransport, o.Delay, (_, b) => hostSim.Step(b))
        {
            EndTick = o.Ticks,
            CollectAi = (_, list) => driver.Collect(hostSim, list),
            HashAt = t =>
            {
                foreach ((int Tick, ulong Hash) h in hostSim.RecentHashes) if (h.Tick == t) return h.Hash;
                return null;
            },
        };
        host.BundleSealed += bundles.Add;
        var lockstepClients = new LockstepClient[clients];
        for (int i = 0; i < clients; i++)
        {
            Simulation sim = clientSims[i];
            lockstepClients[i] = new LockstepClient(clientTransports[i], o.Delay, (_, b) => sim.Step(b), _ =>
            {
                IReadOnlyList<(string Part, ulong Hash)> subs = sim.ComputeHashes(out ulong full);
                var arr = new ulong[subs.Count];
                for (int k = 0; k < arr.Length; k++) arr[k] = subs[k].Hash;
                return (full, arr);
            });
        }

        int expected = clients * (o.Ticks / SimConstants.HashIntervalTicks);
        // generous cap: real time is ticks x 50 ms; allow ten times that before calling it stalled
        long maxSteps = (long)o.Ticks * 50 + 100_000;
        long now = 0;
        bool finished = false;
        for (long step = 0; step < maxSteps; step++)
        {
            if (host.Tick == o.Ticks && lockstepClients.All(c => c.Tick == o.Ticks) && host.HashesCompared + host.HashesUnchecked >= expected)
            {
                finished = true;
                break;
            }
            if (host.Desync is not null) break;
            now += StepMicroseconds;
            host.Update(now);
            foreach (LockstepClient c in lockstepClients) c.Update(now);
        }

        ulong final = hostSim.ComputeStateHash();
        bool agree = finished && clientSims.All(s => s.ComputeStateHash() == final);
        bool replay = finished && Replay(map, rules, rulesHash, o, bundles) == final;
        hostTransport.Dispose();
        foreach (INetTransport t in clientTransports) t.Dispose();
        return new LanSoakResult(host.HashesCompared, host.HashMismatches, host.HashesUnchecked, agree, replay, finished, final,
            bundles.Count, bundles.Sum(b => b.Commands.Length), host.RejectedCommands);
    }

    private static (INetTransport, INetTransport[]) CreateLoopback(LanSoakOptions o, int clients)
    {
        var net = new LoopbackNetwork(o.Seed * 7919 + 1) { LatencyMs = o.LatencyMs, JitterMs = o.JitterMs, UnreliableLossPercent = o.LossPercent };
        var peers = new INetTransport[clients];
        for (int i = 0; i < clients; i++) peers[i] = net.AddClient();
        return (net.Host, peers);
    }

    // PENDING Q-M9-03 (wave 2, M9-A): --udp runs this soak over the real UdpTransport on 127.0.0.1 (one host socket, players-1 client
    // sockets, handshake done before returning). Latency/jitter/loss then come from the transport's own test hooks. Until then: null.
    private static (INetTransport, INetTransport[])? CreateUdp(int clients) => null;

    private static MapData BuildMap(int players, ulong seed)
    {
        if (players == 2) return AiCommands.BuildMatchMap(null, Faction.Authority, Faction.Authority, 64, (int)seed).Map;   // the `ai match` map
        MapData map = MapGenerator.Generate(new MapGenParams(Biome.RustBelt, MapSize.Small, players, seed));
        if (map.StartPositions.Count < players)
            throw new InvalidOperationException($"the map generator gave {map.StartPositions.Count} starts for {players} players");
        return map;
    }

    private static Simulation BuildSim(MapData map, SimRules rules, ulong rulesHash, LanSoakOptions o)
    {
        var sim = new Simulation(new SimSetup(o.Seed, map, o.Players, rules, rulesHash) { NoRebuildDefeat = true });   // as AiMatchRunner
        SkirmishStart.Apply(sim);
        return sim;
    }

    private static ulong Replay(MapData map, SimRules rules, ulong rulesHash, LanSoakOptions o, List<NetMessage.TurnBundle> bundles)
    {
        Simulation sim = BuildSim(map, rules, rulesHash, o);
        var byTick = new Dictionary<int, Command[]>();
        foreach (NetMessage.TurnBundle b in bundles) byTick[b.Tick] = b.Commands;
        for (int t = 0; t < o.Ticks; t++) sim.Step(byTick.TryGetValue(t, out Command[]? c) ? c : Array.Empty<Command>());
        return sim.ComputeStateHash();
    }
}
