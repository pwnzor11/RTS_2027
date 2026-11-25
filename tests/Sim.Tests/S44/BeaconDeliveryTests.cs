// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Data;
using Rts.Sim.Core;
using Rts.Sim.Map;
using Rts.Sim.Rules;
using Rts.Sim.World;

namespace Rts.Sim.Tests.S44;

/// <summary>S44 M-15: Landing Beacon delivered by a landing craft or Skyhook, built by a Pioneer or Combat Diver, on the shipped rules.
/// Map 60x40: land x &lt; 30, water x &gt;= 30. Depot at (6,6) (zone radius 10: x up to about 18), so (25,20) and (28,20) are far outside the zone.</summary>
public sealed class BeaconDeliveryTests
{
    private const int W = 60, H = 40, Coast = 30;
    private ushort _seq;

    private static readonly RuleSet Loaded = RulesLoader.Load(RulesLocator.FindRulesDir(AppContext.BaseDirectory)!);
    private static int Veh(string id) => SimRules.IndexOf(Loaded.Sim.Vehicles, id, v => v.Id);
    private static int Inf(string id) => SimRules.IndexOf(Loaded.Sim.Infantry, id, v => v.Id);
    private static int Bld(string id) => SimRules.IndexOf(Loaded.Sim.Buildings, id, v => v.Id);
    private static readonly int BeaconCost = Loaded.Sim.Buildings[Bld("exp_beacon")].Cost;

    private static SimSetup NewSetup()
    {
        var cells = new LandType[W * H];
        for (int y = 0; y < H; y++) for (int x = 0; x < W; x++) cells[y * W + x] = x >= Coast ? LandType.Water : LandType.Clear;
        var map = new MapData("coast", "rust_belt", W, H, new ushort[W * H], cells, new byte[W * H], new OverlayKind[W * H], new byte[W * H],
            new[] { new CellCoord(1, 1), new CellCoord(W - 2, H - 2) });
        return new SimSetup(1, map, 2, Loaded.Sim with { Supply = Loaded.Sim.Supply with { Enabled = false } }, Loaded.RulesHash)
            { Factions = new[] { Faction.Expedition, Faction.Authority }, AllowDebug = true };
    }

    private static Simulation NewWorld(int cash = 5000)
    {
        var sim = new Simulation(NewSetup());
        Assert.False(sim.PlaceBuilding(0, Bld("exp_landing_depot"), new CellCoord(6, 6)).IsNone, "depot");
        sim.Players.Cash[0] = cash;
        return sim;
    }

    private Command Drop(Simulation s, EntityId who, CellCoord at, ActorKind kind, byte player = 0) =>
        new(s.Tick, player, ++_seq, CommandKind.DropBeacon, who, default, at, (int)kind);

    private static void Run(Simulation s, int n) { for (int i = 0; i < n; i++) s.Step(Array.Empty<Command>()); }

    private static bool BeaconAt(Simulation s, CellCoord c) =>
        !s.Map.BuildingAt(c).IsNone && s.Buildings.TypeIndex[s.Map.BuildingAt(c).Index] == Bld("exp_beacon");

    /// <summary>Steps until a beacon stands on the cell (returns the ticks taken) or the limit passes (-1).</summary>
    private static int WaitBeacon(Simulation s, CellCoord c, int max = 600)
    {
        for (int i = 1; i <= max; i++) { s.Step(Array.Empty<Command>()); if (BeaconAt(s, c)) return i; }
        return -1;
    }

    [Fact]
    public void DataIsWired()
    {
        Assert.Equal("exp_beacon", Loaded.Sim.Vehicles[Veh("exp_skyhook")].BeaconDrop);
        Assert.Equal("exp_beacon", Loaded.Sim.Vehicles[Veh("exp_landing_craft")].BeaconDrop);
        Assert.Equal("exp_beacon", Loaded.Sim.Infantry[Inf("exp_pioneer")].BeaconBuild);
        Assert.Equal("exp_beacon", Loaded.Sim.Infantry[Inf("exp_diver")].BeaconBuild);
        Assert.Equal(0, Loaded.Sim.Vehicles.Count(v => v.BeaconDrop.Length > 0) - 2);
        Assert.Equal(0, Loaded.Sim.Infantry.Count(v => v.BeaconBuild.Length > 0) - 2);
    }

    [Fact]
    public void ABeaconOutsideTheZone_CannotBePlacedFromTheQueue_ButASkyhookDropsIt_AndTheZoneGrows()
    {
        Simulation s = NewWorld();
        var cell = new CellCoord(25, 20);
        Assert.Equal(PlacementResult.OutsideBeachhead, s.CanPlace(0, Bld("exp_beacon"), cell));
        EntityId sky = s.SpawnVehicle(0, Veh("exp_skyhook"), new CellCoord(15, 20));
        int funds = s.Funds(0);
        s.Step(new[] { Drop(s, sky, cell, ActorKind.Vehicle) });
        Assert.True(WaitBeacon(s, cell) > 0, "the beacon was dropped");
        Assert.Equal(funds - BeaconCost, s.Funds(0));
        Assert.True(s.InBeachhead(0, 20 * W + 25 + 5));
        Assert.True(s.InBeachhead(0, 20 * W + 25 - 5));
        Assert.Equal(PlacementResult.Ok, s.CanPlace(0, Bld("exp_pillbox"), new CellCoord(27, 20)));   // the zone now reaches here
        Assert.False(s.BeaconOrders.IsSet(ActorKind.Vehicle, sky.Index));
    }

