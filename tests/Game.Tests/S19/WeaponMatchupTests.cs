// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Data;
using Rts.Game.Ui.Sidebar;
using Rts.Sim.Rules;

namespace Rts.Game.Tests.S19;

/// <summary>M13-15 item 6 (D-205): "Strong vs / Weak vs" tooltip lines generated from the warhead x armour table.</summary>
public sealed class WeaponMatchupTests
{
    private static readonly SimRules Rules = RulesLoader.Load(RulesLocator.FindRulesDir(AppContext.BaseDirectory)!).Sim;
    private static readonly ArmorClass[] Land = { ArmorClass.Unarmored, ArmorClass.Light, ArmorClass.Heavy, ArmorClass.Structure, ArmorClass.Fortified };

    private static int Pct(int weapon, ArmorClass c) => Rules.Warheads[Rules.Weapons[weapon].Warhead].VersusPercent[(int)c];

    [Fact]
    public void Strong_is_the_two_best_land_classes_and_weak_the_two_worst_for_every_armed_type()
    {
        int checkedTypes = 0;
        for (int i = 0; i < Rules.Infantry.Count; i++)
        {
            int w = Rules.Infantry[i].PrimaryWeapon;
            if (w < 0) continue;
            (IReadOnlyList<ArmorClass> strong, IReadOnlyList<ArmorClass> weak) = WeaponMatchup.Classes(Rules, w);
            int max = Land.Max(c => Pct(w, c)), min = Land.Min(c => Pct(w, c));
            if (max == min) { Assert.Empty(strong); Assert.Empty(weak); continue; }
            Assert.Equal(max, Pct(w, strong[0]));
            Assert.Equal(min, Pct(w, weak[0]));
            Assert.True(strong.Count <= 2 && weak.Count <= 2);
            Assert.Empty(strong.Intersect(weak));
            Assert.True(strong.All(s => Land.Where(c => !strong.Contains(c)).All(c => Pct(w, c) <= Pct(w, s))), "nothing outside the strong list beats a strong class");
            checkedTypes++;
        }

        Assert.True(checkedTypes > 0);
    }

    [Fact]
    public void A_known_table_gives_known_lines()
    {
        // data/rules/warheads.toml: bullets = [100, 50, 15, 10, 5, ...]: strong vs unarmoured then light, weak vs fortified then structures.
        int rifle = Rules.Infantry.Select((t, i) => (t, i)).First(x => x.t.PrimaryWeapon >= 0 && Pct(x.t.PrimaryWeapon, ArmorClass.Unarmored) == 100 && Pct(x.t.PrimaryWeapon, ArmorClass.Fortified) == 5).i;
        List<string> lines = WeaponMatchup.Lines(Rules, ProductionCategory.Infantry, rifle);
        Assert.Equal(2, lines.Count);
        Assert.Equal("Strong vs: unarmoured 100%, light armour 50%", lines[0]);
        Assert.Equal("Weak vs: fortified 5%, structures 10%", lines[1]);
    }

    [Fact]
    public void A_type_without_a_weapon_has_no_lines_and_the_cameo_tooltip_carries_them()
    {
        int power = SimRules.IndexOf(Rules.Buildings, "auth_power", b => b.Id);
        Assert.Empty(WeaponMatchup.Lines(Rules, ProductionCategory.Structure, power));
        Assert.Empty(WeaponMatchup.Lines(Rules, ProductionCategory.Infantry, -1));
        int armed = Enumerable.Range(0, Rules.Infantry.Count).First(i => WeaponMatchup.Lines(Rules, ProductionCategory.Infantry, i).Count > 0);
        List<string> tip = CameoTooltip.Head(Rules, ProductionCategory.Infantry, armed, 100, null);
        Assert.Contains(tip, l => l.StartsWith("Strong vs: ", StringComparison.Ordinal));
        Assert.Contains(tip, l => l.StartsWith("Weak vs: ", StringComparison.Ordinal));
        Assert.Equal(2, CameoTooltip.Head(Rules, ProductionCategory.Structure, power, 10, null).Count);   // unchanged for a building without a weapon
    }
}
