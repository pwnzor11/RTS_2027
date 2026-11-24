// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Data;
using Rts.Game.Controls;
using Rts.Sim.Core;
using Rts.Sim.Rules;

namespace Rts.Game.Tests.A43Qol;

/// <summary>A43-QOL-15: which buildings repair-all switches on, and that it never toggles one off.</summary>
public sealed class BuildingBulkTests
{
    private static readonly SimRules Rules = RulesLoader.Load(RulesLocator.FindRulesDir(AppContext.BaseDirectory)!).Sim;

    private static BuildingView Building(int slot, int health, byte player = 0, bool ghost = false, int type = 0) =>
        new(new EntityId((ushort)slot, 1), player, type, new CellCoord(slot, 1), 2, 2, health, ghost);

    private static int Max(int type = 0) => Rules.Buildings[type].MaxHealth;

    [Fact]
    public void Only_damaged_own_buildings_are_targets_in_id_order()
    {
        var bulk = new BuildingBulk();
        var list = new[]
        {
            Building(5, Max() - 10), Building(2, Max()), Building(3, Max() - 1, player: 1), Building(4, Max() - 1, ghost: true), Building(1, 1),
        };
        Assert.Equal(new[] { 1, 5 }, bulk.RepairTargets(list, Rules, 0).Select(b => (int)b.Id.Index));
    }

    [Fact]
    public void A_building_already_repairing_is_not_toggled_off()
    {
        var bulk = new BuildingBulk();
        BuildingView hurt = Building(7, Max() - 50);
        bulk.Started(bulk.RepairTargets(new[] { hurt }, Rules, 0));
        Assert.Empty(bulk.RepairTargets(new[] { hurt }, Rules, 0));
        bulk.Observe(new[] { Building(7, Max() - 40) }, Rules, 0, tick: 1);   // healing: stays on the list
        Assert.True(bulk.IsRepairing(hurt.Id));
        Assert.Empty(bulk.RepairTargets(new[] { Building(7, Max() - 40) }, Rules, 0));
    }

    [Fact]
    public void A_repair_that_stopped_is_forgotten_so_the_next_press_starts_it_again()
    {
        var bulk = new BuildingBulk();
        BuildingView hurt = Building(7, Max() - 50);
        bulk.Started(new[] { hurt });
        bulk.Observe(new[] { Building(7, Max() - 40) }, Rules, 0, tick: 1);
        bulk.Observe(new[] { Building(7, Max() - 40) }, Rules, 0, tick: 2);   // no gain: the owner could not pay
        Assert.False(bulk.IsRepairing(hurt.Id));
        Assert.Single(bulk.RepairTargets(new[] { Building(7, Max() - 40) }, Rules, 0));
    }

    [Fact]
    public void Observing_twice_in_one_tick_does_not_drop_a_repair()
    {
        var bulk = new BuildingBulk();
        bulk.Started(new[] { Building(7, Max() - 50) });
        bulk.Observe(new[] { Building(7, Max() - 50) }, Rules, 0, tick: 3);
        bulk.Observe(new[] { Building(7, Max() - 50) }, Rules, 0, tick: 3);
        Assert.True(bulk.IsRepairing(new EntityId(7, 1)));
    }

    [Fact]
    public void A_full_or_lost_building_leaves_the_list()
    {
        var bulk = new BuildingBulk();
        bulk.Started(new[] { Building(7, Max() - 5), Building(8, Max() - 5) });
        bulk.Observe(new[] { Building(7, Max()) }, Rules, 0, tick: 1);   // 7 is full, 8 is gone
        Assert.False(bulk.IsRepairing(new EntityId(7, 1)));
        Assert.False(bulk.IsRepairing(new EntityId(8, 1)));
    }

    [Fact]
    public void A_hand_repair_toggles_the_belief()
    {
        var bulk = new BuildingBulk();
        var id = new EntityId(9, 1);
        bulk.NoteRepair(id, 100);
        Assert.True(bulk.IsRepairing(id));
        bulk.NoteRepair(id, 120);   // a second click switched it off
        Assert.False(bulk.IsRepairing(id));
    }
}
