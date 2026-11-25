// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Sim.Core;
using Rts.Sim.Rules;
using Rts.Sim.Tests.S18;
using PF = Rts.Sim.Tests.Production.ProductionFixture;
using SF = Rts.Sim.Tests.M3.Systems.SystemsFixture;

namespace Rts.Sim.Tests.A43;

/// <summary>Session 43, wave 3 (D-311): SIMECON-01 prerequisite pause, SIMECON-02/03 storm and no-build threshold, SIMECON-04 defeat rule,
/// SIMCORE-03 shift-queue survives a refused order.</summary>
public sealed class SimEconA43Tests
{
    private static QueueView QueueOf(Simulation sim, ProductionCategory c) => sim.HudFor(0).Queues.Single(q => q.Category == c);

    // ---- SIMECON-01 -------------------------------------------------------------------------------

    [Theory]
    [InlineData(ProductionCategory.Defence, "test_turret", "test_barracks", 8, 4)]      // turret needs the barracks
    [InlineData(ProductionCategory.Structure, "test_refinery", "test_power", 12, 4)]    // refinery needs the power plant
    public void StructureLane_PausesWhenThePrerequisiteBuildingIsLost_AndResumesWhenItIsBack(ProductionCategory cat, string item, string prereq, int px, int py)
    {
        var sim = DebugBase();
        PF.Step(sim, PF.Build(sim, cat, PF.Bld(item)));
        PF.Run(sim, 15);
        int spent = sim.Production.CreditsSpent(0, cat), progress = QueueOf(sim, cat).Items[0].ProgressPermille;
        Assert.InRange(progress, 1, 999);
        PF.Step(sim, PF.Sell(sim, PF.BuildingOf(sim, px, py)));
        int spentAfterSell = sim.Production.CreditsSpent(0, cat);
        int cash = sim.Players.Cash[0];
        PF.Run(sim, 300);
        Assert.Equal(spentAfterSell, sim.Production.CreditsSpent(0, cat));
        Assert.Equal(cash, sim.Players.Cash[0]);
        Assert.Equal(-1, sim.Production.ReadyType(0, cat));
        Assert.True(QueueOf(sim, cat).Items[0].Held);          // shown like an item on hold
        Assert.False(QueueOf(sim, cat).Items[0].Stalled);
        Assert.True(QueueOf(sim, cat).Items[0].ProgressPermille >= progress);
        int frozen = QueueOf(sim, cat).Items[0].ProgressPermille;

        PF.Step(sim, PF.Place(sim, PF.Bld(prereq), px, py));   // debug sim: free placement of the lost building
        Assert.False(PF.BuildingOf(sim, px, py).IsNone);
        PF.SyncPower(sim);
        PF.RunUntil(sim, () => sim.Production.ReadyType(0, cat) >= 0, 2000);
        Assert.Equal(PF.Bld(item), sim.Production.ReadyType(0, cat));
        Assert.True(frozen < 1000);
        Assert.False(QueueOf(sim, cat).Items.Count > 0);
    }

    private static Simulation DebugBase()
    {
        var sim = PF.NewSim(new[] { ("test_hq", 4, 4), ("test_power", 12, 4), ("test_barracks", 8, 4), ("test_factory", 4, 8) }, debug: true);
        sim.Players.Cash[0] = 5000;
        return sim;
    }

    private static Simulation SimWithTankNeedingPower()
    {
        SimRules rules = PF.Loaded.Sim;
        int tank = PF.Veh("test_tank");
        var vehicles = rules.Vehicles.ToArray();
        vehicles[tank] = vehicles[tank] with { Prerequisites = new[] { "test_factory", "test_power" } };
        rules = rules with { Vehicles = vehicles };
        var sim = new Simulation(new SimSetup(1, Rts.Sim.Map.MapData.CreateFlat(32, 32), 2, rules, PF.Loaded.RulesHash) { AllowDebug = true });
        foreach (var (id, x, y) in new[] { ("test_hq", 4, 4), ("test_power", 12, 4), ("test_barracks", 8, 4), ("test_factory", 4, 8) })
            Assert.False(sim.PlaceBuilding(0, PF.Bld(id), new CellCoord(x, y)).IsNone);
        PF.SyncPower(sim);
        sim.Players.Cash[0] = 5000;
        return sim;
    }

