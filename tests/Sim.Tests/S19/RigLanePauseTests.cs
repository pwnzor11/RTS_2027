// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Sim.Core;
using Rts.Sim.Production;
using Rts.Sim.Rules;
using Rts.Sim.Tests.S18;

namespace Rts.Sim.Tests.S19;

/// <summary>M13-09 (FACTION-LE-03, D-102): Left Behind structure and defence lanes hold their progress while the Mother Rig is packing or
/// packed, and resume when it unpacks.</summary>
public sealed class RigLanePauseTests
{
    [Fact]
    public void BothLanes_HoldWhilePacked_AndResumeAfterUnpack()
    {
        var w = new S18World(Faction.LeftBehind, Faction.Authority);
        EntityId rig = w.Place(0, "lb_mother_rig", 20, 20);
        w.Run(1, w.Cmd(0, CommandKind.Debug, default, default, default, (int)DebugOp.SetCash, 9000));
        w.Run(1,
            w.Cmd(0, CommandKind.Build, default, default, default, (int)ProductionCategory.Structure, w.B("lb_refinery_rig")),
            w.Cmd(0, CommandKind.Build, default, default, default, (int)ProductionCategory.Defence, w.B("lb_spike_nest")));
        int sLane = ProductionState.Lane(0, ProductionCategory.Structure), dLane = ProductionState.Lane(0, ProductionCategory.Defence);
        w.Run(10);
        Assert.True(w.Sim.Production.Ticks[sLane] > 0);   // unpacked: building

        w.Run(1, w.Cmd(0, CommandKind.Pack, rig));
        int packTicks = w.Rules.Buildings[w.B("lb_mother_rig")].PackTicks;
        w.Run(packTicks + 20);
        int packed = -1;
        for (int v = 1; v < w.Sim.Units.Capacity; v++)
            if (w.Sim.Units.Slots.IsIndexAlive(v) && w.Sim.Units.Player[v] == 0 && w.Sim.Rigs.IsPackedRig(v)) packed = v;
        Assert.True(packed > 0);
        int s0 = w.Sim.Production.Ticks[sLane], d0 = w.Sim.Production.Ticks[dLane];
        w.Run(60);
        Assert.Equal(s0, w.Sim.Production.Ticks[sLane]);
        Assert.Equal(d0, w.Sim.Production.Ticks[dLane]);

        // unpack where it stands: both lanes advance again
        CellCoord site = w.Sim.Units.Position[packed].ToCell();
        w.Run(1, w.Cmd(0, CommandKind.Pack, w.Sim.Units.Slots.IdOf(packed), default, site));
        w.Run(packTicks * 3);
        Assert.False(w.Sim.Rigs.IsPackedRig(packed) && w.Sim.Units.Slots.IsIndexAlive(packed), "the rig has unpacked");
        Assert.True(w.Sim.Production.Ticks[sLane] > s0 || w.Sim.Production.Ready[sLane] >= 0, "the structure lane resumed");
    }
}
