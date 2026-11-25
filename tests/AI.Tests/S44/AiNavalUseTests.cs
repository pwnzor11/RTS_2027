// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.AI;
using Rts.AI.Recruit;
using Rts.Data;
using Rts.Sim;
using Rts.Sim.Core;
using Rts.Sim.Map;
using Rts.Sim.Missions;
using Rts.Sim.Rules;
using Rts.Sim.View;
using Xunit;

namespace AI.Tests.S44;

/// <summary>s44 (M16-M-03): the Expedition AI builds one Drydock, heavy ships and at most one Minelayer from <c>naval_min_tier</c> up, and sends damaged ships to the Drydock.</summary>
public sealed class AiNavalUseTests
{
    private static SimRules Rules => TestWorld.Rules.Sim;
    private static int Veh(string id) => SimRules.IndexOf(Rules.Vehicles, id, v => v.Id);
    private static int Bld(string id) => SimRules.IndexOf(Rules.Buildings, id, v => v.Id);

    private sealed class Log : ICommandSink
    {
        public readonly List<(CommandKind Kind, EntityId Subject, CellCoord Cell)> Orders = new();
        public void Issue(CommandKind kind, EntityId subject, ActorKind subjectKind, CellCoord cell = default, ActorRef target = default, int arg2 = 0) => Orders.Add((kind, subject, cell));
        public void Build(ProductionCategory category, int typeIndex) { }
        public void Place(int buildingType, CellCoord topLeft) { }
        public void Rally(EntityId factory, CellCoord cell) { }
    }

    private static ViewActor Actor(ActorKind kind, int type, ushort slot = 1) =>
        new(new EntityId(slot, 1), kind, 0, Relation.Self, type, new CellCoord(5, 5), new LeptonCoord(1280, 1280), 100, 100, MissionKind.None, true, EntityId.None, false, 1, 1);

    private static RecruitKnowledge Knowledge(DifficultyProfile p, params (ProductionCategory, int)[] buildable)
    {
        _ = p;
        var k = new RecruitKnowledge
        {
            Rules = Rules,
            View = TestWorld.FlatSim().ViewFor(0),
            Hud = new PlayerHud(5000, 0, 10000, 100, 0, new[] { new QueueView(ProductionCategory.Vehicle, Array.Empty<QueueItemView>(), -1) }, buildable, false),
            BuildingCount = new int[Rules.Buildings.Count],
            HaveYard = true,
        };
        return k;
    }

    // ---- data -----------------------------------------------------------------------------------------

    [Fact]
    public void TheShippedData_EnablesTheNavalLevers_FromTierThreeOnly()
    {
        foreach (DifficultyProfile p in TestWorld.Tiers)
        {
            FactionTuning t = p.FactionKeys.TuningOf((int)Faction.Expedition);
            Assert.Equal(3, t.NavalMinTier);   // one per-faction table; the tier gate is the key itself
            Assert.True(t.NavalMinelayers <= 1 && t.NavalCruisers <= 2 && t.NavalCarriers <= 1);
        }
        Assert.Equal(FactionTuning.Default.NavalMinTier, FactionAiKeys.Default.TuningOf((int)Faction.Authority).NavalMinTier);
        Assert.Equal(0, FactionTuning.Default.NavalReturnPct);
    }

    // ---- build order: a Drydock -----------------------------------------------------------------------

    private static int DrydockSteps(DifficultyProfile p, bool shipyard, bool buildable)
    {
        var faction = new ExpeditionRecruitFaction();
        var build = new BuildOrderManager(faction.DefaultOrder, faction.PowerId, faction.YardId);
        var planner = new TierPlanner(p, faction);
        RecruitKnowledge k = buildable ? Knowledge(p, (ProductionCategory.Structure, Bld("exp_drydock"))) : Knowledge(p);
        if (shipyard) k.BuildingCount[Bld("exp_shipyard")] = 1;
        planner.ExtendBuild(k, build);
        planner.ExtendBuild(k, build);   // idempotent
        return build.StepsOf("exp_drydock");
    }