    [Fact]
    public void ALandingCraftBeachesAtTheShoreAndDropsABeaconOnTheSoftCellBesideIt()
    {
        Simulation s = NewWorld();
        var cell = new CellCoord(29, 20);
        EntityId craft = s.SpawnVehicle(0, Veh("exp_landing_craft"), new CellCoord(40, 12));
        s.Step(new[] { Drop(s, craft, cell, ActorKind.Vehicle) });
        Assert.True(s.BeaconOrders.IsSet(ActorKind.Vehicle, craft.Index));
        Assert.True(WaitBeacon(s, cell, 1500) > 0, "the beacon was dropped");
        Assert.True(s.InBeachhead(0, 20 * W + 29));
    }

    [Fact]
    public void ALandingCraftIsRefusedAnInlandCell_AndAnyoneIsRefusedWaterOrABuilding()
    {
        Simulation s = NewWorld();
        EntityId craft = s.SpawnVehicle(0, Veh("exp_landing_craft"), new CellCoord(40, 12));
        EntityId sky = s.SpawnVehicle(0, Veh("exp_skyhook"), new CellCoord(15, 20));
        s.Step(new[] { Drop(s, craft, new CellCoord(20, 20), ActorKind.Vehicle) });   // no sea cell beside it
        Assert.False(s.BeaconOrders.IsSet(ActorKind.Vehicle, craft.Index));
        s.Step(new[] { Drop(s, sky, new CellCoord(40, 20), ActorKind.Vehicle) });     // water
        Assert.False(s.BeaconOrders.IsSet(ActorKind.Vehicle, sky.Index));
        s.Step(new[] { Drop(s, sky, new CellCoord(6, 6), ActorKind.Vehicle) });       // the depot
        Assert.False(s.BeaconOrders.IsSet(ActorKind.Vehicle, sky.Index));
        s.Step(new[] { Drop(s, sky, new CellCoord(100, 100), ActorKind.Vehicle) });   // off the map
        Assert.False(s.BeaconOrders.IsSet(ActorKind.Vehicle, sky.Index));
    }

    [Fact]
    public void AnActorWithoutTheAbility_ThePoorAndTheOtherPlayer_AreRefused()
    {
        Simulation s = NewWorld(cash: BeaconCost - 1);
        var cell = new CellCoord(25, 20);
        EntityId sky = s.SpawnVehicle(0, Veh("exp_skyhook"), new CellCoord(24, 20));
        EntityId marine = s.SpawnInfantry(0, Inf("exp_marine"), new CellCoord(24, 21));
        s.Step(new[] { Drop(s, sky, cell, ActorKind.Vehicle) });
        Assert.False(s.BeaconOrders.IsSet(ActorKind.Vehicle, sky.Index));   // cannot pay
        s.Players.Cash[0] = 5000;
        s.Step(new[] { Drop(s, marine, cell, ActorKind.Infantry) });
        Assert.False(s.BeaconOrders.IsSet(ActorKind.Infantry, marine.Index));
        s.Step(new[] { Drop(s, sky, cell, ActorKind.Vehicle, player: 1) });
        Assert.False(s.BeaconOrders.IsSet(ActorKind.Vehicle, sky.Index));
        Run(s, 50);
        Assert.False(BeaconAt(s, cell));
        Assert.Equal(5000, s.Funds(0));
    }

    [Fact]
    public void APioneerBuildsABeaconOverItsBuildTime_StandingNextToTheCell()
    {
        Simulation s = NewWorld();
        var cell = new CellCoord(25, 20);
        EntityId p = s.SpawnInfantry(0, Inf("exp_pioneer"), new CellCoord(24, 20));
        int buildTicks = Loaded.Sim.Buildings[Bld("exp_beacon")].BuildTime;
        Assert.True(buildTicks > 0);
        s.Step(new[] { Drop(s, p, cell, ActorKind.Infantry) });
        Assert.True(s.BeaconOrders.IsSet(ActorKind.Infantry, p.Index));
        Run(s, buildTicks / 2);
        Assert.False(BeaconAt(s, cell));
        Assert.Equal(5000, s.Funds(0));   // paid at placement
        int t = WaitBeacon(s, cell);
        Assert.InRange(t, buildTicks / 2 - 5, buildTicks);
        Assert.Equal(5000 - BeaconCost, s.Funds(0));
        Assert.Equal(PlacementResult.Ok, s.CanPlace(0, Bld("exp_pillbox"), new CellCoord(27, 20)));
        Assert.False(s.BeaconOrders.IsSet(ActorKind.Infantry, p.Index));
    }

