// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Data;
using Rts.Game.Archive;
using Rts.Game.Render;
using Rts.Game.Ui;
using Rts.Game.Ui.Sidebar;
using Rts.Sim.Rules;

namespace Rts.Game.Tests.S44;

/// <summary>M-04 UI: every weapon slot of a unit is listed in the Archive, the matchup tooltip and the range rings; one-weapon units are unchanged.</summary>
public sealed class WeaponSlotsUiTests
{
    private static readonly SimRules Rules = RulesLoader.Load(RulesLocator.FindRulesDir(AppContext.BaseDirectory)!).Sim;

    private static int Vehicle(string id) => SimRules.IndexOf(Rules.Vehicles, id, v => v.Id);

    [Fact]
    public void The_destroyer_lists_three_slots_in_order()
    {
        IReadOnlyList<int> slots = WeaponSlots.OfVehicle(Rules, Vehicle("exp_destroyer"));
        Assert.Equal(3, slots.Count);
        Assert.Equal(Rules.Vehicles[Vehicle("exp_destroyer")].PrimaryWeapon, slots[0]);
        Assert.Equal(Rules.Vehicles[Vehicle("exp_destroyer")].SecondaryWeapon, slots[1]);
        Assert.Equal(Rules.Vehicles[Vehicle("exp_destroyer")].TertiaryWeapon, slots[2]);
    }

    [Fact]
    public void The_archive_shows_one_weapon_line_per_slot_with_what_it_hits()
    {
        var lines = ArchiveStats.For(Rules, "exp_destroyer");
        Assert.Equal(1, lines.Count(l => l.Label == "Weapon"));
        Assert.Contains(lines, l => l.Label == "Weapon 2" && l.Value.Contains("hits submarines"));
        Assert.Contains(lines, l => l.Label == "Weapon 3" && l.Value.Contains("hits air"));
        Assert.Equal("Requires", lines[^1].Label);

        var launch = ArchiveStats.For(Rules, "exp_launch");
        Assert.Contains(launch, l => l.Label == "Weapon 2");
        Assert.DoesNotContain(launch, l => l.Label == "Weapon 3");
    }

    [Fact]
    public void A_one_weapon_unit_keeps_its_archive_rows_and_ring()
    {
        VehicleType single = Rules.Vehicles.First(v => v.PrimaryWeapon >= 0 && v.SecondaryWeapon < 0 && v.TertiaryWeapon < 0);
        var lines = ArchiveStats.For(Rules, single.Id);
        Assert.DoesNotContain(lines, l => l.Label.StartsWith("Weapon ", StringComparison.Ordinal));
        Assert.DoesNotContain("hits", lines.First(l => l.Label == "Weapon").Value);
        Assert.Single(RangeOverlay.VehicleRings(Rules, Vehicle(single.Id)));
    }

    [Fact]
    public void A_weaponless_vehicle_has_no_slots_and_no_rings()
    {
        VehicleType none = Rules.Vehicles.First(v => v.PrimaryWeapon < 0);
        Assert.Empty(WeaponSlots.Of(Rules, none));
        Assert.Empty(RangeOverlay.VehicleRings(Rules, Vehicle(none.Id)));
        Assert.Empty(WeaponSlots.OfVehicle(Rules, -1));
    }

    [Fact]
    public void The_range_overlay_draws_one_ring_per_slot_in_distinct_colours()
    {
        Assert.Equal(3, RangeOverlay.VehicleRings(Rules, Vehicle("exp_destroyer")).Count);
        Assert.Equal(2, RangeOverlay.VehicleRings(Rules, Vehicle("exp_launch")).Count);
        Assert.NotEqual(RangeOverlay.SlotColour(0), RangeOverlay.SlotColour(1));
        Assert.NotEqual(RangeOverlay.SlotColour(1), RangeOverlay.SlotColour(2));
    }

    [Fact]
    public void The_matchup_tooltip_names_each_slot_for_a_multi_weapon_unit_only()
    {
        int destroyer = Vehicle("exp_destroyer");
        List<string> lines = WeaponMatchup.Lines(Rules, ProductionCategory.Vehicle, destroyer);
        foreach (int slot in WeaponSlots.OfVehicle(Rules, destroyer))
        {
            (var strong, var weak) = WeaponMatchup.Classes(Rules, slot);
            if (strong.Count + weak.Count > 0) Assert.Contains(lines, l => l.StartsWith(CameoNames.Display(Rules.Weapons[slot].Id), StringComparison.Ordinal));
        }

        VehicleType single = Rules.Vehicles.First(v => v.PrimaryWeapon >= 0 && v.SecondaryWeapon < 0
            && WeaponMatchup.Classes(Rules, v.PrimaryWeapon).Strong.Count > 0);
        List<string> plain = WeaponMatchup.Lines(Rules, ProductionCategory.Vehicle, Vehicle(single.Id));
        Assert.StartsWith("Strong vs", plain[0]);
    }

    [Fact]
    public void Targets_text_covers_every_target_kind()
    {
        foreach (WeaponTargets t in Enum.GetValues<WeaponTargets>()) Assert.False(string.IsNullOrWhiteSpace(WeaponSlots.TargetsText(t)));
        Assert.Equal("air", WeaponSlots.TargetsText(WeaponTargets.Air));
    }
}
