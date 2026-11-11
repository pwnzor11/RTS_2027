// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using System.Reflection;
using Rts.Data;
using Rts.Sim.Core;
using Rts.Sim.Map;
using Rts.Sim.Missions;
using Rts.Sim.Rules;
using Rts.Sim.Tests.Economy;
using Rts.Sim.Tests.M7;
using static Rts.Sim.Tests.Combat.CombatFixture;

namespace Rts.Sim.Tests.S18;

/// <summary>S18 audit, the smaller sim-rules findings: SIM-RULES-09 (yard sell loop), -10 (soldiers of a carried APC when the ship sinks),
/// -12 (damage-0 weapons deal 1), -13 (harvesters stay idle after a refinery is rebuilt).</summary>
public sealed class MinorRulesAuditTests
{
    private static SimRules RealRules() =>
        RulesLoader.Load(RulesLocator.FindRulesDir(AppContext.BaseDirectory) ?? throw new InvalidOperationException("data/rules not found")).Sim;

    // ---- SIM-RULES-12 --------------------------------------------------------------------------

    // S18 audit: pins current behaviour of SIM-RULES-12; flip the assertion when fixed.
    [Fact]
    public void Audit_ZeroDamageAgainstAZeroPercentArmour_StillDealsOne()
    {
        var nothing = new WarheadType("nothing", new[] { 0, 0, 0, 0, 0 }, 1, false);
        foreach (ArmorClass armor in Enum.GetValues<ArmorClass>()) Assert.Equal(1, Simulation.ComputeDamage(0, nothing, armor));
    }

    // S18 audit: pins current behaviour of SIM-RULES-12; flip the assertion when fixed.
    [Fact]
    public void Audit_ShippedData_HasUtilityWeaponsWithDamageZero_ThatThereforeDealOne()
    {
        SimRules r = RealRules();
        string[] zero = r.Weapons.Where(w => w.Damage == 0).Select(w => w.Id).OrderBy(x => x, StringComparer.Ordinal).ToArray();
        Assert.Equal(new[] { "auth_decon_spray", "auth_foam_spray", "dir_ash_sow", "dir_blackout_drop", "lb_dust_drop" }, zero);
        foreach (WeaponType w in r.Weapons.Where(w => w.Damage == 0))
            foreach (ArmorClass armor in Enum.GetValues<ArmorClass>())
                Assert.Equal(1, Simulation.ComputeDamage(w.Damage, r.Warheads[w.Warhead], armor));
    }

    [Fact(Skip = "S18 SIM-RULES-12: awaiting owner decision")]
    public void Desired_ZeroDamage_DealsNothing()
    {
        var nothing = new WarheadType("nothing", new[] { 0, 0, 0, 0, 0 }, 1, false);
        Assert.Equal(0, Simulation.ComputeDamage(0, nothing, ArmorClass.Light));
    }

    // ---- SIM-RULES-13 --------------------------------------------------------------------------

    /// <summary>Harvester loads, the only refinery dies, a new refinery is placed on open ground (it brings its own free harvester, which
    /// works normally). Returns the ORIGINAL harvester's mission, whether it moved, and its cargo, <paramref name="after"/> ticks later.</summary>
    private static (MissionKind Mission, bool Moved, int Cargo, bool RefineryBack, int EarnedByTheNewOne) RebuildRefinery(int after)
    {
        var (sim, h) = EconomyFixture.Basic(silo: true);   // the silo keeps the player alive (and able to give orders) without a refinery
        EconomyFixture.RunUntil(sim, () => sim.Economy.Cargo[h.Index] > 0);
        EntityId refinery = sim.Map.BuildingAt(EconomyFixture.Refinery);
        sim.Step(new[] { new Command(sim.Tick, 0, 1, CommandKind.Debug, refinery, default, default, (int)DebugOp.Kill, (int)ActorKind.Building) });
        EconomyFixture.RunUntil(sim, () => sim.Units.Mission[h.Index] == MissionKind.Guard);   // full, nowhere to unload: it gives up
        var site = new CellCoord(20, 20);   // open ground well away from the harvester
        sim.Step(new[] { new Command(sim.Tick, 0, 2, CommandKind.Place, default, default, site, Bld("test_refinery")) });
        bool back = !sim.Map.BuildingAt(site).IsNone;
        LeptonCoord parked = sim.Units.Position[h.Index];
        Run(sim, after);
        return (sim.Units.Mission[h.Index], sim.Units.Position[h.Index] != parked, sim.Economy.Cargo[h.Index], back, sim.StatsOf(0).CreditsHarvested);
    }

