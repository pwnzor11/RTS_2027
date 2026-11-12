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

namespace Net.Tests.Lockstep;

/// <summary>
/// A LAN match in one process: a host and N clients over a <see cref="LoopbackNetwork"/>, each with its own Simulation built the way
/// the headless AI games build theirs (AiMatchRunner.PlayN: generated map, real rules, NoRebuildDefeat, SkirmishStart, tiered AIs).
/// The host runs the AIs; every peer logs its hash every 16 ticks so tests can compare them independently of the host's own check.
/// Driven by a virtual clock in fixed steps, so a run is fully repeatable.
/// </summary>
internal sealed class LanTestMatch
{
    public static readonly string RulesDir = RulesLocator.FindRulesDir(AppContext.BaseDirectory)!;
    public static readonly RuleSet Rules = RulesLoader.Load(RulesDir);
    public static readonly IReadOnlyList<DifficultyProfile> Tiers = DifficultyLoader.Load(DifficultyLoader.PathBesideRules(RulesDir));
    private static readonly Dictionary<(ulong, int), MapData> Maps = new();

    public const int AiTier = 3;
    public readonly ulong Seed;
    public readonly int Players;
    public readonly LoopbackNetwork Net;
    public readonly Simulation HostSim;
    public readonly Simulation[] ClientSims;
    public readonly LockstepHost Host;
    public readonly LockstepClient[] Clients;
    public readonly INetTransport[] ClientTransports;
    public readonly List<NetMessage.TurnBundle> Bundles = new();
    public readonly SortedDictionary<int, ulong>[] HashLog;   // [0] host, [i + 1] client i
    public readonly bool[] Frozen;
    public readonly List<LockstepDrop> Drops = new();
    public readonly AiDriver HostAi;
    public readonly List<AiDriver> TakeoverAis = new();
    public readonly AiDriver?[] ClientAis;
    public long Now;

    /// <summary>Builds the match. <paramref name="clientSlots"/>: client i plays player clientSlots[i] with its own AI issuing as
    /// a human would (through Submit); null = every player is a host AI and clients only watch and step.</summary>
    public LanTestMatch(int players, int clients, ulong seed, int latencyMs, int jitterMs, int lossPercent, int endTick,
        int delay = NetProtocol.DefaultDelay, int[]? clientSlots = null)
    {
        Seed = seed;
        Players = players;
        Net = new LoopbackNetwork(seed * 7919 + 1) { LatencyMs = latencyMs, JitterMs = jitterMs, UnreliableLossPercent = lossPercent };
        ClientTransports = new INetTransport[clients];
        for (int i = 0; i < clients; i++) ClientTransports[i] = Net.AddClient();

        HostSim = BuildSim(seed, players);
        ClientSims = new Simulation[clients];
        for (int i = 0; i < clients; i++) ClientSims[i] = BuildSim(seed, players);
        HashLog = new SortedDictionary<int, ulong>[clients + 1];
        for (int i = 0; i <= clients; i++) HashLog[i] = new SortedDictionary<int, ulong>();
        Frozen = new bool[clients];

        DifficultyProfile tier = Tiers[AiTier - 1];
        HostAi = new AiDriver(seed);
        var humanSlots = new HashSet<int>(clientSlots ?? Array.Empty<int>());
        for (int p = 0; p < players; p++)
            if (!humanSlots.Contains(p)) HostAi.Add((byte)p, Create(tier), tier);

        Host = new LockstepHost(Net.Host, delay, (t, bundle) => Step(HostSim, 0, t, bundle)) { EndTick = endTick };
        Host.HashAt = tick =>
        {
            foreach ((int Tick, ulong Hash) h in HostSim.RecentHashes) if (h.Tick == tick) return h.Hash;
            return null;
        };
        Host.CollectAi = (t, list) =>
        {
            Assert.Equal(t, HostSim.Tick);
            HostAi.Collect(HostSim, list);
            foreach (AiDriver d in TakeoverAis) d.Collect(HostSim, list);
        };
        Host.BundleSealed += b => Bundles.Add(b);
        Host.PeerDropped += drop =>
        {
            Drops.Add(drop);
            if (drop.Player < 0) return;
            var ai = new AiDriver(seed);   // Q-M9-d: an AI takes the slot from the drop tick; AiDriver.Add must precede Collect, so a fresh driver
            ai.Add((byte)drop.Player, Create(tier), tier);
            TakeoverAis.Add(ai);
        };

        Clients = new LockstepClient[clients];
        ClientAis = new AiDriver?[clients];
        for (int i = 0; i < clients; i++)
        {
            int index = i;
            Simulation sim = ClientSims[i];
            Clients[i] = new LockstepClient(ClientTransports[i], delay, (t, bundle) => ClientStep(index, t, bundle),
                tick =>
                {
                    Assert.Equal(tick, sim.Tick);
                    IReadOnlyList<(string Part, ulong Hash)> subs = sim.ComputeHashes(out ulong full);
                    var arr = new ulong[subs.Count];
                    for (int k = 0; k < arr.Length; k++) arr[k] = subs[k].Hash;
                    return (full, arr);
                });
            if (clientSlots is not null && i < clientSlots.Length)
            {
                Host.SetSlot(ClientTransports[i].LocalPeer, (byte)clientSlots[i]);
                var own = new AiDriver(seed + 1000);
                own.Add((byte)clientSlots[i], Create(tier), tier);
                ClientAis[i] = own;
            }
        }
    }

