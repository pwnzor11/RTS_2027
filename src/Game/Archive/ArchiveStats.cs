// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using System.Globalization;
using Rts.Sim.Rules;

namespace Rts.Game.Archive;

/// <summary>
/// Builds the stats block of a unit, structure or upgrade from the loaded rules, so the Archive never shows stale
/// numbers. Display only (never read by the simulation), so plain formatting is fine here.
/// </summary>
public static class ArchiveStats
{
    private const int LeptonsPerCell = 256;   // matches SimConstants.LeptonsPerCell
    private const int TicksPerSecond = 20;    // matches SimConstants.TicksPerSecond

    /// <summary>The stat rows for a rules id; empty when the id is not a vehicle, infantry, building or upgrade.</summary>
    public static IReadOnlyList<ArchiveStatLine> For(SimRules rules, string id)
    {
        foreach (VehicleType v in rules.Vehicles)
        {
            if (v.Id != id) continue;
            var list = Mobile(rules, v.Cost, v.MaxHealth, v.Armor, v.Speed, v.Sight, v.PrimaryWeapon, v.Prerequisites, v.BuildTime);
            if (v.Passengers > 0) list.Add(new("Carries", $"{v.Passengers} infantry"));
            if (v.TunnelSize != TunnelSize.None) list.Add(new("Tunnel size", v.TunnelSize.ToString()));
            return list;
        }
        foreach (InfantryType i in rules.Infantry)
        {
            if (i.Id != id) continue;
            var list = Mobile(rules, i.Cost, i.MaxHealth, i.Armor, i.Speed, i.Sight, i.PrimaryWeapon, i.Prerequisites, i.BuildTime);
            if (i.TunnelSize != TunnelSize.None) list.Add(new("Tunnel size", i.TunnelSize.ToString()));
            return list;
        }
        foreach (BuildingType b in rules.Buildings)
        {
            if (b.Id != id) continue;
            return Structure(rules, b);
        }
        foreach (UpgradeType u in rules.Upgrades)
        {
            if (u.Id != id) continue;
            return new List<ArchiveStatLine>
            {
                new("Cost", Credits(u.Cost)),
                new("Research time", Seconds(SimRules.BuildTicks(u.Cost, u.BuildTime, rules.Economy))),
                new("Requires", Requires(u.Prerequisites)),
                new("Effect", Rts.Game.Ui.Sidebar.TodayText.Upgrade(u.Id) is { } today ? string.Join(" ", today) : u.Effect),
            };
        }
        return Array.Empty<ArchiveStatLine>();
    }

    /// <summary>Whether the rules define this id as a vehicle, infantry, building or upgrade.</summary>
    public static bool Exists(SimRules rules, string id) =>
        rules.Vehicles.Any(v => v.Id == id) || rules.Infantry.Any(i => i.Id == id)
        || rules.Buildings.Any(b => b.Id == id) || rules.Upgrades.Any(u => u.Id == id);

    private static List<ArchiveStatLine> Mobile(SimRules rules, int cost, int health, ArmorClass armor, int speed, int sight,
        int weapon, IReadOnlyList<string> prerequisites, int buildTime)
    {
        var list = new List<ArchiveStatLine>
        {
            new("Cost", Credits(cost)),
            new("Build time", Seconds(SimRules.BuildTicks(cost, buildTime, rules.Economy))),
            new("Health", health.ToString(CultureInfo.InvariantCulture)),
            new("Armour", armor.ToString()),
            new("Speed", Tenths(speed * TicksPerSecond * 10 / LeptonsPerCell) + " cells/s"),
            new("Sight", sight.ToString(CultureInfo.InvariantCulture) + " cells"),
            new("Weapon", WeaponText(rules, weapon)),
            new("Requires", Requires(prerequisites)),
        };
        return list;
    }

    private static List<ArchiveStatLine> Structure(SimRules rules, BuildingType b)
    {
        var list = new List<ArchiveStatLine>
        {
            new("Cost", b.Cost == 0 ? "free" : Credits(b.Cost)),
            new("Health", b.MaxHealth.ToString(CultureInfo.InvariantCulture)),
            new("Armour", b.Armor.ToString()),
            new("Footprint", $"{b.FootprintW}x{b.FootprintH} cells"),
            new("Power", b.Power > 0 ? $"+{b.Power}" : b.Power.ToString(CultureInfo.InvariantCulture)),
            new("Sight", b.Sight.ToString(CultureInfo.InvariantCulture) + " cells"),
            new("Weapon", WeaponText(rules, b.PrimaryWeapon)),
            new("Requires", Requires(b.Prerequisites)),
        };
        if (b.Produces is { } produces) list.Add(new("Produces", produces.ToString().ToLowerInvariant()));
        if (b.Superweapon.Length > 0) list.Add(new("Superweapon charge", Seconds(b.SuperweaponChargeTicks)));
        return list;
    }

    private static string WeaponText(SimRules rules, int index)
    {
        if (index < 0 || index >= rules.Weapons.Count) return "none";
        WeaponType w = rules.Weapons[index];
        string range = w.MinRange > 0
            ? $"{Tenths(w.MinRange * 10 / LeptonsPerCell)}-{Tenths(w.Range * 10 / LeptonsPerCell)}"
            : Tenths(w.Range * 10 / LeptonsPerCell);
        return $"{w.Id}: {w.Damage} damage, range {range} cells, reload {Seconds(w.RateOfFire)}";
    }

    private static string Requires(IReadOnlyList<string> prerequisites) =>
        prerequisites.Count == 0 ? "nothing" : string.Join(", ", prerequisites);

    private static string Credits(int cost) => cost.ToString(CultureInfo.InvariantCulture);

    private static string Seconds(int ticks) => Tenths(ticks * 10 / TicksPerSecond) + " s";

    private static string Tenths(int tenths) => $"{tenths / 10}.{tenths % 10}";
}