    [Fact]
    public void Harvester_Resumes_AfterTheRefineryIsRebuilt()
    {
        var r = RebuildRefinery(after: 2000);
        Assert.True(r.Moved);
        Assert.NotEqual(MissionKind.Guard, r.Mission);
    }

    // ---- SIM-RULES-09 --------------------------------------------------------------------------

    // S18 audit: pins current behaviour of SIM-RULES-09 (rule + shipped numbers); flip the assertion when fixed.
    [Fact]
    public void Audit_YardPlaceAndSell_GrantsAFreeHarvesterEveryCycle_ForHalfItsPrice()
    {
        SimRules r = RealRules();
        int yard = SimRules.IndexOf(r.Buildings, "lb_yard", b => b.Id), gleaner = SimRules.IndexOf(r.Vehicles, "lb_gleaner", v => v.Id);
        Assert.Equal(gleaner, r.Buildings[yard].FreeUnit);
        int refund = (int)((long)r.Economy.SellRefundPermille * r.Buildings[yard].Cost / 1000);
        Assert.Equal(600, r.Buildings[yard].Cost);
        Assert.Equal(600, r.Vehicles[gleaner].Cost);
        Assert.Equal(300, refund);                                         // net price of each harvester through the loop: 600 - 300

        var sim = new Simulation(new SimSetup(1, MapData.CreateFlat(40, 40), 2, r, 1) { AllowDebug = true });
        Assert.False(sim.PlaceBuilding(0, SimRules.IndexOf(r.Buildings, "lb_mother_rig", b => b.Id), new CellCoord(30, 30)).IsNone);   // a base, so selling the yard is not defeat
        int funds = sim.Funds(0);
        const int Cycles = 5;
        for (int k = 0; k < Cycles; k++)
        {
            var site = new CellCoord(4 + 7 * k, 20);   // a fresh site each time: the free harvester idles by the old one
            sim.Step(new[] { new Command(sim.Tick, 0, (ushort)(2 * k), CommandKind.Place, default, default, site, yard) });   // debug place = "a finished yard"
            EntityId b = sim.Map.BuildingAt(site);
            Assert.False(b.IsNone);
            sim.Step(new[] { new Command(sim.Tick, 0, (ushort)(2 * k + 1), CommandKind.Sell, b, default, default, (int)ActorKind.Building) });
            Assert.True(sim.Map.BuildingAt(site).IsNone);
        }
        int gleaners = 0;
        for (int i = 1; i < sim.Units.Capacity; i++) if (sim.Units.Slots.IsIndexAlive(i) && sim.Units.TypeIndex[i] == gleaner) gleaners++;
        Assert.Equal(Cycles, gleaners);                                    // one more harvester per cycle, no factory involved
        Assert.Equal(funds + Cycles * refund, sim.Funds(0));
    }

    // ---- SIM-RULES-10 --------------------------------------------------------------------------

    private const int Craft = 0, Tank = 3, Marine = 0;   // VehiclePassengerTests' type indexes

