// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Data;
using Rts.Game.Controls;
using Rts.Game.Render;
using Rts.Sim.Core;
using Rts.Sim.Rules;

namespace Rts.Game.Tests.Controls;

/// <summary>M11-10e: the cursor under the mouse follows the same decision tree as OrderController.RightClick.</summary>
public sealed class CursorPreviewTests
{
    private static readonly SimRules Rules = Build();

    // Infantry: 0 rifleman, 1 engineer. Vehicles: 0 tank, 1 harvester, 2 salvager, 3 landing craft, 4 APC, 5 jet (air), 6 bore (deploys at a cell).
    // Buildings: 0 plain, 1 blockhouse (garrison 5).
    private static SimRules Build()
    {
        string dir = RulesLocator.FindRulesDir(AppContext.BaseDirectory) ?? throw new InvalidOperationException("data/rules not found");
        SimRules b = RulesLoader.Load(dir).Sim;
        InfantryType i = b.Infantry[0];
        VehicleType v = b.Vehicles[0];
        BuildingType bt = b.Buildings[0];
        return b with
        {
            Infantry = new[]
            {
                i with { Engineer = false, CanGarrison = true, SalvageRate = 0 },
                i with { Engineer = true, CanGarrison = false, SalvageRate = 0 },
            },
            Vehicles = new[]
            {
                v with { HarvestCapacity = 0, SalvageRate = 0, Passengers = 0, UnloadTicks = 0, Locomotion = Locomotion.Tracked, DeploysInto = -1, Submerges = false },
                v with { HarvestCapacity = 20, SalvageRate = 0, Passengers = 0, UnloadTicks = 0, Locomotion = Locomotion.Tracked, DeploysInto = -1, Submerges = false },
                v with { HarvestCapacity = 0, SalvageRate = 5, Passengers = 0, UnloadTicks = 0, Locomotion = Locomotion.Tracked, DeploysInto = -1, Submerges = false },
                v with { HarvestCapacity = 0, SalvageRate = 0, Passengers = 4, UnloadTicks = 20, Locomotion = Locomotion.Hover, DeploysInto = -1, Submerges = false },
                v with { HarvestCapacity = 0, SalvageRate = 0, Passengers = 4, UnloadTicks = 0, Locomotion = Locomotion.Tracked, DeploysInto = -1, Submerges = false },
                v with { HarvestCapacity = 0, SalvageRate = 0, Passengers = 0, UnloadTicks = 0, Locomotion = Locomotion.Air, DeploysInto = -1, Submerges = false },
                v with { HarvestCapacity = 0, SalvageRate = 0, Passengers = 0, UnloadTicks = 0, Locomotion = Locomotion.Burrow, DeploysInto = 0, Submerges = false },
            },
            Buildings = new[]
            {
                bt with { Garrison = 0, Entrance = TunnelSize.None, MaxHealth = 100 },
                bt with { Garrison = 5, Entrance = TunnelSize.None, MaxHealth = 100 },
            },
        };
    }

    private static CursorPreview.Subject Inf(int t) => new(ActorKind.Infantry, t, 0);
    private static CursorPreview.Subject Veh(int t, int passengers = 0) => new(ActorKind.Vehicle, t, passengers);

    private static BuildingView View(int type, int health = 100) =>
        new(new EntityId(1, 1), 0, type, new CellCoord(3, 3), 2, 2, health, false);

    private static CursorPreview.Target Ground() => new() { Cell = new CellCoord(5, 5), CellInMap = true, OwnVehicleType = -1 };

    private static CursorKind Decide(CursorPreview.Subject s, CursorPreview.Target t, bool am = false, bool dep = false) =>
        CursorPreview.Decide(Rules, new[] { s }, t, am, dep);

    [Fact]
    public void Ground_is_move_and_attack_move_when_armed()
    {
        Assert.Equal(CursorKind.Move, Decide(Veh(0), Ground()));
        Assert.Equal(CursorKind.AttackMove, Decide(Veh(0), Ground(), am: true));
    }

    [Fact]
    public void Nothing_selected_is_select_or_hover()
    {
        Assert.Equal(CursorKind.Select, CursorPreview.Decide(Rules, Array.Empty<CursorPreview.Subject>(), Ground(), false, false));
        Assert.Equal(CursorKind.Hover, CursorPreview.Decide(Rules, Array.Empty<CursorPreview.Subject>(), Ground() with { OverOwn = true }, false, false));
    }

    [Fact]
    public void Enemy_is_attack_even_when_armed() =>
        Assert.Equal(CursorKind.Attack, Decide(Veh(0), Ground() with { HasEnemy = true }, am: true));

    [Fact]
    public void Own_unit_under_a_move_is_hover() =>
        Assert.Equal(CursorKind.Hover, Decide(Veh(0), Ground() with { OverOwn = true }));