    [Fact]
    public void BuildOrder_PicksOneDrydock_AtTierThree_OnceAShipyardStandsAndItIsBuildable()
    {
        Assert.Equal(1, DrydockSteps(TestWorld.Tiers[2], shipyard: true, buildable: true));
        Assert.Equal(1, DrydockSteps(TestWorld.Tiers[4], shipyard: true, buildable: true));
    }

    [Fact]
    public void BuildOrder_NoDrydock_BelowTierThree_WithoutAShipyard_OrWhenLocked()
    {
        Assert.Equal(0, DrydockSteps(TestWorld.Tiers[0], true, true));
        Assert.Equal(0, DrydockSteps(TestWorld.Tiers[1], true, true));
        Assert.Equal(0, DrydockSteps(TestWorld.Tiers[2], false, true));
        Assert.Equal(0, DrydockSteps(TestWorld.Tiers[2], true, false));
    }

    // ---- fleet bag: heavy ships and the Minelayer cap -------------------------------------------------

    private static int Wanted(DifficultyProfile p, int ownMinelayers, bool heavyBuildable = true, bool layerBuildable = true)
    {
        var t = new ExpeditionTactics(p);
        var buildable = new List<(ProductionCategory, int)>();
        if (heavyBuildable) { buildable.Add((ProductionCategory.Vehicle, Veh("exp_cruiser"))); buildable.Add((ProductionCategory.Vehicle, Veh("exp_carrier"))); }
        if (layerBuildable) buildable.Add((ProductionCategory.Vehicle, Veh("exp_minelayer")));
        buildable.Add((ProductionCategory.Vehicle, Veh("exp_destroyer")));
        RecruitKnowledge k = Knowledge(p, buildable.ToArray());
        k.BuildingCount = new int[Rules.Buildings.Count];
        ushort slot = 1;
        k.Army.Add(Actor(ActorKind.Vehicle, Veh("exp_glide_tank"), slot++));
        k.Army.Add(Actor(ActorKind.Vehicle, Veh("exp_glide_tank"), slot++));
        k.Harvesters.Add(Actor(ActorKind.Vehicle, Veh("exp_skimmer"), slot++));
        ViewActor dd = Actor(ActorKind.Vehicle, Veh("exp_destroyer"), slot++);
        k.Fleet.Add(dd); k.Own.Add(dd);
        for (int i = 0; i < ownMinelayers; i++) { ViewActor m = Actor(ActorKind.Vehicle, Veh("exp_minelayer"), slot++); k.Utility.Add(m); k.Own.Add(m); }
        return t.WantedVehicle(k);
    }

    [Fact]
    public void FleetBag_BuildsAtMostOneMinelayer_AtTierThree()
    {
        DifficultyProfile p = TestWorld.Tiers[2];
        Assert.Equal(Veh("exp_minelayer"), Wanted(p, ownMinelayers: 0));
        Assert.NotEqual(Veh("exp_minelayer"), Wanted(p, ownMinelayers: 1));
    }

    [Fact]
    public void FleetBag_NoMinelayer_BelowTierThree_OrWhenNotBuildable()
    {
        Assert.NotEqual(Veh("exp_minelayer"), Wanted(TestWorld.Tiers[1], 0));
        Assert.NotEqual(Veh("exp_minelayer"), Wanted(TestWorld.Tiers[0], 0));
        Assert.NotEqual(Veh("exp_minelayer"), Wanted(TestWorld.Tiers[2], 0, layerBuildable: false));
    }

    [Fact]
    public void FleetBag_AddsAHeavyShip_AtTierThree_ButNotAtTierTwo()
    {
        int heavy3 = Wanted(TestWorld.Tiers[2], ownMinelayers: 1);
        Assert.True(heavy3 == Veh("exp_carrier") || heavy3 == Veh("exp_cruiser"), "a Carrier or Cruiser once the Drydock makes them buildable");
        Assert.Equal(Veh("exp_destroyer"), Wanted(TestWorld.Tiers[2], 1, heavyBuildable: false));   // no Drydock: the old destroyer
        Assert.Equal(Veh("exp_destroyer"), Wanted(TestWorld.Tiers[1], 1));   // tier 2: the old bag (plan wants no heavy ships)
    }

