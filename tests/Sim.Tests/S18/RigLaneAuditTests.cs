// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Sim.Core;
using Rts.Sim.Production;
using Rts.Sim.Rules;

namespace Rts.Sim.Tests.S18;

/// <summary>S18 audit, FACTION-LE-03: on the shipped data the Left Behind structure and defence lanes keep building while the Mother
/// Rig is packing and packed (D-102 says the queue pauses). The existing M6 RigTests fixture sets <c>Produces = Structure</c>, a value the
/// TOML loader never produces, so that test does not cover the real data.</summary>
public sealed class RigLaneAuditTests
{
    private sealed record Run(S18World W, EntityId Rig, int StructLane, int DefLane, int Refinery, int Nest);

    private static Run Start()
    {
        var w = new S18World(Faction.LeftBehind, Faction.Authority);
        EntityId rig = w.Place(0, "lb_mother_rig", 20, 20);
        int refinery = w.B("lb_refinery_rig");
        w.Run(1, w.Cmd(0, CommandKind.Debug, default, default, default, (int)DebugOp.SetCash, 9000));
        w.Run(1,
            w.Cmd(0, CommandKind.Build, default, default, default, (int)ProductionCategory.Structure, refinery),
            w.Cmd(0, CommandKind.Build, default, default, default, (int)ProductionCategory.Defence, w.B("lb_spike_nest")));
        return new Run(w, rig, ProductionState.Lane(0, ProductionCategory.Structure), ProductionState.Lane(0, ProductionCategory.Defence), refinery, w.B("lb_spike_nest"));
    }

    private static bool HasPackedRig(Simulation sim)
    {
        for (int v = 1; v < sim.Units.Capacity; v++)
            if (sim.Units.Slots.IsIndexAlive(v) && sim.Units.Player[v] == 0 && sim.Rigs.IsPackedRig(v)) return true;
        return false;
    }

    [Fact]
    public void Audit_NoShippedBuildingProducesStructuresOrDefences_SoTheRigPauseNeverMatches()
    {
        // S18 audit: pins current behaviour of FACTION-LE-03; flip the assertion when fixed.
        Assert.DoesNotContain(S18World.RealRules.Buildings,
            b => b.Produces == ProductionCategory.Structure || b.Produces == ProductionCategory.Defence);
        Assert.Null(S18World.RealRules.Buildings.Single(b => b.Id == "lb_mother_rig").Produces);
    }

    [Fact]
    public void Audit_StructureAndDefenceLanes_KeepBuildingWhileTheMotherRigIsPackingAndPacked()
    {
        // S18 audit: pins current behaviour of FACTION-LE-03; flip the assertion when fixed.
        Run r = Start();
        S18World w = r.W;
        Assert.Equal(1, w.Sim.Production.QueueLength(0, ProductionCategory.Structure));
        Assert.Equal(1, w.Sim.Production.QueueLength(0, ProductionCategory.Defence));

        w.Run(1, w.Cmd(0, CommandKind.Pack, r.Rig));
        int atOrder = w.Sim.Production.Ticks[r.StructLane];
        int packTicks = w.Rules.Buildings[w.B("lb_mother_rig")].PackTicks;
        w.Run(packTicks / 2);
        int whilePacking = w.Sim.Production.Ticks[r.StructLane];
        Assert.True(whilePacking > atOrder, "the structure lane advanced while the rig was packing");

        w.Run(packTicks / 2 + 5);
        Assert.False(w.Sim.Buildings.Slots.IsAlive(r.Rig));   // the building is gone
        Assert.True(HasPackedRig(w.Sim));                      // and drives as a packed rig
        int packedA = w.Sim.Production.Ticks[r.StructLane];
        w.Run(30);
        int packedB = w.Sim.Production.Ticks[r.StructLane];
        Assert.True(packedB > packedA, "the structure lane advanced while the rig was packed");
        Assert.Equal(r.Nest, w.Sim.Production.ReadyType(0, ProductionCategory.Defence));   // the defence finished meanwhile

        w.Run(900);
        Assert.True(HasPackedRig(w.Sim));
        Assert.Equal(r.Refinery, w.Sim.Production.ReadyType(0, ProductionCategory.Structure));   // a whole Refinery Rig was built with no conyard standing
    }

    [Fact(Skip = "S18 FACTION-LE-03: awaiting owner decision")]
    public void Desired_StructureLane_MakesNoProgressWhileTheMotherRigIsPacked()
    {
        Run r = Start();
        S18World w = r.W;
        w.Run(1, w.Cmd(0, CommandKind.Pack, r.Rig));
        int atOrder = w.Sim.Production.Ticks[r.StructLane];
        w.Run(w.Rules.Buildings[w.B("lb_mother_rig")].PackTicks + 50);
        Assert.True(HasPackedRig(w.Sim));
        Assert.InRange(w.Sim.Production.Ticks[r.StructLane], atOrder, atOrder + 1);
        Assert.Equal(-1, w.Sim.Production.ReadyType(0, ProductionCategory.Structure));
    }
}
