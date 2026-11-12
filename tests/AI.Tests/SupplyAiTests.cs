// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using AI.Tests.Tiers;
using Rts.Data;
using Rts.Sim.Rules;
using Xunit.Abstractions;

namespace AI.Tests;

/// <summary>SUP-5 (design 13 §6, D-133): with the supply cap on, the AI of every faction keeps building supply structures ahead of its units,
/// so it never sits at the cap for long.</summary>
public sealed class SupplyAiTests(ITestOutputHelper log)
{
    /// <summary>Longest total time at the cap allowed in the first 15 minutes (ticks; 20 per second).</summary>
    private const int MaxCappedTicks = 60 * 20;

    [Theory]
    [InlineData(Faction.Authority, 1)]
    [InlineData(Faction.Authority, 5)]
    [InlineData(Faction.Directorate, 1)]
    [InlineData(Faction.Directorate, 5)]
    [InlineData(Faction.LeftBehind, 1)]
    [InlineData(Faction.LeftBehind, 5)]
    public void TheAiNeverSitsCappedForMoreThanAMinuteInTheFirstFifteen(Faction faction, int tier)
    {
        var rig = new TierRig(faction, TestWorld.Tiers[tier - 1]);
        int capped = 0, peakCap = 0, peakUsed = 0;
        for (int step = 0; step < 15 * 60; step++)
        {
            rig.Run(20);
            // M13-05: the check covers the game, not the time after it. Slot 1 has no AI; since attackers reach buildings at once (path
            // retarget) a tier-5 AI defeats it by minute 3-4, and after that its idle army stands in the way of its own placements and
            // nothing it does is about supply any more.
            if (rig.Sim.Players.Defeated[1]) break;
            var s = rig.Sim.HudFor(0).Supply;
            Assert.True(s.Ceiling > 0, "supply is on in the shipped rules");
            if (s.Cap > 0 && s.Used >= s.Cap && s.Cap < s.Ceiling) capped += 20;
            peakCap = Math.Max(peakCap, s.Cap); peakUsed = Math.Max(peakUsed, s.Used);
        }
        log.WriteLine($"{faction} tier {tier}: capped {capped / 20} s, peak used {peakUsed}, peak cap {peakCap}");
        Assert.True(capped <= MaxCappedTicks, $"{faction} tier {tier} was capped for {capped / 20} s");
        Assert.True(peakUsed > 0);
    }
}