    /// <summary>VehiclePassengerTests' coast scene (private there, borrowed by reflection) with the tank turned into a 2-seat APC: a marine
    /// rides the APC, the APC rides the craft, the craft sails to deep water and is destroyed.</summary>
    private static (Simulation Sim, EntityId Craft, EntityId Apc, EntityId Marine) SinkLoadedCraft()
    {
        var setup = (SimSetup)typeof(VehiclePassengerTests).GetMethod("Setup", BindingFlags.NonPublic | BindingFlags.Static)!.Invoke(null, null)!;
        VehicleType[] vehicles = setup.Rules.Vehicles.ToArray();
        vehicles[Tank] = vehicles[Tank] with { Passengers = 2 };
        var s = new Simulation(setup with { Rules = setup.Rules with { Vehicles = vehicles } });
        EntityId craft = s.SpawnVehicle(0, Craft, new CellCoord(9, 10));
        EntityId apc = s.SpawnVehicle(0, Tank, new CellCoord(10, 10));
        EntityId marine = s.SpawnInfantry(0, Marine, new CellCoord(11, 10));
        ushort seq = 0;
        Command C(CommandKind k, EntityId who, EntityId target, CellCoord cell, int arg) => new(s.Tick, 0, ++seq, k, who, target, cell, arg);
        s.Step(new[] { C(CommandKind.Enter, marine, apc, default, (int)ActorKind.Infantry | ((int)ActorKind.Vehicle << 8)) });
        for (int t = 0; t < 100 && s.Tunnels.InfantryLayer[marine.Index] != ActorLayer.Passenger; t++) s.Step(None);
        Assert.Equal(ActorLayer.Passenger, s.Tunnels.InfantryLayer[marine.Index]);
        s.Step(new[] { C(CommandKind.Enter, apc, craft, default, (int)ActorKind.Vehicle | ((int)ActorKind.Vehicle << 8)) });
        for (int t = 0; t < 100 && s.Tunnels.VehicleLayer[apc.Index] != ActorLayer.Passenger; t++) s.Step(None);
        Assert.Equal(ActorLayer.Passenger, s.Tunnels.VehicleLayer[apc.Index]);
        s.Step(new[] { C(CommandKind.Move, craft, default, new CellCoord(3, 20), (int)ActorKind.Vehicle) });
        for (int t = 0; t < 600 && s.Units.Position[craft.Index].ToCell() != new CellCoord(3, 20); t++) s.Step(None);
        Assert.Equal(new CellCoord(3, 20), s.Units.Position[craft.Index].ToCell());
        s.Step(new[] { C(CommandKind.Debug, craft, default, default, (int)DebugOp.Kill) with { Arg2 = (int)ActorKind.Vehicle } });
        for (int t = 0; t < 5; t++) s.Step(None);
        return (s, craft, apc, marine);
    }

    // S18 audit: pins current behaviour of SIM-RULES-10; flip the assertion when fixed.
    [Fact]
    public void Audit_SoldierInACarriedApc_SurvivesTheSinking_StandingOnDeepWater()
    {
        var (s, craft, apc, marine) = SinkLoadedCraft();
        Assert.False(s.Units.Slots.IsAlive(craft));
        Assert.False(s.Units.Slots.IsAlive(apc));
        Assert.True(s.Infantry.Slots.IsAlive(marine));                                        // the marine outlived both
        Assert.Equal(ActorLayer.Surface, s.Tunnels.InfantryLayer[marine.Index]);
        CellCoord at = s.Infantry.Position[marine.Index].ToCell();
        Assert.Equal(LandType.Water, s.Map.LandAt(at));                                       // on deep sea
        Assert.Equal(0, s.Setup.Rules.Terrain.SpeedPercent(LandType.Water, Locomotion.Foot)); // where foot cannot stand
    }

    [Fact(Skip = "S18 SIM-RULES-10: awaiting owner decision")]
    public void Desired_SoldierInACarriedApc_IsLostWithTheShip()
    {
        var (s, _, _, marine) = SinkLoadedCraft();
        Assert.False(s.Infantry.Slots.IsAlive(marine));
    }
}
