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
        ["exp_diver"] = "Plants charges on ships and quays. Swims in deep water, hidden from all but sonar.",
        ["exp_drydock"] = "Unlocks the Cruiser and Carrier. It does not build or repair ships itself yet.",
        ["exp_destroyer"] = "Gun warship that detects submarines, with depth charges and an anti-air mount.",
        ["lb_harpoon_wagon"] = "Anti-armour harpoon gun. It does not tether or slow targets yet.",
    };

    private static readonly Dictionary<string, string[]> Upgrades = new(StringComparer.Ordinal)
    {
        ["lb_up_scrap_plate"] = new[] { "Light vehicles, Rotors and Kites become Heavy armour.", "Gleaners, Rolling Forts and packed rigs get +20% health." },
        ["lb_up_patchwork"] = new[] { "Tinkers repair vehicles from 9 cells away (3 without it), four times as fast." },
        ["lb_up_quick_pack"] = new[] { "Rigs pack 40% faster." },
        ["lb_up_grapple"] = new[] { "Hook Runners plunder x1.5, up to the 1000 credit limit.", "They cannot breach walls yet." },
        ["lb_up_salvage_rights"] = new[] { "Salvage crews drain a wreck 1.5x faster.", "Wrecks pay 1.5x the credits." },
        ["exp_up_sonar_nets"] = new[] { "Pickets see and listen 2 cells further." },
    };

    /// <summary>The one-line tooltip text for a unit or building that has an entry here, else null (use the Archive subtitle).</summary>
    public static string? Cameo(string id) => Cameos.GetValueOrDefault(id);

    /// <summary>The hand-written effect lines of an upgrade, else null (use the generated lines).</summary>
    public static IReadOnlyList<string>? Upgrade(string id) => Upgrades.GetValueOrDefault(id);
}
