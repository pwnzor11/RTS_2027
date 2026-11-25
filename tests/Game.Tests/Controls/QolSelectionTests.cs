// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Microsoft.Xna.Framework;
using Rts.Game;
using Rts.Game.Controls;
using Rts.Game.Sandbox;
using Rts.Sim.Core;

namespace Rts.Game.Tests.Controls;

/// <summary>QoL selection helpers (QOL-02, QOL-03, QOL-05, QOL-11, QOL-17): the pure rules behind the new keys and gestures.</summary>
public sealed class QolSelectionTests
{
    private static UnitKey K(int n) => new(ActorKind.Infantry, new EntityId((ushort)n, 1));

    private static RenderUnit U(int n, byte player, int type, float x, float y, UnitKind kind = UnitKind.Infantry) =>
        new(new UnitView(new EntityId((ushort)n, 1), kind, player, type, default, 0, 10), new Vector2(x, y));

    [Fact]
    public void Same_type_anywhere_takes_every_own_unit_of_that_kind_and_type_on_the_map()
    {
        var units = new List<RenderUnit>
        {
            U(1, 0, 3, 10, 10), U(2, 0, 3, 900, 900),          // both own, type 3, one far off screen
            U(3, 0, 4, 20, 20),                                 // other type
            U(4, 1, 3, 30, 30),                                 // enemy
            U(5, 0, 3, 40, 40, UnitKind.Vehicle),               // same index, other kind
        };
        Assert.Equal(new[] { K(1), K(2) }, Selection.SameTypeAnywhere(units, 0, K(1)).ToArray());
        Assert.Empty(Selection.SameTypeAnywhere(units, 0, K(99)));
    }

    [Fact]
    public void Same_type_among_narrows_the_selection_and_keeps_its_order()
    {
        var units = new List<RenderUnit> { U(1, 0, 3, 0, 0), U(2, 0, 4, 0, 0), U(3, 0, 3, 0, 0), U(4, 0, 3, 900, 900) };
        var selected = new List<UnitKey> { K(3), K(2), K(1) };
        Assert.Equal(new[] { K(3), K(1) }, Selection.SameTypeAmong(selected, units, K(1)).ToArray());
    }

    [Fact]
    public void Auto_groups_recall_every_own_unit_of_the_members_types()
    {
        var units = new List<RenderUnit>
        {
            U(1, 0, 3, 0, 0), U(2, 0, 3, 900, 900),   // member 1 and its type far away
            U(3, 0, 4, 0, 0),                         // other type, not in the group
            U(4, 1, 3, 0, 0),                         // enemy of the same type
        };
        Assert.Equal(new[] { K(1), K(2) }, Selection.OfTypesOf(new[] { K(1) }, units, 0).ToArray());
        Assert.Empty(Selection.OfTypesOf(new[] { K(9) }, units, 0));   // a dead member adds no type
    }

    [Fact]
    public void Auto_by_type_is_off_by_default_so_a_recall_is_unchanged()
    {
        var g = new ControlGroups();
        Assert.False(g.AutoByType);
        g.Set(1, new[] { K(1) });
        Assert.Equal(new[] { K(1) }, g.Members(1));   // membership is never expanded in the group itself
    }

    [Fact]
    public void Alt_box_takes_fighters_only_when_a_fighter_is_inside_and_everything_otherwise()
    {
        Func<UnitView, bool> fighter = v => v.TypeIndex == 0;
        var units = new List<RenderUnit>
        {
            U(1, 0, 0, 10, 10),   // fighter
            U(2, 0, 1, 20, 20),   // harvester-like, not a fighter
            U(3, 0, 1, 900, 900), // outside the box
        };
        Vector2 min = Vector2.Zero, max = new(100, 100);
        Assert.Equal(new[] { K(1) }, Selection.InsideFightersFirst(units, min, max, 0, fighter).ToArray());

        var noFighters = new List<RenderUnit> { U(2, 0, 1, 20, 20), U(4, 0, 1, 30, 30) };
        Assert.Equal(new[] { K(2), K(4) }, Selection.InsideFightersFirst(noFighters, min, max, 0, fighter).ToArray());
    }

    [Fact]
    public void Alt_box_ignores_enemy_units()
    {
        var units = new List<RenderUnit> { U(1, 1, 0, 10, 10), U(2, 0, 1, 20, 20) };
        Assert.Equal(new[] { K(2) }, Selection.InsideFightersFirst(units, Vector2.Zero, new Vector2(100, 100), 0, v => v.TypeIndex == 0).ToArray());
    }
}
