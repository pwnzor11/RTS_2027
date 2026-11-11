// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
namespace Rts.Game.Ui.Sidebar;

/// <summary>
/// What a unit, building or upgrade really does today, for the tooltip, where the Archive's lore line promises more than the game
/// has built (S19, D-205 "reword now, build later"). The Archive text is lore and stays as it is; delete an entry here when the
/// mechanic ships. Display only.
/// </summary>
public static class TodayText
{
    private static readonly Dictionary<string, string> Cameos = new(StringComparer.Ordinal)
    {
        ["exp_minelayer"] = "Unarmed sea hull. It cannot lay mines yet.",
        ["exp_diver"] = "Plants charges on ships and quays from the shore. It cannot swim yet.",
        ["exp_drydock"] = "Unlocks the Cruiser, Carrier and Command Ship. It does not build or repair ships itself yet.",
        ["exp_destroyer"] = "Gun warship that detects submarines. It has no depth charge yet.",
        ["lb_harpoon_wagon"] = "Anti-armour harpoon gun. It does not tether or slow targets yet.",
    };

    private static readonly Dictionary<string, string[]> Upgrades = new(StringComparer.Ordinal)
    {
        ["lb_up_scrap_plate"] = new[] { "+20% health for ground vehicles and packed rigs.", "No armour change. Rotors and Kites are not covered." },
        ["lb_up_patchwork"] = new[] { "Tinkers repair vehicles from 5 cells away (3 without it)." },
        ["lb_up_quick_pack"] = new[] { "Rigs pack 40% faster." },
        ["lb_up_grapple"] = new[] { "Hook Runners plunder x1.5, up to the 1000 credit limit.", "They cannot breach walls yet." },
        ["lb_up_salvage_rights"] = new[] { "Salvage crews drain a wreck 1.5x faster.", "The wreck's value is unchanged." },
        ["exp_up_sonar_nets"] = new[] { "Pickets see 2 cells further." },
    };

    /// <summary>The one-line tooltip text for a unit or building that has an entry here, else null (use the Archive subtitle).</summary>
    public static string? Cameo(string id) => Cameos.GetValueOrDefault(id);

    /// <summary>The hand-written effect lines of an upgrade, else null (use the generated lines).</summary>
    public static IReadOnlyList<string>? Upgrade(string id) => Upgrades.GetValueOrDefault(id);
}
