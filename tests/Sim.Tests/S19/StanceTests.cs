// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Sim.Core;
using Rts.Sim.Numerics;
using Rts.Sim.Tests.S18;

namespace Rts.Sim.Tests.S19;

/// <summary>S19 D-205 (M13-15): the three stances. Aggressive is today's behaviour, Defensive gives up a chase after
/// <see cref="Simulation.DefensiveChaseCells"/> cells and walks back, HoldFire never fires unless ordered to attack.</summary>
public sealed class StanceTests
{
    private static int Arg(ActorKind k, UnitStance s) => (int)k | ((int)s << 8);

    private static Command SetStance(S18World w, EntityId id, UnitStance s) =>
        w.Cmd(0, CommandKind.SetStance, id, default, default, Arg(ActorKind.Infantry, s));

    // Spawned units have no mission and look for no targets until ordered, so the fire tests put the rifleman on guard at its post.
    private static Command GuardHere(S18World w, EntityId id) =>
        w.Cmd(0, CommandKind.Guard, id, default, w.Sim.Infantry.Position[id.Index].ToCell(), (int)ActorKind.Infantry);

    [Fact]
    public void DefaultStanceIsAggressive_AndSetStanceChangesIt()
    {
        var w = new S18World();
        EntityId rifle = w.Inf(0, "auth_rifleman", 20, 20);
        Assert.Equal(UnitStance.Aggressive, w.Sim.StanceOf(new ActorRef(ActorKind.Infantry, rifle)));
        w.Run(1, SetStance(w, rifle, UnitStance.HoldFire));
        Assert.Equal(UnitStance.HoldFire, w.Sim.StanceOf(new ActorRef(ActorKind.Infantry, rifle)));
        w.Run(1, SetStance(w, rifle, UnitStance.Defensive));
        Assert.Equal(UnitStance.Defensive, w.Sim.StanceOf(new ActorRef(ActorKind.Infantry, rifle)));
    }

    [Fact]
    public void SetStance_IsRefused_ForAnotherPlayersUnit_AndForABadValue()
    {
        var w = new S18World();
        EntityId mine = w.Inf(0, "auth_rifleman", 20, 20);
        EntityId theirs = w.Inf(1, "auth_rifleman", 40, 40);
        w.Run(1, SetStance(w, theirs, UnitStance.HoldFire), w.Cmd(0, CommandKind.SetStance, mine, default, default, (int)ActorKind.Infantry | (9 << 8)));
        Assert.Equal(UnitStance.Aggressive, w.Sim.StanceOf(new ActorRef(ActorKind.Infantry, theirs)));
        Assert.Equal(UnitStance.Aggressive, w.Sim.StanceOf(new ActorRef(ActorKind.Infantry, mine)));
    }

    [Fact]
    public void Aggressive_FiresAtAnEnemyInRange_HoldFireDoesNot_UntilOrderedToAttack()
    {
        // An unarmed Decon Truck cannot shoot back, so only the stance decides whether it is hurt.
        int Hp(UnitStance? stance, bool order)
        {
            var w = new S18World();
            EntityId rifle = w.Inf(0, "auth_rifleman", 20, 20);
            EntityId truck = w.Veh(1, "auth_decon_truck", 22, 20);
            w.Run(1, GuardHere(w, rifle));
            if (stance is { } s) w.Run(1, SetStance(w, rifle, s));
            if (order) w.Run(1, w.Attack(0, ActorKind.Infantry, rifle, ActorKind.Vehicle, truck));
            w.Run(120);
            return w.VehHp(truck);
        }

        int full = Hp(UnitStance.HoldFire, false);
        Assert.True(Hp(null, false) < full);                     // today's behaviour
        Assert.True(Hp(UnitStance.Defensive, false) < full);     // in range, so a defensive unit fires too
        Assert.True(Hp(UnitStance.HoldFire, true) < full);       // an explicit order is always obeyed
    }

    [Fact]
    public void HoldFire_AttackMove_MarchesWithoutFiring()
    {
        var w = new S18World();
        EntityId rifle = w.Inf(0, "auth_rifleman", 20, 20);
        EntityId truck = w.Veh(1, "auth_decon_truck", 24, 20);
        w.Run(1, GuardHere(w, rifle));
        w.Run(1, SetStance(w, rifle, UnitStance.HoldFire));
        int before = w.VehHp(truck);
        w.Run(1, w.Cmd(0, CommandKind.AttackMove, rifle, default, new CellCoord(28, 20), (int)ActorKind.Infantry));
        w.Run(250);
        Assert.Equal(before, w.VehHp(truck));
        Assert.Equal(new CellCoord(28, 20), w.Sim.Infantry.Position[rifle.Index].ToCell());
    }

