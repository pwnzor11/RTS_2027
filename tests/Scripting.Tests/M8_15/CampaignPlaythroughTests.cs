// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using System.Diagnostics;
using Xunit.Abstractions;

namespace Rts.Scripting.Tests.M8_15;

/// <summary>
/// M8-15 acceptance: a bot plays each faction's four missions in campaign order at tier 3, one fresh session per mission, and wins every
/// one before its design tick limit with no script errors and no locale warnings. The all-sixteen test is tagged so CI can pick it by trait.
/// </summary>
public sealed class CampaignPlaythroughTests
{
    private const int Tier = 3;
    private readonly ITestOutputHelper _out;
    public CampaignPlaythroughTests(ITestOutputHelper output) => _out = output;

    private void PlayInOrder(IEnumerable<string> missions)
    {
        var problems = new List<string>();
        var total = Stopwatch.StartNew();
        foreach (string id in missions)
        {
            var sw = Stopwatch.StartNew();
            WalkResult r = WalkCache.Fresh(id, Tier);
            WalkCache.Remember(r);
            int limit = CampaignWalks.TickLimit(id);
            _out.WriteLine($"{id}: {(r.Won ? "won" : "NOT WON")} at tick {r.Tick} ({BalanceTableTests.Clock(r.Tick)}), limit {limit}, " +
                $"losses {r.PlayerLosses}/{r.EnemyLosses}, {sw.ElapsedMilliseconds} ms");
            if (r.Failure is not null) problems.Add(id + " walk stopped: " + r.Failure);
            else if (!r.Won) problems.Add(id + " not won: " + r.Diagnostics);
            if (r.Tick >= limit) problems.Add($"{id} won too late: tick {r.Tick} >= {limit}");
            if (r.Errors.Count > 0) problems.Add(id + " script errors: " + string.Join("; ", r.Errors));
            if (r.LocaleWarnings > 0) problems.Add($"{id} has {r.LocaleWarnings} locale warnings");
        }
        _out.WriteLine($"total {total.ElapsedMilliseconds} ms");
        Assert.True(problems.Count == 0, string.Join(Environment.NewLine, problems));
    }

    [Fact]
    public void Authority_campaign_is_won_in_order() => PlayInOrder(CampaignWalks.Campaigns["authority"]);

    [Fact]
    public void Directorate_campaign_is_won_in_order() => PlayInOrder(CampaignWalks.Campaigns["directorate"]);

    [Fact]
    public void Left_Behind_campaign_is_won_in_order() => PlayInOrder(CampaignWalks.Campaigns["left_behind"]);

    [Fact]
    public void Expedition_campaign_is_won_in_order() => PlayInOrder(CampaignWalks.Campaigns["expedition"]);

    [Fact]
    [Trait("Category", "Campaign16")]
    public void All_sixteen_missions_are_won_in_campaign_order() => PlayInOrder(CampaignWalks.AllMissions);
}