    [Fact]
    public void UnitLane_PausesWhenThePrerequisiteBuildingIsLost_AndResumes()
    {
        var sim = SimWithTankNeedingPower();
        int tank = PF.Veh("test_tank");
        PF.Step(sim, PF.Build(sim, ProductionCategory.Vehicle, tank), PF.Build(sim, ProductionCategory.Vehicle, tank));
        PF.Run(sim, 20);
        PF.Step(sim, PF.Sell(sim, PF.BuildingOf(sim, 12, 4)));
        int spent = sim.Production.CreditsSpent(0, ProductionCategory.Vehicle), cash = sim.Players.Cash[0];
        PF.Run(sim, 400);
        Assert.Equal(spent, sim.Production.CreditsSpent(0, ProductionCategory.Vehicle));
        Assert.Equal(cash, sim.Players.Cash[0]);
        Assert.Equal(0, sim.Units.Slots.Count);
        Assert.Equal(2, sim.Production.QueueLength(0, ProductionCategory.Vehicle));
        Assert.True(QueueOf(sim, ProductionCategory.Vehicle).Items[0].Held);

        PF.Step(sim, PF.Place(sim, PF.Bld("test_power"), 12, 4));
        PF.SyncPower(sim);
        PF.RunUntil(sim, () => sim.Units.Slots.Count >= 1, 3000);
        Assert.Equal(1, sim.Units.Slots.Count);
        Assert.False(QueueOf(sim, ProductionCategory.Vehicle).Items[0].Held);
    }

    [Fact]
    public void UpgradeLane_PausesWhenThePrerequisiteBuildingIsLost_AndResumes()
    {
        SimRules rules = SF.Rules();
        var upgrades = rules.Upgrades.ToArray();
        upgrades[0] = upgrades[0] with { Prerequisites = new[] { "tower" } };
        rules = rules with { Upgrades = upgrades };
        var sim = new Simulation(new SimSetup(1, SF.Map(), 2, rules, 0x3606UL) { AllowDebug = true });
        sim.PlaceBuilding(0, SF.Hq, new CellCoord(4, 4));
        sim.PlaceBuilding(0, SF.Lab, new CellCoord(8, 4));
        EntityId tower = sim.PlaceBuilding(0, SF.Tower, new CellCoord(12, 4));
        sim.Players.Cash[0] = 2000;
        SF.Run(sim, 40, SF.BuildUpgrade(sim, 0));
        int spent = sim.Production.CreditsSpent(0, ProductionCategory.Upgrade);
        Assert.InRange(spent, 1, 499);
        SF.Run(sim, 1, SF.Kill(sim, ActorKind.Building, tower));
        SF.Run(sim, 5);
        spent = sim.Production.CreditsSpent(0, ProductionCategory.Upgrade);
        int cash = sim.Players.Cash[0];
        SF.Run(sim, 300);
        Assert.Equal(spent, sim.Production.CreditsSpent(0, ProductionCategory.Upgrade));
        Assert.Equal(cash, sim.Players.Cash[0]);
        Assert.False(sim.HasUpgrade(0, 0));
        Assert.True(QueueOf(sim, ProductionCategory.Upgrade).Items[0].Held);

        SF.Run(sim, 1, SF.Cmd(sim, 0, CommandKind.Place, default, default, new CellCoord(12, 4), SF.Tower));
        for (int i = 0; i < 400 && !sim.HasUpgrade(0, 0); i++) sim.Step(SF.None);
        Assert.True(sim.HasUpgrade(0, 0));
        Assert.Equal(1500, sim.Players.Cash[0]);   // paid the cost exactly once
    }

    // ---- SIMECON-02 / SIMECON-03 -------------------------------------------------------------------

    [Fact]
    public void ShippedData_StormCapStaysBelowTheDamageThreshold_AndNoBuildThresholdIsReachable()
    {
        SimRules s = S18World.RealRules;
        WarheadType storm = s.Warheads.Single(w => w.StormTicks > 0);
        Assert.True(storm.StormContaminateCap < s.Radiation.DamageThreshold, "the storm must deny building, not kill");
        Assert.True(s.Radiation.NoBuildThreshold <= storm.StormContaminateCap, "the storm must be able to deny building");
        Assert.True(s.Radiation.NoBuildThreshold > 0);
    }

    [Fact]
    public void Radiation_AtTheNoBuildThreshold_RefusesAPlace_AndTheGhostCheck_UntilItDecays()
    {
        var sim = DebugBase();
        int power = PF.Bld("test_power");
        PF.Step(sim, PF.Build(sim, ProductionCategory.Structure, power));
        PF.RunUntil(sim, () => sim.Production.ReadyType(0, ProductionCategory.Structure) == power);
        var spot = new CellCoord(15, 4);
        Assert.True(sim.CanPlaceReady(0, power, spot));
        int threshold = PF.Loaded.Sim.Radiation.NoBuildThreshold;
        sim.Ground.Radiation[sim.Ground.IndexOf(new CellCoord(16, 5))] = (byte)threshold;   // one footprint cell
        Assert.False(sim.CanPlaceReady(0, power, spot));
        PF.Step(sim, PF.Place(sim, power, 15, 4));
        Assert.True(PF.BuildingOf(sim, 15, 4).IsNone);
        Assert.Equal(power, sim.Production.ReadyType(0, ProductionCategory.Structure));   // still waiting

        sim.Ground.Radiation[sim.Ground.IndexOf(new CellCoord(16, 5))] = (byte)(threshold - 1);
        Assert.True(sim.CanPlaceReady(0, power, spot));
        PF.Step(sim, PF.Place(sim, power, 15, 4));
        Assert.False(PF.BuildingOf(sim, 15, 4).IsNone);
    }

