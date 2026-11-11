// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using System.Globalization;

namespace Rts.Game.Ui.Sidebar;

/// <summary>Display names for rules ids. The Archive title is the one name of a thing (S19 naming pass, D-205); when no Archive is
/// loaded the id is tidied instead: "auth_power" shows as "Power Plant".</summary>
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
        ["tank"] = "Heavy Tank",
        ["jeep"] = "Scout Car",
    };

    /// <summary>Looks up the Archive title of a rules id (null when unknown); set by the game screen once the Archive is loaded.</summary>
    public static Func<string, string?>? ArchiveTitle { get; set; }

    /// <summary>The name to show for a rules id: the Archive title when known, else the id tidied (faction prefix dropped).</summary>
    public static string Display(string id) => Display(id, ArchiveTitle);

    /// <summary>As <see cref="Display(string)"/> with an explicit Archive lookup (null: tidy the id).</summary>
    public static string Display(string id, Func<string, string?>? archiveTitle)
    {
        if (string.IsNullOrEmpty(id)) return "?";
        string? title = archiveTitle?.Invoke(id);
        if (!string.IsNullOrWhiteSpace(title)) return title;
        int cut = id.IndexOf('_');
        string key = cut >= 0 && cut + 1 < id.Length ? id[(cut + 1)..] : id;
        if (Known.TryGetValue(key, out string? name)) return name;
        return CultureInfo.InvariantCulture.TextInfo.ToTitleCase(key.Replace('_', ' '));
    }
}