    [Theory]
    [InlineData(1, Relation.Enemy, 100, CursorKind.Capture)]
    [InlineData(1, Relation.Neutral, 100, CursorKind.Capture)]
    [InlineData(1, Relation.Self, 50, CursorKind.Repair)]
    [InlineData(0, Relation.Self, 100, CursorKind.Garrison)]   // rifleman on a blockhouse
    [InlineData(0, Relation.Enemy, 100, CursorKind.Attack)]
    public void Building_clicks_match_GarrisonOrders(int infantry, Relation relation, int health, CursorKind expected)
    {
        int type = expected == CursorKind.Garrison || (infantry == 0 && relation == Relation.Enemy) ? 1 : 0;
        var t = Ground() with { Building = View(type, health), BuildingRelation = relation, HasEnemy = relation == Relation.Enemy, OverOwn = relation == Relation.Self };
        Assert.Equal(expected, Decide(Inf(infantry), t));
    }

    [Fact]
    public void Infantry_boards_an_own_transport_but_a_tank_does_not()
    {
        var t = Ground() with { OwnVehicleType = 4, OverOwn = true };
        Assert.Equal(CursorKind.Enter, Decide(Inf(0), t));
        Assert.Equal(CursorKind.Hover, Decide(Veh(0), t));
    }

    [Fact]
    public void Salvagers_take_wrecks_and_others_just_move()
    {
        var t = Ground() with { WreckHere = true };
        Assert.Equal(CursorKind.Salvage, Decide(Veh(2), t));
        Assert.Equal(CursorKind.Move, Decide(Veh(0), t));
        Assert.Equal(CursorKind.AttackMove, Decide(Veh(2), t, am: true));   // armed attack-move skips the wreck, like the order
    }

    [Fact]
    public void Harvesters_work_ash_fields()
    {
        var t = Ground() with { FieldHere = true };
        Assert.Equal(CursorKind.Harvest, Decide(Veh(1), t));
        Assert.Equal(CursorKind.Move, Decide(Veh(0), t));
        Assert.True(CursorPreview.CanHarvestField(Rules, ActorKind.Vehicle, 1));
        Assert.False(CursorPreview.CanHarvestField(Rules, ActorKind.Infantry, 1));
    }

    [Fact]
    public void A_loaded_landing_craft_unloads_on_a_shore_cell_only()
    {
        var shore = Ground() with { FootStandable = true };
        Assert.Equal(CursorKind.Unload, Decide(Veh(3, 2), shore));
        Assert.Equal(CursorKind.Move, Decide(Veh(3, 0), shore));
        Assert.Equal(CursorKind.Move, Decide(Veh(4, 2), shore));
        Assert.Equal(CursorKind.Move, Decide(Veh(3, 2), Ground()));
    }

    [Fact]
    public void Deploy_armed_shows_the_deploy_cell_only_for_cell_deployers()
    {
        Assert.Equal(CursorKind.DeployAt, Decide(Veh(6), Ground(), dep: true));
        Assert.Equal(CursorKind.Select, Decide(Veh(0), Ground(), dep: true));
    }

    [Fact]
    public void Impassable_ground_is_blocked_unless_a_unit_can_cross_it()
    {
        Func<CellCoord, Locomotion, bool> onlyAir = (_, l) => l == Locomotion.Air;
        var t = Ground() with { Passable = onlyAir };
        Assert.Equal(CursorKind.Blocked, Decide(Veh(0), t));
        Assert.Equal(CursorKind.Move, Decide(Veh(5), t));
        Assert.Equal(CursorKind.Blocked, Decide(Inf(0), t));
        Assert.Equal(CursorKind.Select, Decide(Veh(0), Ground() with { CellInMap = false }));
    }

    [Fact]
    public void A_mixed_selection_shows_the_most_specific_action()
    {
        var enemyBuilding = Ground() with { Building = View(0), BuildingRelation = Relation.Enemy, HasEnemy = true };
        Assert.Equal(CursorKind.Capture, CursorPreview.Decide(Rules, new[] { Veh(0), Inf(1) }, enemyBuilding, false, false));
        Assert.Equal(CursorKind.Attack, CursorPreview.Decide(Rules, new[] { Veh(0), Inf(0) }, enemyBuilding, false, false));
        var blockedGround = Ground() with { Passable = (_, l) => l == Locomotion.Foot };
        Assert.Equal(CursorKind.Move, CursorPreview.Decide(Rules, new[] { Veh(0), Inf(0) }, blockedGround, false, false));
    }

    [Fact]
    public void Every_cursor_kind_has_art_with_its_hot_spot_inside()
    {
        foreach (CursorKind k in Enum.GetValues<CursorKind>())
        {
            CursorArt.Sprite s = CursorArt.Of(k);
            Assert.Equal(CursorArt.Size * CursorArt.Size, s.Pixels.Length);
            Assert.InRange(s.HotX, 0, CursorArt.Size - 1);
            Assert.InRange(s.HotY, 0, CursorArt.Size - 1);
            Assert.Contains(s.Pixels, p => p.A != 0);
        }
    }
}