    // ---- a damaged ship returns to the Drydock ---------------------------------------------------------

    private const int Size = 40, Coast = 10;

    private static (Simulation Sim, EntityId Ship, int Slot) World()
    {
        var cells = new LandType[Size * Size];
        for (int y = 0; y < Size; y++) for (int x = 0; x < Size; x++) cells[y * Size + x] = x >= Coast ? LandType.Water : LandType.Clear;
        var map = new MapData("coast", "rust_belt", Size, Size, new ushort[Size * Size], cells, new byte[Size * Size], new OverlayKind[Size * Size],
            new byte[Size * Size], new[] { new CellCoord(1, 1), new CellCoord(Size - 2, Size - 2) });
        var sim = new Simulation(new SimSetup(1, map, 2, Rules with { Supply = Rules.Supply with { Enabled = false } }, TestWorld.Rules.RulesHash) { Factions = [Faction.Expedition, Faction.Authority] });
        Assert.False(sim.PlaceBuilding(0, Bld("exp_landing_depot"), new CellCoord(6, 6)).IsNone);
        Assert.False(sim.PlaceBuilding(0, Bld("exp_plotting_room"), new CellCoord(2, 10)).IsNone);
        Assert.False(sim.PlaceBuilding(0, Bld("exp_core_hall"), new CellCoord(2, 14)).IsNone);
        Assert.False(sim.PlaceBuilding(0, Bld("exp_shipyard"), new CellCoord(10, 2)).IsNone);
        Assert.False(sim.PlaceBuilding(0, Bld("exp_drydock"), new CellCoord(10, 8)).IsNone);
        EntityId ship = sim.SpawnVehicle(0, Veh("exp_destroyer"), new CellCoord(32, 9));
        sim.Step(Array.Empty<Command>());
        return (sim, ship, ship.Index);
    }

    private static List<(CommandKind Kind, EntityId Subject, CellCoord Cell)> Fleet(DifficultyProfile p, int healthPct)
    {
        (Simulation sim, EntityId ship, int slot) = World();
        sim.Units.Health[slot] = Rules.Vehicles[Veh("exp_destroyer")].MaxHealth * healthPct / 100;
        var k = new RecruitKnowledge();
        k.Refresh(sim.ViewFor(0), "exp_landing_depot");
        var t = new ExpeditionTactics(p);
        var sink = new Log();
        t.Early(k, sink, new CellCoord(8, 8), new List<ViewActor>(), new TierPlanner(p, new ExpeditionRecruitFaction()));
        return sink.Orders.FindAll(o => o.Subject == ship && o.Kind == CommandKind.Move);
    }

    [Fact]
    public void ADamagedIdleShip_SailsToTheDrydock_AtTierThree()
    {
        var moves = Fleet(TestWorld.Tiers[2], 45);   // above the old 40 % floor, below naval_return_pct
        Assert.NotEmpty(moves);
        CellCoord c = moves[0].Cell;
        Assert.InRange(c.X, 9, 14);   // next to the 4x3 Drydock at (10,8)
        Assert.InRange(c.Y, 7, 11);
    }

    [Fact]
    public void ADamagedShip_AboveTheOldFloor_StaysPut_AtTierTwo()
    {
        foreach (var m in Fleet(TestWorld.Tiers[1], 45)) Assert.False(m.Cell.X <= 14 && m.Cell.Y is >= 7 and <= 11, "tier 2: no repair trip (the old floor is 40 %)");
    }

    [Fact]
    public void AHealthyShip_IsNotSentToTheDrydock()
    {
        foreach (var m in Fleet(TestWorld.Tiers[2], 100)) Assert.False(m.Cell.X <= 14 && m.Cell.Y is >= 7 and <= 11, "no repair trip at full health");
    }
}
