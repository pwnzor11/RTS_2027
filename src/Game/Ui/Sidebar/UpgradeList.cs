// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Sim.Core;
using Rts.Sim.Rules;

namespace Rts.Game.Ui.Sidebar;

/// <summary>Where an upgrade stands for the local player.</summary>
public enum UpgradeState
{
    /// <summary>Can be researched now (it is in <see cref="PlayerHud.BuildableUpgrades"/>).</summary>
    Available,
    /// <summary>In the Upgrade queue.</summary>
    Researching,
    /// <summary>Already researched.</summary>
    Owned,
}

/// <summary>One row of the Upgrades tab.</summary>
/// <param name="Index">Index in <c>SimRules.Upgrades</c> (the bit in the player's mask).</param>
/// <param name="Id">Upgrade id, for the name and cameo.</param>
/// <param name="Cost">Cost in credits.</param>
/// <param name="State">Researchable, in progress or owned.</param>
/// <param name="ProgressPermille">Progress 0..1000 of the front queued item when researching, else 0.</param>
public readonly record struct UpgradeEntry(int Index, string Id, int Cost, UpgradeState State, int ProgressPermille);

/// <summary>
/// What the Upgrades tab lists (pure, so it is unit-tested): every upgrade the player can research now, the ones being researched
/// and the ones already owned, in rules order. Upgrades that are none of these (wrong faction, prerequisite missing) are not shown,
/// the same way the other tabs hide locked types. It reads the HUD only; the sim decides what is buildable.
/// </summary>
public static class UpgradeList
{
    /// <summary>The entries to show for this HUD.</summary>
    /// <param name="hud">The player's HUD.</param>
    /// <param name="upgrades">The rules' upgrade table.</param>
    public static IReadOnlyList<UpgradeEntry> Entries(PlayerHud hud, IReadOnlyList<UpgradeType> upgrades)
    {
        var list = new List<UpgradeEntry>();
        for (int i = 0; i < upgrades.Count && i < RuleCounts.MaxUpgrades; i++)
        {
            UpgradeType u = upgrades[i];
            if ((hud.Upgrades & (1UL << i)) != 0)
            {
                list.Add(new UpgradeEntry(i, u.Id, u.Cost, UpgradeState.Owned, 0));
            }
            else if (QueuePosition(hud, i, out int progress) >= 0)
            {
                list.Add(new UpgradeEntry(i, u.Id, u.Cost, UpgradeState.Researching, progress));
            }
            else if (hud.BuildableUpgrades.Contains(i))
            {
                list.Add(new UpgradeEntry(i, u.Id, u.Cost, UpgradeState.Available, 0));
            }
        }

        return list;
    }

    /// <summary>Position of the upgrade in the Upgrade queue (0 = front), or -1; <paramref name="progress"/> is the front item's progress.</summary>
    public static int QueuePosition(PlayerHud hud, int upgradeIndex, out int progress)
    {
        progress = 0;
        foreach (QueueView q in hud.Queues)
        {
            if (q.Category != ProductionCategory.Upgrade) continue;
            for (int i = 0; i < q.Items.Count; i++)
            {
                if (q.Items[i].TypeIndex != upgradeIndex) continue;
                progress = q.Items[i].ProgressPermille;
                return i;
            }
        }

        return -1;
    }

    /// <summary>S15: true when the upgrade is the front item of the Upgrade queue and on hold.</summary>
    public static bool IsHeld(PlayerHud hud, int upgradeIndex) =>
        SidebarCommands.FrontHeld(hud, ProductionCategory.Upgrade, upgradeIndex);

    /// <summary>The command a left click on an available entry issues: research it, else nothing (no HUD, so no resume).</summary>
    public static Command? LeftClick(byte player, UpgradeEntry entry) =>
        entry.State == UpgradeState.Available ? SidebarCommands.BuildUpgrade(player, entry.Index) : null;

    /// <summary>The command a left click on an entry issues: research it when available, resume it when held, else nothing.</summary>
    public static Command? LeftClick(byte player, PlayerHud hud, UpgradeEntry entry) =>
        entry.State == UpgradeState.Available ? SidebarCommands.BuildUpgrade(player, entry.Index)
        : entry.State == UpgradeState.Researching && IsHeld(hud, entry.Index) ? SidebarCommands.Hold(player, ProductionCategory.Upgrade, false)
        : null;

    /// <summary>S15: the command a right click on an entry issues: hold the research in progress, cancel it when held or queued behind, else nothing.</summary>
    public static Command? RightClick(byte player, PlayerHud hud, UpgradeEntry entry)
    {
        if (entry.State != UpgradeState.Researching) return null;
        return QueuePosition(hud, entry.Index, out _) == 0 && !IsHeld(hud, entry.Index)
            ? SidebarCommands.Hold(player, ProductionCategory.Upgrade, true)
            : SidebarCommands.CancelUpgrade(player, hud, entry.Index);
    }
}
