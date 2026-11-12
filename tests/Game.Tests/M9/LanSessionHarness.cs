// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Data;
using Rts.Game.Lan;
using Rts.Game.Screens;
using Rts.Net;
using Rts.Net.Loopback;
using Rts.Sim.Core;
using Rts.Sim.Rules;

namespace Rts.Game.Tests.M9;

/// <summary>
/// A whole LAN match in one process (M9-07 tests): a host and three clients over a <see cref="LoopbackNetwork"/> with real sims, each a
/// real <see cref="LanSession"/> as the game builds it. Slots: 0 = the host (Human), 1 and 2 = clients 1 and 2 (Human), 3 = an AI the
/// host runs; peer 3 watches as a spectator. Generated small map, four players. Driven by a virtual clock in fixed steps, so a run is
/// repeatable and never depends on timing luck. Every peer logs the bundles it stepped.
/// </summary>
internal sealed class LanSessionHarness : IDisposable
{
    public const int Peers = 4;   // host, client 1, client 2, spectator
    public const int SpectatorPeer = 3;

    public readonly string RulesDir = RulesLocator.FindRulesDir(AppContext.BaseDirectory) ?? throw new InvalidOperationException("data/rules not found");
    public readonly string Folder = Path.Combine(Path.GetTempPath(), "rts_lan_tests_" + Guid.NewGuid().ToString("N"));
    public readonly LoopbackNetwork Net;
    public readonly SkirmishSettings Settings;
    public readonly LanSession[] Sessions = new LanSession[Peers];
    public readonly bool[] Frozen = new bool[Peers];
    public readonly List<(int Tick, Command Command)>[] Stepped = new List<(int, Command)>[Peers];
    public long Now;

    public LanSessionHarness(ulong seed = 41, int latencyMs = 20, int jitterMs = 10, int lossPercent = 5, int delay = NetProtocol.DefaultDelay, int goneBeforeStart = -1)
    {
        Directory.CreateDirectory(Folder);
        Net = new LoopbackNetwork(seed) { LatencyMs = latencyMs, JitterMs = jitterMs, UnreliableLossPercent = lossPercent };
        var transports = new INetTransport[Peers];
        transports[0] = Net.Host;
        for (int i = 1; i < Peers; i++) transports[i] = Net.AddClient();

        Settings = new SkirmishSettings(
            new MapChoice(null, new MapGenParams(Biome.RustBelt, MapSize.Small, 4, seed)),
            new[]
            {
                new SkirmishSlot(SlotKind.Human, Faction.Authority, SkirmishSettings.DefaultAiTier, 0, 0),
                new SkirmishSlot(SlotKind.Human, Faction.Authority, SkirmishSettings.DefaultAiTier, 1, 1),
                new SkirmishSlot(SlotKind.Human, Faction.Authority, SkirmishSettings.DefaultAiTier, 2, 2),
                new SkirmishSlot(SlotKind.Ai, Faction.Authority, 3, 3, 3),
            },
            seed);
        var peerSlots = new Dictionary<int, int> { [0] = 0, [1] = 1, [2] = 2 };
        var names = new Dictionary<int, string> { [0] = "Hana", [1] = "Ivo", [2] = "Jun", [3] = "Kit" };
        if (goneBeforeStart > 0)
        {
            // M9-14: this peer left between the lobby's Start and the match; the lobby already took the disconnect event
            Net.Disconnect(goneBeforeStart, "left before the match");
            while (transports[0].TryGetEvent(out _)) { }
            Frozen[goneBeforeStart] = true;
        }
        for (int peer = 0; peer < Peers; peer++)
        {
            Stepped[peer] = new List<(int, Command)>();
            if (peer == goneBeforeStart) continue;
            var setup = new LanMatchSetup(transports[peer], peer == 0, peer, Settings, peerSlots, new[] { SpectatorPeer }, names, delay);
            string own = Path.Combine(Folder, "peer" + peer);
            Sessions[peer] = new LanSession(setup, RulesDir, new LanSessionOptions(null, Path.Combine(own, "lan_last.rpl"), Path.Combine(own, "desync")));
            int index = peer;
            Sessions[peer].Stepped += (tick, bundle) => { foreach (Command c in bundle) Stepped[index].Add((tick, c)); };
        }
    }

    public LanSession Host => Sessions[0];

    /// <summary>Advances the virtual clock by <paramref name="stepUs"/> and updates every peer that is not frozen.</summary>
    public void Advance(long stepUs = 10_000)
    {
        Now += stepUs;
        for (int i = 0; i < Peers; i++)
            if (!Frozen[i]) Sessions[i].Update(Now);
    }

    /// <summary>Runs until <paramref name="until"/> holds; fails after <paramref name="maxIterations"/> steps.</summary>
    public void RunUntil(Func<bool> until, int maxIterations = 400_000)
    {
        for (int i = 0; i < maxIterations; i++)
        {
            if (until()) return;
            Advance();
        }
        Assert.Fail($"condition not reached after {maxIterations} iterations (host tick {Host.Tick})");
    }

    /// <summary>Runs until every listed peer has stepped up to <paramref name="tick"/> (the host stops sealing there).</summary>
    public void RunToTick(int tick, params int[] peers)
    {
        Host.Host!.EndTick = tick;
        int[] who = peers.Length == 0 ? Enumerable.Range(0, Peers).ToArray() : peers;
        RunUntil(() => who.All(p => Sessions[p].Tick >= tick));
    }

    /// <summary>The full state hash of a peer's sim as it is now.</summary>
    public ulong HashOf(int peer) => Sessions[peer].Source.Sim.ComputeStateHash();

    public void Dispose()
    {
        foreach (LanSession s in Sessions) s?.Dispose();
        try { Directory.Delete(Folder, recursive: true); }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }
}
