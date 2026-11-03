// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using System.Globalization;

namespace Rts.Game.Ui.Sidebar;

/// <summary>Display names for rules ids when the rules carry none: "auth_power" shows as "Power Plant".</summary>
public static class CameoNames
{
    private static readonly Dictionary<string, string> Known = new(StringComparer.Ordinal)
    {
        ["conyard"] = "Construction Yard",
        ["power"] = "Power Plant",
        ["refinery"] = "Refinery",
        ["barracks"] = "Barracks",
        ["factory"] = "War Factory",
        ["silo"] = "Silo",
        ["wall"] = "Wall",
        ["turret"] = "Gun Turret",
        ["scraper"] = "Scraper",
        ["rifleman"] = "Rifleman",
        ["tank"] = "Medium Tank",
        ["jeep"] = "Jeep",
    };

    /// <summary>The name to show for a rules id (faction prefix before the first underscore dropped).</summary>
    public static string Display(string id)
    {
        if (string.IsNullOrEmpty(id)) return "?";
        int cut = id.IndexOf('_');
        string key = cut >= 0 && cut + 1 < id.Length ? id[(cut + 1)..] : id;
        if (Known.TryGetValue(key, out string? name)) return name;
        return CultureInfo.InvariantCulture.TextInfo.ToTitleCase(key.Replace('_', ' '));
    }
}
