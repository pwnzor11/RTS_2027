// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using AI.Tests.Tiers;
using Rts.Data;
using Rts.Sim.Core;
using Rts.Sim.Rules;
using Rts.Sim.View;
using Xunit;

namespace AI.Tests.S15;

/// <summary>S15 lbknob (D-186): the per-faction army cap of a <c>[faction.&lt;id&gt;.tier&lt;N&gt;]</c> override stops unit production at the cap.</summary>
public sealed class LbKnobTests
{
    private const int Cap = 6;

    private static int PeakArmy(DifficultyProfile profile)
    {
        var rig = new TierRig(Faction.LeftBehind, profile);
        var own = new List<ViewActor>();
        int peak = 0;
        for (int i = 0; i < 60; i++)
        {
            rig.Run(100);
            rig.Sim.ViewFor(0).CopyOwn(own, false);
            int army = 0;
            foreach (ViewActor a in own)
                if (a.IsMobile && !(a.Kind == ActorKind.Vehicle && rig.World.Sim.Vehicles[a.TypeIndex].HarvestCapacity > 0)) army++;
            peak = Math.Max(peak, army);
        }
        return peak;
    }

    [Fact]
    public void ArmyCapOverrideLimitsTheSwarm()
    {
        DifficultyProfile shared = TestWorld.Tiers[2];
        DifficultyProfile free = shared.WithFactionOverride((int)Faction.LeftBehind, shared with { ArmyCap = 0 });
        DifficultyProfile capped = shared.WithFactionOverride((int)Faction.LeftBehind, shared with { ArmyCap = Cap });
        int open = PeakArmy(free), held = PeakArmy(capped);
        Assert.True(open > Cap + 4, $"without the cap the army grows past it ({open})");
        Assert.True(held <= Cap + 4, $"with the cap the army stays near it ({held}; in-flight units and free spawns allowed)");
    }

    [Fact]
    public void ALeftBehindTierThreeAiSavedMidGameContinuesLikeOneThatNeverSaved()
    {
        const int Before = 3000, After = 1500;   // across the end of the early cap
        DifficultyProfile tier = TestWorld.Tiers[2];
        var straight = new TierRig(Faction.LeftBehind, tier);
        straight.Run(Before + After);
        var r = new TierRig(Faction.LeftBehind, tier);
        r.Run(Before);
        byte[]? blob = r.CaptureAi();
        Assert.NotNull(blob);
        Assert.Equal(straight.Sim.ComputeStateHash(), r.ResumeWithBlob(blob!, After));
    }
}