    // ---- SIMECON-04 --------------------------------------------------------------------------------

    private static (S18World W, EntityId Bld) DirectorateWithOnlyA(string vehicle)
    {
        var w = new S18World(Faction.Directorate, Faction.Authority, keepAlive: false);
        w.Place(1, "auth_conyard", 10, 50);
        EntityId b = w.Place(0, "dir_smelter_vent", 10, 10);
        w.Veh(0, vehicle, 30, 20);
        w.Run(2);
        return (w, b);
    }

    [Fact]
    public void Defeat_DirectorateWithOnlyABore_IsDefeated_BecauseTheHatchIsNotABase()
    {
        var (w, b) = DirectorateWithOnlyA("dir_bore");
        w.Sim.Buildings.Health[b.Index] = 0;
        w.Run(3);
        Assert.True(w.Sim.Players.Defeated[0]);
    }

    [Fact]
    public void Defeat_AFounderThatDeploysIntoARootBuilding_StillCountsAsABase()
    {
        var w = new S18World(Faction.Expedition, Faction.Authority, waterFromX: 40, keepAlive: false);
        w.Place(1, "auth_conyard", 10, 50);
        EntityId b = w.Place(0, "exp_dredge_works", 10, 10);
        w.Veh(0, "exp_command_ship", 45, 20);
        w.Run(2);
        w.Sim.Buildings.Health[b.Index] = 0;
        w.Run(3);
        Assert.False(w.Sim.Players.Defeated[0]);
    }

    // ---- SIMCORE-03 --------------------------------------------------------------------------------

    private static (Simulation Sim, EntityId Unit) UnitWithTwoWaypoints()
    {
        var sim = SF.NewSim();
        EntityId u = sim.SpawnVehicle(0, SF.Grunt, new CellCoord(5, 5));
        sim.SpawnVehicle(1, SF.Grunt, new CellCoord(30, 30));
        SF.Run(sim, 2);
        SF.Run(sim, 1, SF.Move(sim, 0, ActorKind.Vehicle, u, 10, 5));
        SF.Run(sim, 1, SF.Cmd(sim, 0, CommandKind.QueueOrder, u, default, new CellCoord(12, 8), (int)ActorKind.Vehicle));
        SF.Run(sim, 1, SF.Cmd(sim, 0, CommandKind.QueueOrder, u, default, new CellCoord(14, 8), (int)ActorKind.Vehicle));
        Assert.Equal(2, sim.WaypointCountOf(new ActorRef(ActorKind.Vehicle, u)));
        return (sim, u);
    }

    [Fact]
    public void RefusedPlainOrders_LeaveTheShiftQueueIntact()
    {
        var (sim, u) = UnitWithTwoWaypoints();
        var actor = new ActorRef(ActorKind.Vehicle, u);
        SF.Run(sim, 1, SF.Move(sim, 0, ActorKind.Vehicle, u, 500, 500));   // off the map
        Assert.Equal(2, sim.WaypointCountOf(actor));
        EntityId dead = sim.Units.Slots.IdOf(2);   // the enemy grunt spawned in the setup
        Assert.Equal(1, sim.Units.Player[dead.Index]);
        SF.Run(sim, 1, SF.Kill(sim, ActorKind.Vehicle, dead));
        SF.Run(sim, 3);
        SF.Run(sim, 1, SF.Cmd(sim, 0, CommandKind.Attack, u, dead, default, (int)ActorKind.Vehicle | ((int)ActorKind.Vehicle << 8)));
        Assert.Equal(2, sim.WaypointCountOf(actor));
    }

    [Fact]
    public void AcceptedPlainOrder_StillReplacesTheShiftQueue()
    {
        var (sim, u) = UnitWithTwoWaypoints();
        SF.Run(sim, 1, SF.Move(sim, 0, ActorKind.Vehicle, u, 20, 20));
        Assert.Equal(0, sim.WaypointCountOf(new ActorRef(ActorKind.Vehicle, u)));
    }
}