    [Fact]
    public void APioneerWalksUpFirst_AndLeavingCancelsTheBuild()
    {
        Simulation s = NewWorld();
        var cell = new CellCoord(25, 20);
        EntityId p = s.SpawnInfantry(0, Inf("exp_pioneer"), new CellCoord(18, 20));
        EntityId q = s.SpawnInfantry(0, Inf("exp_pioneer"), new CellCoord(24, 30));
        s.Step(new[] { Drop(s, p, cell, ActorKind.Infantry) });
        Assert.True(s.BeaconOrders.IsSet(ActorKind.Infantry, p.Index));
        Assert.True(WaitBeacon(s, cell, 1200) > 0, "walked up and built");

        var cell2 = new CellCoord(25, 30);
        s.Step(new[] { Drop(s, q, cell2, ActorKind.Infantry) });
        Run(s, 40);
        s.Step(new[] { new Command(s.Tick, 0, ++_seq, CommandKind.Move, q, default, new CellCoord(10, 35), (int)ActorKind.Infantry) });
        Run(s, 400);
        Assert.False(BeaconAt(s, cell2));
        Assert.False(s.BeaconOrders.IsSet(ActorKind.Infantry, q.Index));
    }

    [Fact]
    public void ACombatDiverOnLandBuildsABeaconToo_AndADeadBuilderCancelsTheOrder()
    {
        Simulation s = NewWorld();
        var cell = new CellCoord(22, 15);
        EntityId d = s.SpawnInfantry(0, Inf("exp_diver"), new CellCoord(21, 15));
        EntityId d2 = s.SpawnInfantry(0, Inf("exp_diver"), new CellCoord(21, 25));
        s.Step(new[] { Drop(s, d, cell, ActorKind.Infantry) });
        Assert.True(WaitBeacon(s, cell) > 0);

        var cell2 = new CellCoord(22, 25);
        s.Step(new[] { Drop(s, d2, cell2, ActorKind.Infantry) });
        s.Step(new[] { new Command(s.Tick, 0, ++_seq, CommandKind.Debug, d2, default, default, (int)DebugOp.Kill, (int)ActorKind.Infantry) });
        Run(s, 400);
        Assert.False(BeaconAt(s, cell2));
    }

    [Fact]
    public void ABeaconOrder_SurvivesSaveAndLoad_AndIsHashed()
    {
        SimSetup setup = NewSetup();
        Simulation Make()
        {
            var sim = new Simulation(setup);
            sim.PlaceBuilding(0, Bld("exp_landing_depot"), new CellCoord(6, 6));
            sim.Players.Cash[0] = 5000;
            return sim;
        }
        Simulation s = Make();
        Simulation idle = Make();
        EntityId p = s.SpawnInfantry(0, Inf("exp_pioneer"), new CellCoord(24, 20));
        idle.SpawnInfantry(0, Inf("exp_pioneer"), new CellCoord(24, 20));
        s.Step(new[] { Drop(s, p, new CellCoord(25, 20), ActorKind.Infantry) });
        idle.Step(Array.Empty<Command>());
        Run(s, 60); Run(idle, 60);
        Assert.NotEqual(idle.ComputeStateHash(), s.ComputeStateHash());   // the pending order and its progress are state
        var ms = new MemoryStream();
        s.Save(ms);
        ms.Position = 0;
        Simulation loaded = Simulation.Load(ms, setup);
        Assert.Equal(s.ComputeStateHash(), loaded.ComputeStateHash());
        Assert.True(loaded.BeaconOrders.IsSet(ActorKind.Infantry, p.Index));
        Assert.Equal(s.BeaconOrders.ProgressOf(ActorKind.Infantry, p.Index), loaded.BeaconOrders.ProgressOf(ActorKind.Infantry, p.Index));
        Run(s, 300); Run(loaded, 300);
        Assert.Equal(s.ComputeStateHash(), loaded.ComputeStateHash());
        Assert.True(BeaconAt(loaded, new CellCoord(25, 20)));
        Assert.Equal("beacon_orders", s.StateParts[^1].Name);
    }

    [Fact]
    public void SameInputs_GiveTheSameHash()
    {
        ulong Play()
        {
            _seq = 0;
            Simulation s = NewWorld();
            EntityId sky = s.SpawnVehicle(0, Veh("exp_skyhook"), new CellCoord(15, 20));
            EntityId p = s.SpawnInfantry(0, Inf("exp_pioneer"), new CellCoord(20, 30));
            s.Step(new[] { Drop(s, sky, new CellCoord(25, 20), ActorKind.Vehicle), Drop(s, p, new CellCoord(25, 31), ActorKind.Infantry) });
            Run(s, 500);
            return s.ComputeStateHash();
        }
        Assert.Equal(Play(), Play());
    }
}
