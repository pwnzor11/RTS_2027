// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using AI.Tests.Tiers;
using Rts.Data;
using Rts.Sim.Rules;
using Xunit;
using Xunit.Abstractions;

namespace AI.Tests.S19;

/// <summary>M13-10 (AI-04 family): a Left Behind tier 5 AI in a map-corner base used to fill the 12-cell window around its yard (30 supply
/// structures plus its idle army) and then sat on a finished structure, cancelling and re-buying it for the rest of the game, placing
/// nothing more. Placement now widens its search before giving up, and the AI moves its idle army off the build area first.</summary>
public sealed class IdleArmyPlacementTests(ITestOutputHelper log)
{
    [Fact]
    public void LeftBehindTier5_CornerBase_KeepsPlacingStructures_AfterTheNearWindowIsFull()
    {
        var rig = new TierRig(Faction.LeftBehind, TestWorld.Tiers[4]);
        int placedAt9600 = 0, hold = 0, longest = 0;
        for (int step = 0; step < 18000 / 10; step++)
        {
            rig.Run(10);
            if (rig.Sim.Tick == 9600) placedAt9600 = rig.Ai.StructuresPlaced;
            hold = rig.Sim.HudFor(0).Queues.First(q => q.Category == ProductionCategory.Structure).ReadyTypeIndex >= 0 ? hold + 10 : 0;
            longest = Math.Max(longest, hold);
        }
        log.WriteLine($"placed at 9600: {placedAt9600}, at end {rig.Ai.StructuresPlaced}; unplaceable {rig.Ai.Unplaceable}, cancelled {rig.Ai.CancelledReady}, longest ready hold {longest}");
        Assert.True(rig.Ai.Unplaceable <= 5, $"{rig.Ai.Unplaceable} placements found no site");   // before: 210 (740 without the army move)
        Assert.True(rig.Ai.CancelledReady <= 1, $"cancelled {rig.Ai.CancelledReady} finished structures");   // before: 70
        Assert.True(longest < 500, $"a finished structure waited {longest} ticks");
    }
}
