// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Sim;
using Xunit.Abstractions;

namespace Rts.Scripting.Tests.M8_15;

/// <summary>
/// M8-15: save a campaign mission halfway through its bot walk, load it into a fresh simulation and a fresh script host, carry on with
/// the same walk (the walk's own memory, such as which units form the convoy, lives in the walk and survives) and expect the very same
/// end state as the walk that never stopped.
/// </summary>
public sealed class MidMissionSaveLoadTests
{
    private readonly ITestOutputHelper _out;
    public MidMissionSaveLoadTests(ITestOutputHelper output) => _out = output;

    public static IEnumerable<object[]> Missions() => CampaignWalks.AllMissionRows;

    [Theory]
    [MemberData(nameof(Missions))]
    public void A_save_halfway_through_the_walk_loads_and_ends_in_the_same_state(string id)
    {
        Check(id, 3);
    }

    [Fact]
    public void A_tier_5_save_keeps_the_tier_and_ends_in_the_same_state()
    {
        Check("auth_01", 5);
    }

    private void Check(string id, int tier)
    {
        WalkResult straight = WalkCache.Get(id, tier);
        // The resumed walk must end exactly like the uninterrupted one; a tier-5 walk may end either way (D-221), tiers 1-3 must win first.
        if (BalanceTableTests.MustWin(tier)) Assert.True(straight.Won, "the uninterrupted walk must win first: " + straight.Diagnostics);
        int saveAt = straight.Tick / 2;

        MissionSession m = MissionSession.Create(TestData.DataDir, id);
        int savedTick = -1;
        ulong hashBefore = 0, hashAfter = 0;
        bool done = false;
        int loadedTier = -1;

        void Hook(MissionBot bot)
        {
            if (done || bot.Sim.Tick < saveAt) return;
            done = true;
            Simulation sim = bot.Sim;
            savedTick = sim.Tick;
            hashBefore = sim.ComputeStateHash();
            using var ms = new MemoryStream();
            sim.Save(ms);
            ms.Position = 0;
            var host = new MissionHost(m.Def, m.ScriptSource);
            Simulation loaded = Simulation.Load(ms, MissionSession.MakeSetup(m.Def, m.Map, m.Rules, host));
            hashAfter = loaded.ComputeStateHash();
            loadedTier = host.DifficultyTier;
            bot.Rebind(loaded, host);
        }

        WalkResult resumed = CampaignWalks.Play(id, tier, m, Hook);

        Assert.True(done, "the walk never reached the save tick " + saveAt);
        _out.WriteLine($"{id} tier {tier}: saved at tick {savedTick} of {straight.Tick}; end hash {resumed.Hash:x16}");
        // The hook runs on the bot's own ticks; a walk step that drives the sim directly can pass the exact tick (s22: exp_04 saved one tick late).
        Assert.InRange(savedTick, saveAt, saveAt + 60);
        Assert.Equal(hashBefore, hashAfter);
        Assert.Equal(tier, loadedTier);
        Assert.Null(resumed.Failure);
        Assert.Empty(resumed.Errors);
        Assert.True(resumed.Won == straight.Won, resumed.Diagnostics);
        Assert.Equal(straight.Tick, resumed.Tick);
        Assert.Equal(straight.PlayerLosses, resumed.PlayerLosses);
        Assert.Equal(straight.EnemyLosses, resumed.EnemyLosses);
        Assert.Equal(straight.Hash, resumed.Hash);
    }
}
