// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Data;
using Rts.Game.Controls;
using Rts.Game.Sandbox;
using Rts.Sim.Core;
using Rts.Sim.Rules;

namespace Rts.Game.Tests.M3;

/// <summary>M3-08: which Deploy / Enter orders the controls offer, from rules built in code (no Directorate data needed), and how Enter is encoded.</summary>
public sealed class M3OrdersTests
{
    private static readonly SimRules Rules = Build();

    private static SimRules Build()
    {
        string dir = RulesLocator.FindRulesDir(AppContext.BaseDirectory) ?? throw new InvalidOperationException("data/rules not found");
        SimRules baseRules = RulesLoader.Load(dir).Sim;
        VehicleType v = baseRules.Vehicles[0];
        InfantryType i = baseRules.Infantry[0];
        BuildingType b = baseRules.Buildings[0];
        return baseRules with
        {
            Vehicles = new[]
            {
                v with { Passengers = 0, DeploysInto = -1, TunnelSize = TunnelSize.None },                                  // 0 plain
                v with { Passengers = 5, DeploysInto = -1, TunnelSize = TunnelSize.M },                                     // 1 transport
                v with { Passengers = 0, DeploysInto = 0, Locomotion = Locomotion.Tracked, TunnelSize = TunnelSize.L },     // 2 land-train
                v with { Passengers = 0, DeploysInto = 0, Locomotion = Locomotion.Burrow, TunnelSize = TunnelSize.M },      // 3 bore
            },
            Infantry = new[] { i with { TunnelSize = TunnelSize.S }, i with { TunnelSize = TunnelSize.None }, i with { Engineer = true } },   // 2 engineer
            Buildings = new[]
            {
                b with { Entrance = TunnelSize.None, MaxHealth = 100 },   // 0 plain
                b with { Entrance = TunnelSize.S },      // 1 hatch
                b with { Entrance = TunnelSize.M },      // 2 bore head
            },
        };
    }

    [Theory]
    [InlineData(0, DeployMode.None)]
    [InlineData(1, DeployMode.InPlace)]   // a transport unloads
    [InlineData(2, DeployMode.InPlace)]   // the land-train deploys where it stands
    [InlineData(3, DeployMode.AtCell)]    // the Bore digs at a chosen cell
    public void Deploy_mode_follows_the_vehicle_type(int type, DeployMode expected) =>
        Assert.Equal(expected, M3Orders.DeployModeOf(Rules, ActorKind.Vehicle, type));

    [Fact]
    public void Infantry_and_unknown_types_do_not_deploy()
    {
        Assert.Equal(DeployMode.None, M3Orders.DeployModeOf(Rules, ActorKind.Infantry, 0));
        Assert.Equal(DeployMode.None, M3Orders.DeployModeOf(Rules, ActorKind.Vehicle, 99));
    }

    [Theory]
    [InlineData(ActorKind.Infantry, 0, 1, true)]    // S infantry into a hatch (S)
    [InlineData(ActorKind.Infantry, 0, 2, true)]    // S infantry into a bore head (M)
    [InlineData(ActorKind.Vehicle, 3, 2, true)]     // M vehicle into an M entrance
    [InlineData(ActorKind.Vehicle, 3, 1, false)]    // M vehicle does not fit an S hatch
    [InlineData(ActorKind.Vehicle, 2, 2, false)]    // an L land-train never uses the network
    [InlineData(ActorKind.Vehicle, 0, 1, false)]    // Authority vehicle has no tunnel size
    [InlineData(ActorKind.Infantry, 1, 1, false)]   // infantry type with no tunnel size
    [InlineData(ActorKind.Infantry, 0, 0, false)]   // building without an entrance
    [InlineData(ActorKind.Infantry, 0, 99, false)]
    public void Enter_tunnel_is_offered_only_when_the_unit_fits_the_entrance(ActorKind kind, int unit, int building, bool expected) =>
        Assert.Equal(expected, M3Orders.CanEnterTunnel(Rules, kind, unit, building));

    [Theory]
    [InlineData(ActorKind.Infantry, 1, true)]
    [InlineData(ActorKind.Infantry, 0, false)]   // not a transport
    [InlineData(ActorKind.Vehicle, 1, false)]    // vehicles do not board
    [InlineData(ActorKind.Infantry, 99, false)]
    public void Board_is_offered_to_infantry_for_transports(ActorKind kind, int transport, bool expected) =>
        Assert.Equal(expected, M3Orders.CanBoard(Rules, kind, transport));

    [Fact]
    public void Enter_is_encoded_like_attack_with_both_kinds_in_Arg()
    {
        var unit = new UnitKey(ActorKind.Infantry, new EntityId(3, 1));
        var target = new ActorRef(ActorKind.Building, new EntityId(9, 2));
        Command c = OrderBuilder.Build(5, 0, 0, CommandKind.Enter, unit, default, target);
        Assert.Equal(CommandKind.Enter, c.Kind);
        Assert.Equal(unit.Id, c.Subject);
        Assert.Equal(target.Id, c.Target);
        Assert.Equal((int)ActorKind.Infantry | ((int)ActorKind.Building << 8), c.Arg);
    }

    [Fact]
    public void Deploy_carries_the_target_cell_and_the_subject_kind()
    {
        var unit = new UnitKey(ActorKind.Vehicle, new EntityId(3, 1));
        Command c = OrderBuilder.Build(5, 0, 0, CommandKind.Deploy, unit, new CellCoord(12, 8), default);
        Assert.Equal(new CellCoord(12, 8), c.Cell);
        Assert.Equal((int)ActorKind.Vehicle, c.Arg);
        Assert.True(c.Target.IsNone);
    }

    [Theory]
    [InlineData(ActorKind.Infantry, 2, false, 0, 100, EngineerOrder.Capture)]   // engineer on an enemy or neutral building (any health)
    [InlineData(ActorKind.Infantry, 2, false, 0, 10, EngineerOrder.Capture)]
    [InlineData(ActorKind.Infantry, 2, true, 0, 60, EngineerOrder.Repair)]      // own damaged building
    [InlineData(ActorKind.Infantry, 2, true, 0, 100, EngineerOrder.None)]       // own building at full health
    [InlineData(ActorKind.Infantry, 0, false, 0, 100, EngineerOrder.None)]      // plain infantry
    [InlineData(ActorKind.Vehicle, 2, false, 0, 100, EngineerOrder.None)]       // a vehicle of the same index is no engineer
    [InlineData(ActorKind.Infantry, 2, false, 99, 100, EngineerOrder.None)]     // unknown building type
    public void Engineer_orders_follow_ownership_and_health(ActorKind kind, int unit, bool own, int building, int health, EngineerOrder expected) =>
        Assert.Equal(expected, M3Orders.EngineerOrderOn(Rules, kind, unit, own, building, health));

    [Fact]
    public void Engineer_enter_is_encoded_with_infantry_subject_and_building_target()
    {
        var engineer = new UnitKey(ActorKind.Infantry, new EntityId(4, 1));
        var building = new ActorRef(ActorKind.Building, new EntityId(2, 1));
        Command c = OrderBuilder.Build(5, 0, 0, CommandKind.Enter, engineer, default, building);
        Assert.Equal((int)ActorKind.Infantry | ((int)ActorKind.Building << 8), c.Arg);   // the sim routes this to ApplyEngineer
    }
}
