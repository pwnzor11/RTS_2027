// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.AI;
using Rts.Sim.Core;

namespace AI.Tests.M8;

/// <summary>M8-06: each AI.* hook changes the Recruit AI as documented in docs/missions/api.md, and Clear restores it.</summary>
public class AiHookTests
{
    private static readonly CellCoord Zone = new(50, 12);
    private const int Army = 13;   // tier 1 attack threshold is 10 (+ seeded bias/jitter, at most +5)

    [Fact]
    public void ControlArmyLaunchesAWaveOnItsOwn()
    {
        var rig = new HookRig(Army);
        rig.Run(2400);
        Assert.True(rig.Ai.Waves >= 1);
        Assert.DoesNotContain(Zone, rig.AttackMoves);
    }

    [Fact]
    public void AttackSendsWavesToTheZoneCentre()
    {
        var rig = new HookRig(Army);
        rig.Hooks.Current = AiHooks.None with { AttackCell = Zone };
        rig.Run(4500);
        Assert.True(rig.Ai.Waves >= 1);
        Assert.Contains(Zone, rig.AttackMoves);
    }

    [Fact]
    public void DefendHoldsWavesAndRalliesAtTheZone()
    {
        var rig = new HookRig(Army);
        CellCoord def = rig.FarCell();
        rig.Hooks.Current = AiHooks.None with { DefendCell = def };
        rig.Run(4500);
        Assert.Equal(0, rig.Ai.Waves);
        Assert.Contains(def, rig.Moves);
    }

    [Fact]
    public void ClearRestoresNormalBehaviour()
    {
        var rig = new HookRig(Army);
        rig.Hooks.Current = AiHooks.None with { DefendCell = rig.Yard() };
        rig.Run(2400);
        Assert.Equal(0, rig.Ai.Waves);
        rig.Hooks.Current = AiHooks.None;   // AI.Clear
        rig.Run(2400);
        Assert.True(rig.Ai.Waves >= 1);
    }

    [Fact]
    public void SetTierSwitchesTheProfileAndClearGoesBack()
    {
        var rig = new HookRig(2, tier: 1);
        rig.Run(120);
        Assert.Equal(1, rig.Ai.ActiveTier);
        rig.Hooks.Current = AiHooks.None with { Tier = 4 };
        rig.Run(120);
        Assert.Equal(4, rig.Ai.ActiveTier);
        rig.Hooks.Current = AiHooks.None;
        rig.Run(120);
        Assert.Equal(1, rig.Ai.ActiveTier);
    }

    [Fact]
    public void WaveLaunchesOnceWithASmallArmyAtTheZone()
    {
        var rig = new HookRig(2);
        rig.Run(1200);
        Assert.Equal(0, rig.Ai.Waves);   // 2 units are far below any wave trigger
        rig.Hooks.Current = AiHooks.None with { WaveSeq = 1, WaveId = "raid_a", WaveCell = Zone };
        rig.Run(4000);
        Assert.Equal(1, rig.Ai.ServedWaveSeq);
        Assert.Equal(1, rig.Ai.Waves);
        Assert.Contains(Zone, rig.AttackMoves);
        rig.Run(1500);
        Assert.Equal(1, rig.Ai.Waves);   // the same WaveSeq is not served twice
    }

    [Fact]
    public void WaveHookIgnoresADefendHold()
    {
        var rig = new HookRig(2);
        rig.Hooks.Current = AiHooks.None with { DefendCell = rig.Yard(), WaveSeq = 1, WaveId = "w", WaveCell = Zone };
        rig.Run(600);
        Assert.Equal(1, rig.Ai.Waves);
    }

    [Fact]
    public void SaveMidHookReproducesTheFinalHash()
    {
        const ulong seed = 7;
        ulong Final(bool resume)
        {
            var rig = new HookRig(Army, seed: seed);
            rig.Hooks.Current = AiHooks.None with { AttackCell = Zone, Tier = 3, WaveSeq = 1, WaveId = "w", WaveCell = Zone };
            rig.Run(900);   // the scripted wave is out; its goal and the served sequence are AI memory
            if (resume) return rig.ResumeWithBlob(rig.Capture()!, 1500);
            rig.Run(1500);
            return rig.Sim.ComputeStateHash();
        }
        Assert.Equal(Final(false), Final(true));
    }

    [Fact]
    public void ARestoredAiDoesNotServeTheSameWaveAgain()
    {
        var rig = new HookRig(2);
        rig.Hooks.Current = AiHooks.None with { WaveSeq = 1, WaveId = "w", WaveCell = Zone };
        rig.Run(300);
        byte[] blob = rig.Capture()!;
        rig.ResumeWithBlob(blob, 60);
        Assert.Equal(1, rig.Ai.ServedWaveSeq);
    }

    [Fact]
    public void NoHooksChangeNothing()
    {
        ulong Run(bool attach)
        {
            var rig = new HookRig(Army, attachHooks: attach);
            rig.Run(2400);
            return rig.Sim.ComputeStateHash();
        }
        Assert.Equal(Run(false), Run(true));
    }
}
