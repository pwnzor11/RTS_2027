// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Microsoft.Xna.Framework;
using Rts.Game;
using Rts.Game.Controls;
using Rts.Game.Sandbox;
using Rts.Sim.Core;

namespace Rts.Game.Tests.Controls;

public sealed class SelectionToolkitTests
{
    private static UnitKey K(int n, ActorKind kind = ActorKind.Infantry) => new(kind, new EntityId((ushort)n, 1));

    private static RenderUnit U(int n, byte player, int type, float x, float y, UnitKind kind = UnitKind.Infantry) =>
        new(new UnitView(new EntityId((ushort)n, 1), kind, player, type, default, 0, 10), new Vector2(x, y));

    [Fact]
    public void Shift_box_adds_and_a_plain_box_replaces()
    {
        var sel = new Selection();
        sel.Replace(new[] { K(1), K(2) });
        sel.Apply(new[] { K(2), K(3) }, additive: true);
        Assert.Equal(new[] { K(1), K(2), K(3) }, sel.Units);
        sel.Apply(new[] { K(4) }, additive: false);
        Assert.Equal(new[] { K(4) }, sel.Units);
    }

    [Fact]
    public void Shift_click_toggles_one_unit()
    {
        var sel = new Selection();
        Assert.True(sel.Toggle(K(1)));
        Assert.True(sel.Toggle(K(2)));
        Assert.False(sel.Toggle(K(1)));
        Assert.Equal(new[] { K(2) }, sel.Units);
    }

    [Fact]
    public void Same_type_picks_own_units_of_that_kind_and_type_inside_the_box_only()
    {
        var units = new List<RenderUnit>
        {
            U(1, 0, 3, 10, 10), U(2, 0, 3, 50, 50), U(3, 0, 3, 500, 500),   // 3 is outside the box
            U(4, 0, 4, 20, 20),                                              // other type
            U(5, 1, 3, 30, 30),                                              // enemy
            U(6, 0, 3, 40, 40, UnitKind.Vehicle),                            // same index, other kind
        };
        UnitKey[] got = Selection.SameTypeInside(units, new Vector2(0, 0), new Vector2(100, 100), 0, K(1)).ToArray();
        Assert.Equal(new[] { K(1), K(2) }, got);
        Assert.Empty(Selection.SameTypeInside(units, Vector2.Zero, new Vector2(100, 100), 0, K(99)));
    }

    [Fact]
    public void Army_takes_only_own_units_the_predicate_calls_combat()
    {
        var units = new List<RenderUnit> { U(1, 0, 0, 0, 0), U(2, 0, 1, 0, 0), U(3, 1, 0, 0, 0), U(4, 0, 0, 900, 900) };
        UnitKey[] got = Selection.Army(units, 0, v => v.TypeIndex == 0).ToArray();
        Assert.Equal(new[] { K(1), K(4) }, got);
    }

    [Fact]
    public void Groups_set_recall_add_and_one_group_per_unit()
    {
        var g = new ControlGroups();
        g.Set(1, new[] { K(1), K(2) });
        Assert.Equal(new[] { K(1), K(2) }, g.Members(1));
        g.Add(1, new[] { K(2), K(3) });
        Assert.Equal(new[] { K(1), K(2), K(3) }, g.Members(1));
        g.Set(2, new[] { K(3) });   // taken out of group 1
        Assert.Equal(new[] { K(1), K(2) }, g.Members(1));
        Assert.Equal(2, g.GroupOf(K(3)));
        Assert.Equal(0, g.GroupOf(K(9)));
        g.Set(2, Array.Empty<UnitKey>());
        Assert.Empty(g.Members(2));
    }

    [Fact]
    public void Groups_ignore_bad_numbers_and_drop_dead_units()
    {
        var g = new ControlGroups();
        g.Set(0, new[] { K(1) });
        g.Set(10, new[] { K(1) });
        g.Add(0, new[] { K(1) });
        Assert.Empty(g.Members(0));
        Assert.Empty(g.Members(10));
        g.Set(9, new[] { K(1), K(2), K(3) });
        g.Prune(k => k != K(2));
        Assert.Equal(new[] { K(1), K(3) }, g.Members(9));
        g.Clear();
        Assert.Empty(g.Members(9));
    }

    [Fact]
    public void Group_selection_is_independent_of_the_list_it_was_made_from()
    {
        var sel = new Selection();
        sel.Replace(new[] { K(1), K(2) });
        var g = new ControlGroups();
        g.Set(1, sel.Units);
        sel.Clear();
        Assert.Equal(2, g.Members(1).Count);
    }

    [Fact]
    public void Double_tap_needs_the_same_target_inside_the_window()
    {
        var t = new TapTracker<int>();
        Assert.False(t.Tap(3, 1000));
        Assert.True(t.Tap(3, 1000 + TapTracker<int>.WindowMs));
        Assert.False(t.Tap(3, 1000 + TapTracker<int>.WindowMs + 10));   // a third tap starts afresh
        Assert.False(t.Tap(4, 1000 + TapTracker<int>.WindowMs + 20));   // another target
        Assert.False(t.Tap(4, 1000 + TapTracker<int>.WindowMs + 20 + TapTracker<int>.WindowMs + 1));   // too slow
    }

    [Fact]
    public void Reset_breaks_a_pair_and_a_clock_going_backwards_is_no_double()
    {
        var t = new TapTracker<UnitKey>();
        Assert.False(t.Tap(K(1), 500));
        t.Reset();
        Assert.False(t.Tap(K(1), 600));
        Assert.False(t.Tap(K(1), 100));
    }

    [Fact]
    public void New_actions_are_appended_after_ObjList_in_the_agreed_order()
    {
        GameAction[] all = Enum.GetValues<GameAction>();
        int at = Array.IndexOf(all, GameAction.ObjList);
        Assert.Equal(new[] { GameAction.SelectAll, GameAction.Group1, GameAction.Group2, GameAction.Group3, GameAction.Group4, GameAction.Group5,
            GameAction.Group6, GameAction.Group7, GameAction.Group8, GameAction.Group9, GameAction.LastAlert, GameAction.DecorToggle, GameAction.QuickSave, GameAction.QuickLoad }, all.Skip(at + 1).Take(14).ToArray());   // DecorToggle: M12-03; QuickSave, QuickLoad: D-205; later packages append after these
        Assert.Equal(Microsoft.Xna.Framework.Input.Keys.D5, GameKeys.InfoOf(GameAction.Group5).Default);
        Assert.All(all.Skip(at + 1).Take(10), a => Assert.Equal(GameKeys.Selection, GameKeys.InfoOf(a).Group));
    }
}
