// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Data;
using Rts.Sim.Core;
using Rts.Sim.Rules;

namespace Rts.Sim.Tests.S18;

/// <summary>S18 audit, FACTION-LE-02 and FACTION-LE-19: the defeat rule and the Expedition's Command Ship on the shipped data, with the
/// skirmish defeat rule (NoRebuildDefeat, D-173) switched on.</summary>
public sealed class DefeatAuditTests
{
    private const int LongEnough = SimConstants.NoRebuildDefeatTicks + 300;

    private static void KillEverythingOf(Simulation sim, byte player)
    {
        for (int i = 1; i < sim.Units.Capacity; i++)
            if (sim.Units.Slots.IsIndexAlive(i) && sim.Units.Player[i] == player) sim.Units.Health[i] = 0;
        for (int i = 1; i < sim.Infantry.Capacity; i++)
            if (sim.Infantry.Slots.IsIndexAlive(i) && sim.Infantry.Player[i] == player) sim.Infantry.Health[i] = 0;
    }

    private static int LiveActorsOf(Simulation sim, byte player)
    {
        int n = 0;
        for (int i = 1; i < sim.Units.Capacity; i++) if (sim.Units.Slots.IsIndexAlive(i) && sim.Units.Player[i] == player) n++;
        for (int i = 1; i < sim.Infantry.Capacity; i++) if (sim.Infantry.Slots.IsIndexAlive(i) && sim.Infantry.Player[i] == player) n++;
        for (int i = 1; i < sim.Buildings.Capacity; i++) if (sim.Buildings.Slots.IsIndexAlive(i) && sim.Buildings.Player[i] == player) n++;
        return n;
    }

    /// <summary>Player 0 = <paramref name="faction"/> with its founder at sea or on land and two soldiers; player 1 = Authority with a conyard.</summary>
    private static (S18World W, EntityId Founder) FounderOnly(Faction faction)
    {
        var w = new S18World(faction, Faction.Authority, waterFromX: 40, noRebuild: true, keepAlive: false);
        w.Place(1, "auth_conyard", 10, 50);
        EntityId founder = faction == Faction.Expedition ? w.Veh(0, "exp_command_ship", 45, 20) : w.Veh(0, "lb_mother_rig_packed", 30, 20);
        string soldier = faction == Faction.Expedition ? "exp_marine" : "lb_scavenger";
        w.Inf(0, soldier, 30, 22);
        w.Inf(0, soldier, 31, 22);
        return (w, founder);
    }

    [Fact]
    public void Audit_LeftBehindWhosePackedRigDiesBeforeUnpacking_IsDefeatedAtOnce()
    {
        // S18 audit: the comparison case for FACTION-LE-02: a packed rig counts as a base; since M13-02 a live Command Ship does too.
        var (w, rig) = FounderOnly(Faction.LeftBehind);
        w.Run(1);
        w.Sim.Units.Health[rig.Index] = 0;
        w.Run(3);

        Assert.True(w.Sim.Players.Defeated[0]);
        Assert.True(w.Sim.GameOver);
    }

    [Fact]
    public void Audit_SkirmishStartOnAMapWithNoSeaInReach_SpawnsNoCommandShip_AndThatPlayerIsNeverDefeated()
    {
        // S18 audit: pins current behaviour of FACTION-LE-02 (case c); flip the assertion when fixed.
        var w = new S18World(Faction.Expedition, Faction.Authority, noRebuild: true, keepAlive: false);   // all land
        SkirmishStart.Apply(w.Sim);
        int ship = w.V("exp_command_ship");
        bool hasShip = false;
        for (int i = 1; i < w.Sim.Units.Capacity; i++)
            if (w.Sim.Units.Slots.IsIndexAlive(i) && w.Sim.Units.Player[i] == 0 && w.Sim.Units.TypeIndex[i] == ship) hasShip = true;
        Assert.False(hasShip);                       // silently skipped: nothing reports it
        Assert.True(LiveActorsOf(w.Sim, 0) > 0);     // the land part of the kit did spawn

        w.Run(10);
        KillEverythingOf(w.Sim, 0);
        w.Run(LongEnough);

        Assert.Equal(0, LiveActorsOf(w.Sim, 0));
        Assert.False(w.Sim.Players.Defeated[0]);
        Assert.False(w.Sim.GameOver);
    }

    [Fact]
    public void Desired_ExpeditionWithNoFounderNoBuildingAndNoUnits_IsDefeated()
    {
        var (w, ship) = FounderOnly(Faction.Expedition);
        w.Run(1);
        w.Sim.Units.Health[ship.Index] = 0;
        w.Run(200);
        KillEverythingOf(w.Sim, 0);
        w.Run(LongEnough);
        Assert.True(w.Sim.Players.Defeated[0]);
        Assert.True(w.Sim.GameOver);
    }

    /// <summary>Player 0 owns one base building and a spare founder vehicle; player 1 = Authority with a conyard.</summary>
    private static (S18World W, EntityId Base) BaseAndSpareFounder(Faction faction)
    {
        var w = new S18World(faction, Faction.Authority, waterFromX: 40, noRebuild: true, keepAlive: false);
        w.Place(1, "auth_conyard", 10, 50);
        EntityId b;
        if (faction == Faction.Expedition)
        {
            b = w.Place(0, "exp_landing_depot", 34, 20);
            w.Veh(0, "exp_command_ship", 45, 30);      // a second Command Ship, afloat and able to deploy
        }
        else
        {
            b = w.Place(0, "lb_mother_rig", 30, 20);
            w.Veh(0, "lb_mother_rig_packed", 30, 30);  // a second packed Mother Rig
        }
        return (w, b);
    }

    [Fact]
    public void LeftBehindWithAPackedRigAndNoBuilding_IsNotDefeated()
    {
        var (lb, rig) = BaseAndSpareFounder(Faction.LeftBehind);
        lb.Run(5);
        lb.Run(1, lb.Cmd(0, CommandKind.Debug, rig, default, default, (int)DebugOp.Kill, (int)ActorKind.Building));
        lb.Run(300);
        Assert.False(lb.Sim.Players.Defeated[0]);
    }

    [Fact]
    public void Desired_ExpeditionWithALiveCommandShip_IsNotDefeatedWhenItsLastBuildingFalls()
    {
        var (exp, depot) = BaseAndSpareFounder(Faction.Expedition);
        exp.Run(5);
        exp.Run(1, exp.Cmd(0, CommandKind.Debug, depot, default, default, (int)DebugOp.Kill, (int)ActorKind.Building));
        exp.Run(300);
        Assert.False(exp.Sim.Players.Defeated[0]);
    }
}