    public static Simulation BuildSim(ulong seed, int players)
    {
        MapData map;
        lock (Maps)
        {
            if (!Maps.TryGetValue((seed, players), out map!))
            {
                map = MapGenerator.Generate(new MapGenParams(Biome.RustBelt, MapSize.Small, players, seed));
                Maps[(seed, players)] = map;
            }
        }
        Assert.True(map.StartPositions.Count >= players, $"map has {map.StartPositions.Count} starts for {players} players");
        var sim = new Simulation(new SimSetup(seed, map, players, Rules.Sim, Rules.RulesHash) { NoRebuildDefeat = true });
        SkirmishStart.Apply(sim);
        return sim;
    }

    private static IAiPlayer Create(DifficultyProfile tier)
    {
        Assert.True(AiRegistry.TryCreate(AiRegistry.TieredId, tier, out IAiPlayer? ai));
        return ai!;
    }

    private void ClientStep(int index, int tick, IReadOnlyList<Command> bundle)
    {
        Simulation sim = ClientSims[index];
        if (ClientAis[index] is { } ai && !Clients[index].DroppedSelf)
        {
            // the client's "human" is an AI issuing through Submit: the host restamps every command
            var issued = new List<Command>();
            ai.Collect(sim, issued);
            foreach (Command c in issued) Clients[index].Submit(c);
        }
        Step(sim, index + 1, tick, bundle);
    }

    private void Step(Simulation sim, int logIndex, int tick, IReadOnlyList<Command> bundle)
    {
        Assert.Equal(tick, sim.Tick);
        foreach (Command c in bundle) Assert.Equal(tick, c.ExecTick);
        sim.Step(bundle);
        if (sim.Tick % SimConstants.HashIntervalTicks == 0) HashLog[logIndex][sim.Tick] = sim.RecentHashes[^1].Hash;
    }

    /// <summary>Advances the virtual clock in steps of <paramref name="stepUs"/> until <paramref name="until"/> holds; fails after
    /// <paramref name="maxIterations"/>.</summary>
    public void RunUntil(Func<bool> until, int maxIterations = 400_000, long stepUs = 10_000)
    {
        for (int i = 0; i < maxIterations; i++)
        {
            if (until()) return;
            Advance(stepUs);
        }
        Assert.Fail($"condition not reached after {maxIterations} iterations (host tick {Host.Tick})");
    }

    /// <summary>Advances the virtual clock for <paramref name="microseconds"/>.</summary>
    public void RunFor(long microseconds, long stepUs = 10_000)
    {
        long end = Now + microseconds;
        while (Now < end) Advance(stepUs);
    }

    private void Advance(long stepUs)
    {
        Now += stepUs;
        Host.Update(Now);
        for (int i = 0; i < Clients.Length; i++)
            if (!Frozen[i]) Clients[i].Update(Now);
    }

    /// <summary>Asserts that the hash logs of the host and the listed clients agree at every tick they share.</summary>
    public int AssertLogsAgree(IEnumerable<int> clientIndexes)
    {
        int compared = 0;
        foreach (int i in clientIndexes)
            foreach ((int tick, ulong hash) in HashLog[i + 1])
            {
                Assert.True(HashLog[0].TryGetValue(tick, out ulong own), $"host has no hash for tick {tick}");
                Assert.True(own == hash, $"client {i} differs from the host at tick {tick}");
                compared++;
            }
        return compared;
    }

    /// <summary>Re-runs the host's bundle stream on a fresh sim and returns the final hash after <paramref name="ticks"/> ticks.</summary>
    public ulong ReplayBundles(int ticks)
    {
        Simulation sim = BuildSim(Seed, Players);
        var byTick = new Dictionary<int, Command[]>();
        foreach (NetMessage.TurnBundle b in Bundles) byTick.Add(b.Tick, b.Commands);
        for (int t = 0; t < ticks; t++) sim.Step(byTick.TryGetValue(t, out Command[]? cmds) ? cmds : Array.Empty<Command>());
        return sim.ComputeStateHash();
    }
}
