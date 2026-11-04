// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Data;
using Rts.Game.Controls;
using Rts.Game.Render;
using Rts.Sim.Core;
using Rts.Sim.Rules;

namespace Rts.Game.Tests.Wiring;

/// <summary>M4-09: right-click order picking on buildings (Enter vs Capture vs Attack vs Move), Unload, pips and foam alpha.</summary>
public sealed class M4GarrisonOrderTests
{
    private static readonly SimRules Rules = Build();

    // Infantry: 0 rifleman (can garrison), 1 engineer, 2 rifleman that may not garrison. Buildings: 0 plain, 1 blockhouse (5), 2 hatch.
    private static SimRules Build()
    {
        string dir = RulesLocator.FindRulesDir(AppContext.BaseDirectory) ?? throw new InvalidOperationException("data/rules not found");
        SimRules b = RulesLoader.Load(dir).Sim;
        InfantryType i = b.Infantry[0];
        BuildingType bt = b.Buildings[0];
        return b with
        {
            Infantry = new[]
            {
                i with { Engineer = false, CanGarrison = true, TunnelSize = TunnelSize.S },
                i with { Engineer = true, CanGarrison = false, TunnelSize = TunnelSize.None },
                i with { Engineer = false, CanGarrison = false, TunnelSize = TunnelSize.None },
            },
            Buildings = new[]
            {
                bt with { Garrison = 0, Entrance = TunnelSize.None, MaxHealth = 100 },
                bt with { Garrison = 5, Entrance = TunnelSize.None, MaxHealth = 100 },
                bt with { Garrison = 0, Entrance = TunnelSize.S, MaxHealth = 100 },
            },
        };
    }

    private static BuildingView View(int type, int occupants = 0, int health = 100, bool ghost = false) =>
        new(new EntityId(1, 1), 0, type, new CellCoord(3, 3), 2, 2, health, ghost) { Occupants = occupants };

    [Theory]
    [InlineData(Relation.Self, 0, BuildingClick.Garrison)]
    [InlineData(Relation.Ally, 0, BuildingClick.Garrison)]
    [InlineData(Relation.Neutral, 0, BuildingClick.Garrison)]
    [InlineData(Relation.Enemy, 0, BuildingClick.Attack)]
    public void Rifleman_on_a_blockhouse(Relation relation, int occupants, BuildingClick expected) =>
        Assert.Equal(expected, GarrisonOrders.Classify(Rules, ActorKind.Infantry, 0, View(1, occupants), relation));

    [Fact]
    public void A_neutral_building_that_already_has_occupants_is_attacked_not_entered() =>
        Assert.Equal(BuildingClick.Attack, GarrisonOrders.Classify(Rules, ActorKind.Infantry, 0, View(1, 2), Relation.Neutral));

    [Fact]
    public void A_full_own_blockhouse_falls_through_to_a_move() =>
        Assert.Equal(BuildingClick.None, GarrisonOrders.Classify(Rules, ActorKind.Infantry, 0, View(1, 5), Relation.Self));

    [Fact]
    public void Engineer_captures_neutral_and_enemy_garrisonables_instead_of_entering()
    {
        Assert.Equal(BuildingClick.Capture, GarrisonOrders.Classify(Rules, ActorKind.Infantry, 1, View(1), Relation.Neutral));
        Assert.Equal(BuildingClick.Capture, GarrisonOrders.Classify(Rules, ActorKind.Infantry, 1, View(1), Relation.Enemy));
    }

    [Fact]
    public void Engineer_repairs_a_damaged_own_building_but_a_healthy_one_is_a_move()
    {
        Assert.Equal(BuildingClick.Repair, GarrisonOrders.Classify(Rules, ActorKind.Infantry, 1, View(0, health: 50), Relation.Self));
        Assert.Equal(BuildingClick.None, GarrisonOrders.Classify(Rules, ActorKind.Infantry, 1, View(0), Relation.Self));
    }

    [Fact]
    public void Soldiers_that_cannot_garrison_and_vehicles_never_garrison()
    {
        Assert.Equal(BuildingClick.Attack, GarrisonOrders.Classify(Rules, ActorKind.Infantry, 2, View(1), Relation.Neutral));
        Assert.Equal(BuildingClick.None, GarrisonOrders.Classify(Rules, ActorKind.Infantry, 2, View(1), Relation.Self));
        Assert.Equal(BuildingClick.None, GarrisonOrders.Classify(Rules, ActorKind.Vehicle, 0, View(1), Relation.Self));
    }

    [Fact]
    public void An_own_hatch_is_a_tunnel_entry_and_a_plain_building_is_a_move()
    {
        Assert.Equal(BuildingClick.EnterTunnel, GarrisonOrders.Classify(Rules, ActorKind.Infantry, 0, View(2), Relation.Self));
        Assert.Equal(BuildingClick.None, GarrisonOrders.Classify(Rules, ActorKind.Infantry, 0, View(0), Relation.Self));
    }

    [Fact]
    public void Ghosts_are_never_targets() =>
        Assert.Equal(BuildingClick.None, GarrisonOrders.Classify(Rules, ActorKind.Infantry, 0, View(1, ghost: true), Relation.Neutral));

    [Fact]
    public void D_unloads_only_an_own_building_with_occupants()
    {
        Assert.True(GarrisonOrders.CanUnload(View(1, 2), 0));
        Assert.False(GarrisonOrders.CanUnload(View(1, 0), 0));
        Assert.False(GarrisonOrders.CanUnload(View(1, 2), 1));
        Assert.False(GarrisonOrders.CanUnload(null, 0));
        Command c = GarrisonOrders.Unload(0, new EntityId(4, 2));
        Assert.Equal((CommandKind.Unload, new EntityId(4, 2), (int)ActorKind.Building), (c.Kind, c.Subject, c.Arg));
    }

    [Theory]
    [InlineData(0, 5, 0, 5)]
    [InlineData(3, 5, 3, 5)]
    [InlineData(9, 5, 5, 5)]    // never more filled than room
    [InlineData(-1, 5, 0, 5)]
    [InlineData(2, 0, 2, 2)]    // capacity unknown: occupants only
    public void Pip_counts(int occupants, int capacity, int filled, int total) =>
        Assert.Equal(new PipCounts(filled, total), GarrisonOverlay.Pips(occupants, capacity));

    [Fact]
    public void Foam_alpha_rises_with_ticks_left_and_is_zero_without_foam()
    {
        Assert.Equal(0, FoamOverlay.AlphaFor(0));
        Assert.True(FoamOverlay.AlphaFor(1) > 0);
        Assert.True(FoamOverlay.AlphaFor(100) < FoamOverlay.AlphaFor(400));
        Assert.Equal(FoamOverlay.AlphaFor(400), FoamOverlay.AlphaFor(4000));
    }
}
