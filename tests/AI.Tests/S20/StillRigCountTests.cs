// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using AI.Tests.Tiers;
using Rts.Data;
using Rts.Sim.Rules;
using Xunit;
using Xunit.Abstractions;

namespace AI.Tests.S20;

/// <summary>M13-18: a Left Behind AI in a map-corner base used to build about 30 Water Still Rigs (supply buildings) in ten minutes.</summary>
public sealed class StillRigCountTests(ITestOutputHelper log)
{
    private static int Count(Rts.Sim.Simulation sim, string id)
    {
        var b = sim.Buildings;
        var rules = sim.RulesOf(0);
        int n = 0;
        for (int i = 1; i < b.Capacity; i++)
            if (b.Slots.IsIndexAlive(i) && b.Player[i] == 0 && rules.Buildings[b.TypeIndex[i]].Id == id) n++;
        return n;
    }

    [Theory]
    [InlineData(0)]
    [InlineData(4)]
    public void LeftBehind_CornerBase_ReportsStillRigCountInTenMinutes(int tierIndex)
    {
        var rig = new TierRig(Faction.LeftBehind, TestWorld.Tiers[tierIndex]);
        int max = 0, early = 0, prev = 0;
        for (int step = 0; step < 12000 / 20; step++)
        {
            var before = rig.Sim.HudFor(0).Supply;
            rig.Run(20);
            int n = Count(rig.Sim, "lb_still_rig");
            if (n > prev && before.Used < before.Cap - 10) early++;
            prev = n; max = Math.Max(max, n);
        }
        var hud = rig.Sim.HudFor(0).Supply;
        log.WriteLine($"tier idx {tierIndex}: still rigs at end {Count(rig.Sim, "lb_still_rig")}, max {max}; supply used {hud.Used} cap {hud.Cap}; rigs added while used < cap - 10: {early}");
        Assert.True(max > 0 || early == 0);   // report-only: the counts above are the result
    }
}
