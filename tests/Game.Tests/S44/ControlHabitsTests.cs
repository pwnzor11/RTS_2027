// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Game.Controls;
using Rts.Game.Sandbox;
using Rts.Sim.Core;

namespace Rts.Game.Tests.S44;

/// <summary>U-05: structure control groups, Shift+number adds, and the queued attack cell (pure input model, no window).</summary>
public sealed class ControlHabitsTests
{
    private static UnitKey U(int i) => new(ActorKind.Vehicle, new EntityId((ushort)i, 1));
    private static EntityId B(int i) => new((ushort)i, 1);
    private static readonly UnitKey[] NoUnits = Array.Empty<UnitKey>();
    private static readonly EntityId[] NoBuildings = Array.Empty<EntityId>();

    [Fact]
    public void Ctrl_number_on_a_building_assigns_it_and_the_number_recalls_it()
    {
        var g = new ControlGroups();
        Assert.Equal(GroupPress.Set, g.Press(3, true, false, NoUnits, new[] { B(7) }));
        Assert.Equal(new[] { B(7) }, g.Structures(3));
        Assert.Equal(3, g.GroupOfStructure(B(7)));
        Assert.Equal(GroupPress.RecallStructures, g.Press(3, false, false, NoUnits, NoBuildings));
    }

    [Fact]
    public void An_empty_selection_never_changes_a_group()
    {
        var g = new ControlGroups();
        g.Press(2, true, false, NoUnits, new[] { B(1) });
        Assert.Equal(GroupPress.None, g.Press(2, true, false, NoUnits, NoBuildings));
        Assert.Equal(GroupPress.None, g.Press(2, false, true, NoUnits, NoBuildings));
        Assert.Equal(new[] { B(1) }, g.Structures(2));
        Assert.Equal(GroupPress.None, g.Press(5, false, false, NoUnits, NoBuildings));
    }

    [Fact]
    public void Setting_a_group_replaces_it_whole()
    {
        var g = new ControlGroups();
        g.Press(1, true, false, new[] { U(1), U(2) }, NoBuildings);
        g.Press(1, true, false, NoUnits, new[] { B(4) });   // a building now: the units leave
        Assert.Empty(g.Members(1));
        Assert.Equal(new[] { B(4) }, g.Structures(1));
        g.Press(1, true, false, new[] { U(3) }, NoBuildings);   // units again: the building leaves
        Assert.Empty(g.Structures(1));
        Assert.Equal(new[] { U(3) }, g.Members(1));
    }

    [Fact]
    public void A_building_belongs_to_one_group_only()
    {
        var g = new ControlGroups();
        g.Press(1, true, false, NoUnits, new[] { B(4) });
        g.Press(2, true, false, NoUnits, new[] { B(4), B(5) });
        Assert.Empty(g.Structures(1));
        Assert.Equal(2, g.GroupOfStructure(B(4)));
    }

    [Fact]
    public void Shift_number_adds_the_selection_and_keeps_the_group()
    {
        var g = new ControlGroups();
        g.Press(4, true, false, new[] { U(1) }, NoBuildings);
        Assert.Equal(GroupPress.Added, g.Press(4, false, true, new[] { U(2), U(3) }, NoBuildings));
        Assert.Equal(new[] { U(1), U(2), U(3) }, g.Members(4));
        Assert.Equal(GroupPress.Added, g.Press(4, false, true, NoUnits, new[] { B(9) }));
        Assert.Equal(new[] { B(9) }, g.Structures(4));
        Assert.Equal(3, g.Members(4).Count);
    }

    [Fact]
    public void Shift_number_moves_a_unit_out_of_its_old_group()
    {
        var g = new ControlGroups();
        g.Press(1, true, false, new[] { U(1) }, NoBuildings);
        g.Press(2, false, true, new[] { U(1) }, NoBuildings);
        Assert.Equal(2, g.GroupOf(U(1)));
        Assert.Empty(g.Members(1));
    }

    [Fact]
    public void A_plain_press_recalls_units_first_and_pruning_drops_dead_buildings()
    {
        var g = new ControlGroups();
        g.Press(6, true, false, new[] { U(1) }, NoBuildings);
        g.Press(6, false, true, NoUnits, new[] { B(2), B(3) });
        Assert.Equal(GroupPress.RecallUnits, g.Press(6, false, false, NoUnits, NoBuildings));
        g.PruneStructures(id => id == B(3));
        Assert.Equal(new[] { B(3) }, g.Structures(6));
    }

    [Fact]
    public void Structures_are_not_in_the_save_snapshot_and_clear_empties_them()
    {
        var g = new ControlGroups();
        g.Press(1, true, false, NoUnits, new[] { B(1) });
        Assert.All(g.Snapshot(), grp => Assert.Empty(grp));
        g.Clear();
        Assert.Empty(g.Structures(1));
    }

    [Fact]
    public void Shift_number_is_bound_as_add_in_the_help_text()
    {
        Assert.Contains(GameKeys.Rows, r => r.Action == GameAction.Group1 && r.Description.Contains("Shift + key adds"));
    }

    [Fact]
    public void A_queued_attack_targets_a_buildings_centre_or_a_units_cell()
    {
        var units = new List<UnitView> { new(new EntityId(5, 1), UnitKind.Vehicle, 1, 0, new LeptonCoord(10 * 256 + 5, 20 * 256 + 9), 0, 100) };
        var buildings = new List<BuildingView> { new(new EntityId(2, 1), 1, 0, new CellCoord(30, 40), 3, 3, 100, false) };
        Assert.Equal(new CellCoord(31, 41), QueuedAttack.CellFor(new ActorRef(ActorKind.Building, new EntityId(2, 1)), units, buildings));
        Assert.Equal(new CellCoord(10, 20), QueuedAttack.CellFor(new ActorRef(ActorKind.Vehicle, new EntityId(5, 1)), units, buildings));
        Assert.Null(QueuedAttack.CellFor(new ActorRef(ActorKind.Vehicle, new EntityId(6, 1)), units, buildings));
    }
}
