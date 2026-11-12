// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Sim.Core;

namespace Net.Tests.Lockstep;

/// <summary>
/// M9-03 acceptance: real simulations in lockstep over the loopback network. A host and three clients, four host-run AIs, 0-40 ms
/// jitter and unreliable loss, 4 000 ticks: every client hash equals the host's at every hash tick, and replaying the host's bundle
/// stream reproduces the final hash. Plus drop-to-AI, pause/resume and a corrupted client caught as a desync.
/// </summary>
public sealed class LockstepSoakTests
{
    [Fact]
    public void Four_ai_slots_three_clients_4000_ticks_with_jitter_stay_in_sync_and_the_bundle_stream_replays()
    {
        const int End = 4000;
        var m = new LanTestMatch(players: 4, clients: 3, seed: 21, latencyMs: 0, jitterMs: 40, lossPercent: 10, endTick: End);
        const int expected = 3 * (End / SimConstants.HashIntervalTicks);
        m.RunUntil(() => m.Host.Tick == End && m.Clients.All(c => c.Tick == End) && m.Host.HashesCompared == expected);

        Assert.Equal(0, m.Host.HashMismatches);
        Assert.Equal(0, m.Host.HashesUnchecked);
        Assert.Null(m.Host.Desync);
        Assert.Equal(expected, m.AssertLogsAgree(new[] { 0, 1, 2 }));
        Assert.Equal(End / SimConstants.HashIntervalTicks, m.HashLog[0].Count);
        Assert.True(m.Bundles.Sum(b => b.Commands.Length) > 100, "the AIs issued commands through the scheduler");
        Assert.Empty(m.Drops);

        ulong final = m.HostSim.ComputeStateHash();
        foreach (var sim in m.ClientSims) Assert.Equal(final, sim.ComputeStateHash());
        Assert.Equal(final, m.ReplayBundles(End));
    }

    [Fact]
    public void A_dropped_clients_slot_is_taken_by_an_ai_and_the_others_stay_in_sync()
    {
        const int End = 1600;
        // client 0 plays player 1 (its own AI issues through Submit, as a human would); the host's AIs play 0, 2 and 3
        var m = new LanTestMatch(players: 4, clients: 3, seed: 5, latencyMs: 5, jitterMs: 20, lossPercent: 0, endTick: End, clientSlots: new[] { 1 });
        m.RunUntil(() => m.Host.Tick >= 400);
        Assert.Contains(m.Bundles, b => b.Commands.Any(c => c.Player == 1));   // the client's input reached the bundles
        Assert.Equal(0, m.Host.RejectedCommands);

        m.Frozen[0] = true;
        m.RunUntil(() => m.Host.CanDrop(1));
        int dropTick = m.Host.Tick;
        Assert.True(m.Host.Drop(1, "not responding"));
        Assert.Equal(1, Assert.Single(m.Drops).Player);
        Assert.Single(m.TakeoverAis);

        m.RunUntil(() => m.Host.Tick == End && m.Clients[1].Tick == End && m.Clients[2].Tick == End);
        m.RunFor(200_000);
        Assert.Contains(m.Bundles, b => b.Tick > dropTick && b.Commands.Any(c => c.Player == 1));   // the AI issues for the slot
        Assert.Equal(0, m.Host.HashMismatches);
        Assert.True(m.AssertLogsAgree(new[] { 1, 2 }) >= 2 * (End / SimConstants.HashIntervalTicks));
        Assert.All(m.Clients.Skip(1), c => Assert.Equal(new[] { 1 }, c.DroppedPeers));
        Assert.Equal(m.HostSim.ComputeStateHash(), m.ReplayBundles(End));
    }

    [Fact]
    public void Pause_and_resume_stop_and_restart_all_peers_on_the_same_tick()
    {
        const int End = 1200;
        var m = new LanTestMatch(players: 2, clients: 2, seed: 9, latencyMs: 10, jitterMs: 30, lossPercent: 5, endTick: End);
        m.RunUntil(() => m.Host.Tick >= 300);
        m.Clients[0].RequestPause();
        m.RunFor(1_000_000);
        int stop = m.HostSim.Tick;
        Assert.True(m.Host.Paused);
        Assert.All(m.ClientSims, s => Assert.Equal(stop, s.Tick));
        m.RunFor(10_000_000);
        Assert.Equal(stop, m.HostSim.Tick);
        Assert.All(m.ClientSims, s => Assert.Equal(stop, s.Tick));
        Assert.Empty(m.Drops);   // pausing never counts as lag

        m.Clients[1].RequestResume();
        m.RunFor(NetProtocolCountdownUs - 100_000);
        Assert.Equal(stop, m.HostSim.Tick);
        m.RunFor(400_000);
        Assert.False(m.Host.Paused);
        Assert.True(m.HostSim.Tick > stop);
        m.RunUntil(() => m.Host.Tick == End && m.Clients.All(c => c.Tick == End));
        m.RunFor(200_000);
        Assert.Equal(0, m.Host.HashMismatches);
        Assert.Equal(2 * (End / SimConstants.HashIntervalTicks), m.AssertLogsAgree(new[] { 0, 1 }));
    }

    private const long NetProtocolCountdownUs = Rts.Net.NetProtocol.ResumeCountdownMs * 1000L;

    [Fact]
    public void A_corrupted_client_state_is_detected_as_a_desync()
    {
        var m = new LanTestMatch(players: 2, clients: 2, seed: 3, latencyMs: 5, jitterMs: 10, lossPercent: 0, endTick: 2000);
        m.RunUntil(() => m.ClientSims[1].Tick >= 500);
        int corruptAt = m.ClientSims[1].Tick;
        m.ClientSims[1].Players.Cash[0] += 7;   // a bug-shaped divergence on client 1 (peer 2)
        m.RunUntil(() => m.Host.Desync is not null, maxIterations: 20_000);
        (int tick, int[] peers, ulong[] subs) = m.Host.Desync!.Value;
        Assert.Equal(new[] { 2 }, peers);
        Assert.InRange(tick, corruptAt, corruptAt + SimConstants.HashIntervalTicks);
        Assert.NotEmpty(subs);
        m.RunFor(1_000_000);
        Assert.True(m.Host.Paused);
        Assert.All(m.Clients, c => Assert.Equal(tick, c.Desync!.Value.Tick));
        Assert.All(m.ClientSims, s => Assert.Equal(m.HostSim.Tick, s.Tick));   // stopped together
        Assert.Equal(m.HostSim.ComputeStateHash(), m.ClientSims[0].ComputeStateHash());   // the healthy client still agrees
    }
}
