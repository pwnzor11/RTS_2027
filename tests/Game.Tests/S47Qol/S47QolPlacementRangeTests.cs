// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Data;
using Rts.Game.Render;
using Rts.Sim.Rules;

namespace Rts.Game.Tests.S47Qol;

/// <summary>S47 U-13 (cnc#28): the ring drawn while placing a structure exists exactly for the types that have a weapon.</summary>
public sealed class S47QolPlacementRangeTests
{
    private static readonly SimRules Rules = RulesLoader.Load(RulesLocator.FindRulesDir(AppContext.BaseDirectory)!).Sim;

    [Fact]
    public void Every_building_type_with_a_ranged_weapon_has_a_placement_ring_and_no_other_has()
    {
        int ranged = 0;
        for (int i = 0; i < Rules.Buildings.Count; i++)
        {
            int w = Rules.Buildings[i].PrimaryWeapon;
            bool hasRange = w >= 0 && w < Rules.Weapons.Count && Rules.Weapons[w].Range > 0;
            Assert.Equal(hasRange, RangeOverlay.BuildingRange(Rules, i) is not null);
            if (hasRange) ranged++;
        }

        Assert.True(ranged > 0);
    }
}
