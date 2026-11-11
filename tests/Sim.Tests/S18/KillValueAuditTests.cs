// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Sim.Core;
using Rts.Sim.Rules;

namespace Rts.Sim.Tests.S18;

/// <summary>S18 audit, FACTION-LE-04 / FACTION-MATH-04 / SIM-RULES-08: kill value is the victim's rules cost, and three types carry a
/// "do not build" cost (packed rig 4000, Supply Tender and Command Ship 6000). One kill makes the shooter a Hero; the wreck is worth 1000.</summary>
public sealed class KillValueAuditTests
{
    /// <summary>A fresh Authority tank kills one enemy vehicle that has 10 hit points left; returns the tank's rank and kill value.</summary>
    private static (int Rank, int KillValue, bool VictimDead) OneKill(string victim, bool atSea)
    {
        var w = new S18World(Faction.Authority, atSea ? Faction.Expedition : Faction.LeftBehind, waterFromX: 40);
        EntityId tank = w.Veh(0, "auth_tank", 37, 20);
        EntityId v = w.Veh(1, victim, atSea ? 41 : 34, 20);
        w.Sim.Units.Health[v.Index] = 10;
        w.Run(1, w.Attack(0, ActorKind.Vehicle, tank, ActorKind.Vehicle, v));
        w.Run(200);
        return (w.Sim.RankOf(ActorKind.Vehicle, tank.Index), w.Sim.KillValueOf(ActorKind.Vehicle, tank.Index), !w.Sim.Units.Slots.IsAlive(v));
    }

    [Fact]
    public void RealCosts_AndNotBuildableFlags_AndTheHeroThreshold()
    {
        // D-205 (LE-04): the three non-buildable vehicles carry real costs
        SimRules r = S18World.RealRules;
        Assert.Equal(200, r.Vehicles.Single(v => v.Id == "lb_mother_rig_packed").Cost);
        Assert.Equal(1500, r.Vehicles.Single(v => v.Id == "exp_supply_tender").Cost);
        Assert.Equal(900, r.Vehicles.Single(v => v.Id == "exp_command_ship").Cost);
        foreach (string id in new[] { "lb_mother_rig_packed", "exp_supply_tender", "exp_command_ship" })
            Assert.False(r.Vehicles.Single(v => v.Id == id).Buildable, id);
        Assert.Equal(3, r.Vehicles.Count(v => !v.Buildable));
        Assert.Equal(new[] { 600, 1600, 3200 }, r.Veterancy.Thresholds);
        Assert.Equal(1000, r.Veterancy.KillPermilleUnit);
    }

    [Theory]
    [InlineData("exp_supply_tender", true, 1500, 1)]
    [InlineData("exp_command_ship", true, 900, 1)]
    [InlineData("lb_mother_rig_packed", false, 200, 0)]
    public void OneKillOfAFormerPlaceholderCostVehicle_FollowsItsRealCost(string victim, bool atSea, int value, int expectedRank)
    {
        var (rank, killValue, dead) = OneKill(victim, atSea);
        Assert.True(dead);
        Assert.Equal(value, killValue);
        Assert.Equal(expectedRank, rank);
    }

    [Fact]
    public void Audit_OneKillOfAnOrdinaryTank_IsWorthItsCost_AndOneRank()
    {
        // the comparison case: an 800-credit tank is one rank (Veteran)
        var (rank, killValue, dead) = OneKill("auth_tank", false);
        Assert.True(dead);
        Assert.Equal(800, killValue);
        Assert.Equal(1, rank);
    }

    private static int WreckValueAt(Simulation sim, CellCoord cell)
    {
        int slot = sim.FindWreckAt(cell);
        return slot == 0 ? 0 : sim.Salvage.ValueStart[slot];
    }

    [Fact]
    public void PackedStillRig_LeavesTheSameWreckAsTheUnpackedRig()
    {
        var w = new S18World(Faction.LeftBehind, Faction.Authority);
        Assert.Equal(200, w.Rules.Buildings[w.B("lb_still_rig")].Cost);
        EntityId packed = w.Veh(0, "lb_mother_rig_packed", 20, 20);
        w.Sim.Rigs.PackedFrom[packed.Index] = w.B("lb_still_rig") + 1;   // this packed vehicle carries a Still Rig
        EntityId still = w.Place(0, "lb_still_rig", 30, 20);
        BuildingType bt = w.Rules.Buildings[w.B("lb_still_rig")];
        var centre = new CellCoord(30 + bt.FootprintW / 2, 20 + bt.FootprintH / 2);
        w.Run(2);
        w.Run(1,
            w.Cmd(0, CommandKind.Debug, packed, default, default, (int)DebugOp.Kill, (int)ActorKind.Vehicle),
            w.Cmd(0, CommandKind.Debug, still, default, default, (int)DebugOp.Kill, (int)ActorKind.Building));
        w.Run(2);

        Assert.Equal(50, WreckValueAt(w.Sim, new CellCoord(20, 20)));     // 25 % of the 200 the packed rig carries
        Assert.Equal(50, WreckValueAt(w.Sim, centre));                    // 25 % of 200
    }

    [Fact]
    public void Audit_EveryPackedRig_IsThe1000HitPointHeavyVehicle_WhateverItCarries()
    {
        // S18 audit: pins current behaviour of FACTION-LE-04 (packed form); flip the assertion when fixed.
        SimRules r = S18World.RealRules;
        VehicleType packed = r.Vehicles.Single(v => v.Id == "lb_mother_rig_packed");
        Assert.Equal(1000, packed.MaxHealth);
        Assert.Equal(ArmorClass.Heavy, packed.Armor);
        int packedIndex = SimRules.IndexOf(r.Vehicles, "lb_mother_rig_packed", v => v.Id);
        BuildingType[] rigs = r.Buildings.Where(b => b.PacksInto >= 0).ToArray();
        Assert.True(rigs.Length >= 6);
        Assert.All(rigs, b => Assert.Equal(packedIndex, b.PacksInto));           // one generic packed vehicle for all of them
        Assert.Contains(rigs, b => b.MaxHealth <= 300);                           // e.g. a 250-300 hp rig drives around with 1000
    }

    [Fact]
    public void OneKillOfASupplyTender_DoesNotMakeAHero()
    {
        var (rank, _, dead) = OneKill("exp_supply_tender", true);
        Assert.True(dead);
        Assert.True(rank < 3);
    }
}
