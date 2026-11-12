// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Sim.Rules;
using Rts.Sim.Core;
using Rts.Sim.Numerics;
using Rts.Sim.Tests.S18;

namespace Rts.Sim.Tests.S20;

/// <summary>M13-11 item 19: melee-range units (Sappers) acquire enemies in sight on attack-move and guard, close in and attack.</summary>
public sealed class MeleeAcquireTests
{
    private static S18World World() => new(Faction.Directorate, Faction.Authority);

    // The enemy tank holds fire so the 60-health Sapper lives to reach it. Spawn everything first (setup calls end at the first Step).
    private static Command Passive(S18World w, EntityId tank) =>
        w.Cmd(1, CommandKind.SetStance, tank, default, default, (int)ActorKind.Vehicle | ((int)UnitStance.HoldFire << 8));

    [Fact]
    public void Sapper_OnAttackMove_PastATankInSight_AttacksIt()
    {
        var w = World();
        EntityId tank = w.Veh(1, "auth_tank", 20, 23);
        EntityId sapper = w.Inf(0, "dir_sapper", 14, 20);
        int hp = w.VehHp(tank);
        w.Run(600, Passive(w, tank), w.Cmd(0, CommandKind.AttackMove, sapper, default, new CellCoord(30, 20), (int)ActorKind.Infantry));
        Assert.True(w.VehHp(tank) < hp, "the sapper walked past the tank without attacking it");
    }

    [Fact]
    public void Sapper_OnAttackMove_ResumesTheMarchAfterTheKill()
    {
        var w = World();
        EntityId tank = w.Veh(1, "auth_tank", 20, 23);
        // a second charge may be needed to finish the tank; two sappers in the party
        EntityId s1 = w.Inf(0, "dir_sapper", 14, 20), s2 = w.Inf(0, "dir_sapper", 14, 21);
        w.Run(1, Passive(w, tank), w.Cmd(0, CommandKind.AttackMove, s1, default, new CellCoord(36, 20), (int)ActorKind.Infantry),
                 w.Cmd(0, CommandKind.AttackMove, s2, default, new CellCoord(36, 21), (int)ActorKind.Infantry));
        w.Run(1500);
        Assert.Equal(0, w.VehHp(tank));
        Assert.True(w.Sim.Infantry.Slots.IsAlive(s1) || w.Sim.Infantry.Slots.IsAlive(s2));
        EntityId alive = w.Sim.Infantry.Slots.IsAlive(s1) ? s1 : s2;
        Assert.True(w.Sim.Infantry.Position[alive.Index].ToCell().X > 28, "the survivor did not resume its attack-move");
    }

    [Fact]
    public void GuardingSapper_AttacksAnEnemyThatEntersSight_AndReturnsToItsPost()
    {
        var w = World();
        EntityId sapper = w.Inf(0, "dir_sapper", 20, 20);
        EntityId tank = w.Veh(1, "auth_tank", 32, 22);   // starts far out of sight, then drives to 3 cells off: in sight (4), outside the 1-cell reach
        CellCoord post = new(20, 20);
        w.Run(1, Passive(w, tank), w.Cmd(0, CommandKind.Guard, sapper, default, post, (int)ActorKind.Infantry));
        w.Run(1, w.Cmd(1, CommandKind.Move, tank, default, new CellCoord(23, 22), (int)ActorKind.Vehicle));
        w.Run(100);
        int hp = w.VehHp(tank);
        w.Run(200);
        Assert.True(w.VehHp(tank) < hp, "the guarding sapper ignored the tank in sight");
        w.Run(800);
        Assert.Equal(post, w.Sim.Infantry.Position[sapper.Index].ToCell());
    }
}
