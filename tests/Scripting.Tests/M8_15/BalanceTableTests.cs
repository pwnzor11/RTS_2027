// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Sim.Core;
using Xunit.Abstractions;

namespace Rts.Scripting.Tests.M8_15;

/// <summary>
/// M8-15 balance table: walks every mission at every tier its bot test supports and prints one markdown row per run
/// (lines starting with <c>ROW |</c>) for docs/balance/m8_15_acceptance.md.
/// </summary>
public sealed class BalanceTableTests
{
    private readonly ITestOutputHelper _out;
    public BalanceTableTests(ITestOutputHelper output) => _out = output;

    /// <summary>mm:ss at the sim tick rate.</summary>
    internal static string Clock(int tick)
    {
        int s = tick / SimConstants.TicksPerSecond;
        return $"{s / 60}:{s % 60:00}";
    }

    public static IEnumerable<object[]> MissionTiers() =>
        CampaignWalks.AllMissions.SelectMany(id => CampaignWalks.Tiers(id).Select(t => new object[] { id, t }));

    [Theory]
    [MemberData(nameof(MissionTiers))]
    public void Every_mission_tier_is_walked_and_reported(string id, int tier)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        WalkResult r = WalkCache.Get(id, tier);
        int limit = CampaignWalks.TickLimit(id);
        string note = r.Failure is not null ? "walk stopped: " + r.Failure[..Math.Min(80, r.Failure.Length)]
            : !r.Won ? "not won" : r.Tick >= limit ? "over the design limit"
            : $"{r.Tick * 100 / limit}% of limit {Clock(limit)}";
        _out.WriteLine($"ROW | {id} | {tier} | {(r.Won ? "yes" : "NO")} | {r.Tick} | {Clock(r.Tick)} | {r.PlayerLosses} | {r.EnemyLosses} | {note} | {sw.ElapsedMilliseconds} ms");
        Assert.Empty(r.Errors);
        Assert.Equal(0, r.LocaleWarnings);
        Assert.True(r.Won, r.Diagnostics);
        Assert.True(r.Tick < limit, r.Diagnostics);
    }
}
