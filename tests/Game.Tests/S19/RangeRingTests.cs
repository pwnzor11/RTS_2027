// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Data;
using Rts.Game.Controls;
using Rts.Game.Render;
using Rts.Sim.Rules;

namespace Rts.Game.Tests.S19;

/// <summary>M13-15 item 5 (D-205): weapon range rings in play for the selected units and defences, off by default, on a rebindable key.</summary>
public sealed class RangeRingTests
{
    private static readonly SimRules Rules = RulesLoader.Load(RulesLocator.FindRulesDir(AppContext.BaseDirectory)!).Sim;

    [Fact]
    public void The_rings_are_off_by_default_and_have_their_own_key_that_clashes_with_nothing()
    {
        Assert.False(new OverlayToggles().Ranges);
        Assert.Empty(KeyBindings.Default.Conflicts());
        Assert.NotEqual(KeyBindings.Default.Get(GameAction.DebugRanges), KeyBindings.Default.Get(GameAction.RangeRings));
        Assert.Equal(Microsoft.Xna.Framework.Input.Keys.L, KeyBindings.Default.Get(GameAction.RangeRings));
        Assert.Equal(KeyContext.Play, GameKeys.InfoOf(GameAction.RangeRings).Context);
    }

    [Fact]
    public void A_defence_with_a_weapon_has_a_range_and_a_building_without_one_has_none()
    {
        int turret = SimRules.IndexOf(Rules.Buildings, "auth_turret", b => b.Id);
        int power = SimRules.IndexOf(Rules.Buildings, "auth_power", b => b.Id);
        (int range, _) = RangeOverlay.BuildingRange(Rules, turret)!.Value;
        Assert.True(range > 0);
        Assert.Equal(Rules.Weapons[Rules.Buildings[turret].PrimaryWeapon].Range, range);
        Assert.Null(RangeOverlay.BuildingRange(Rules, power));
        Assert.Null(RangeOverlay.BuildingRange(Rules, -1));
        Assert.Null(RangeOverlay.BuildingRange(Rules, 9999));
    }
}
