// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Sim.Core;
using Rts.Sim.Missions;
using Rts.Sim.Rules;
using Rts.Sim.Tests.S18;

namespace Rts.Sim.Tests.S19;

/// <summary>S19 D-205 (FAD-05): the attack-ground order. A unit closes to weapon range and fires at a bare ground cell; the order is
/// a normal command (sorted, saved, replayed), refused for an unarmed unit or an off-map cell.</summary>
public sealed class AttackGroundTests
{
    private static readonly CellCoord Aim = new(30, 30);

    private static int Rad(S18World w, CellCoord c) => w.Sim.Ground.RadiationAt(c);

    [Fact]
    public void AshSower_SowsRadiation_OnABareCell_AfterWalkingInRange()
    {
        var w = new S18World(Faction.Directorate, Faction.Authority);
        EntityId t = w.Veh(0, "dir_ash_sower", 24, 30);
        Assert.Equal(0, Rad(w, Aim));
        w.Run(1, w.Cmd(0, CommandKind.AttackGround, t, default, Aim, (int)ActorKind.Vehicle));
        w.Run(300);
        Assert.True(Rad(w, Aim) > 0);
        Assert.True(w.VehHp(t) > 0);   // the order ends once the cell is sown, so the sower does not cook itself
        Assert.NotEqual(MissionKind.AttackGround, w.Sim.Units.Mission[t.Index]);
    }

    [Fact]
    public void FoamLayer_FoamsABareCell()
    {
        var w = new S18World();
        EntityId t = w.Veh(0, "auth_foam_truck", 24, 30);
        w.Run(1, w.Cmd(0, CommandKind.AttackGround, t, default, Aim, (int)ActorKind.Vehicle));
        w.Run(300);
        Assert.True(w.Sim.IsFoamed(Aim));
    }

    [Fact]
    public void AttackGround_Refused_ForUnarmedUnit_AndOffMapCell()
    {
        var w = new S18World();
        EntityId truck = w.Veh(0, "auth_decon_truck", 24, 30);
        w.Run(1, w.Cmd(0, CommandKind.AttackGround, truck, default, new CellCoord(-3, 4000), (int)ActorKind.Vehicle));
        w.Run(5);
        Assert.NotEqual(MissionKind.AttackGround, w.Sim.Units.Mission[truck.Index]);
    }

    [Fact]
    public void AttackGround_SurvivesSaveAndLoad_WithEqualHashes()
    {
        var w = new S18World(Faction.Directorate, Faction.Authority);
        EntityId t = w.Veh(0, "dir_ash_sower", 24, 30);
        w.Run(1, w.Cmd(0, CommandKind.AttackGround, t, default, Aim, (int)ActorKind.Vehicle));
        w.Run(40);
        using var ms = new MemoryStream();
        w.Sim.Save(ms);
        ms.Position = 0;
        Simulation copy = Simulation.Load(ms, w.Sim.Setup);
        Assert.Equal(w.Sim.ComputeStateHash(), copy.ComputeStateHash());
        for (int i = 0; i < 200; i++) { w.Sim.Step(Array.Empty<Command>()); copy.Step(Array.Empty<Command>()); }
        Assert.Equal(w.Sim.ComputeStateHash(), copy.ComputeStateHash());
        Assert.True(Rad(w, Aim) > 0);
    }
}