    // The post is (20,20); the lure drives east. Returns the farthest the guard strays (in cells x 256) and where it ends up.
    private static (int MaxStray, CellCoord End) Chase(UnitStance stance)
    {
        var w = new S18World();
        EntityId rifle = w.Inf(0, "auth_rifleman", 20, 20);
        EntityId lure = w.Inf(1, "auth_engineer", 22, 20);   // as slow as the rifleman, unarmed, and in sight long enough to be chased
        w.Run(1, GuardHere(w, rifle));
        w.Run(1, SetStance(w, rifle, stance));
        w.Run(1, w.Cmd(1, CommandKind.Move, lure, default, new CellCoord(45, 20), (int)ActorKind.Infantry));
        int max = 0;
        for (int t = 0; t < 700; t++)
        {
            w.Run(1);
            LeptonCoord p = w.Sim.Infantry.Position[rifle.Index];
            max = Math.Max(max, LeptonMath.Distance(p, LeptonCoord.CenterOf(new CellCoord(20, 20))));
        }
        return (max, w.Sim.Infantry.Position[rifle.Index].ToCell());
    }

    [Fact]
    public void Defensive_GivesUpTheChaseSooner_AndWalksBackToItsPost()
    {
        var (aggMax, _) = Chase(UnitStance.Aggressive);
        var (defMax, defEnd) = Chase(UnitStance.Defensive);
        Assert.True(defMax < aggMax, $"defensive {defMax} should stray less than aggressive {aggMax}");
        Assert.True(defMax <= (Simulation.DefensiveChaseCells + 2) * 256, $"defensive strayed {defMax} leptons");
        Assert.Equal(new CellCoord(20, 20), defEnd);
    }

    [Fact]
    public void Stance_SurvivesSaveAndLoad_AndIsHashed()
    {
        var w = new S18World();
        EntityId rifle = w.Inf(0, "auth_rifleman", 20, 20);
        ulong plain = w.Sim.ComputeStateHash();
        w.Run(1, SetStance(w, rifle, UnitStance.Defensive));
        Assert.NotEqual(plain, w.Sim.ComputeStateHash());
        using var ms = new MemoryStream();
        w.Sim.Save(ms);
        ms.Position = 0;
        Simulation copy = Simulation.Load(ms, w.Sim.Setup);
        Assert.Equal(UnitStance.Defensive, copy.StanceOf(new ActorRef(ActorKind.Infantry, rifle)));
        Assert.Equal(w.Sim.ComputeStateHash(), copy.ComputeStateHash());
        for (int i = 0; i < 60; i++) { w.Sim.Step(Array.Empty<Command>()); copy.Step(Array.Empty<Command>()); }
        Assert.Equal(w.Sim.ComputeStateHash(), copy.ComputeStateHash());
    }

    [Fact]
    public void SettingTheDefaultStance_LeavesTheHashUntouched()
    {
        var a = new S18World();
        var b = new S18World();
        EntityId ra = a.Inf(0, "auth_rifleman", 20, 20);
        b.Inf(0, "auth_rifleman", 20, 20);
        a.Run(5, SetStance(a, ra, UnitStance.Aggressive));
        b.Run(5);
        Assert.Equal(b.Sim.ComputeStateHash(), a.Sim.ComputeStateHash());
        Assert.Equal(b.Sim.ComputeHashes(out _).Count, a.Sim.ComputeHashes(out _).Count);
    }

    [Fact]
    public void ARecycledSlot_StartsAggressive()
    {
        var w = new S18World();
        EntityId rifle = w.Inf(0, "auth_rifleman", 20, 20);
        w.Run(1, SetStance(w, rifle, UnitStance.HoldFire));
        w.Run(1, w.Cmd(0, CommandKind.Debug, default, default, default, (int)DebugOp.Kill, (int)ActorKind.Infantry) with { Subject = rifle });
        w.Run(3);
        Assert.False(w.Sim.Infantry.Slots.IsAlive(rifle));
        w.Run(1, w.Cmd(0, CommandKind.Debug, default, default, new CellCoord(21, 20), (int)DebugOp.SpawnInfantry, w.I("auth_rifleman")));
        w.Run(2);
        Assert.True(w.Sim.Infantry.Slots.IsIndexAlive(rifle.Index));   // the freed slot is reused
        var fresh = new ActorRef(ActorKind.Infantry, w.Sim.Infantry.Slots.IdOf(rifle.Index));
        Assert.Equal(UnitStance.Aggressive, w.Sim.StanceOf(fresh));
    }
}
