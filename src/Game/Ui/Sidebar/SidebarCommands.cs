// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Sim.Core;
using Rts.Sim.Rules;

namespace Rts.Game.Ui.Sidebar;

/// <summary>What a left click on a cameo does.</summary>
public enum CameoClick
{
    /// <summary>Nothing (the option is blocked).</summary>
    None,
    /// <summary>Queue one more (a Build command).</summary>
    Build,
    /// <summary>The item is finished: enter placement mode.</summary>
    EnterPlacement,
    /// <summary>S15: the item is on hold; resume it (a Hold command with Arg2 = 0).</summary>
    Resume,
}

/// <summary>
/// Maps sidebar clicks to sim commands (payloads as documented on <see cref="Command"/>). Pure: nothing here reads the sim, so
/// rules stay in the sim; the sidebar only asks the HUD snapshot what is blocked. ExecTick and Sequence are left 0 for
/// <see cref="Rts.Game.Host.SimHost.Issue"/> to stamp.
/// </summary>
public static class SidebarCommands
{
    /// <summary>The finished structure or defence waiting in a category, or -1.</summary>
    public static int ReadyType(PlayerHud hud, ProductionCategory category)
    {
        foreach (QueueView q in hud.Queues)
            if (q.Category == category) return q.ReadyTypeIndex;
        return -1;
    }

    /// <summary>S15: true when the front item of the category's queue is this type and is on hold.</summary>
    public static bool FrontHeld(PlayerHud hud, ProductionCategory category, int typeIndex)
    {
        foreach (QueueView q in hud.Queues)
            if (q.Category == category)
                return q.Items.Count > 0 && q.Items[0].TypeIndex == typeIndex && q.Items[0].Held;
        return false;
    }

    /// <summary>S15: the Hold command for a category: hold (true) or resume (false) the front item.</summary>
    public static Command Hold(byte player, ProductionCategory category, bool hold) =>
        new(0, player, 0, CommandKind.Hold, EntityId.None, EntityId.None, default, (int)category, hold ? 1 : 0);

    /// <summary>
    /// S15: the command a right click on a cameo issues, C&amp;C style. The front item in progress goes on hold; a held front item is
    /// cancelled (refund as before); an item queued behind the front is cancelled at once; null when none of that type is queued.
    /// </summary>
    public static Command? RightClick(byte player, PlayerHud hud, BuildOption option)
    {
        foreach (QueueView q in hud.Queues)
        {
            if (q.Category != option.Category) continue;
            for (int i = 0; i < q.Items.Count; i++)
            {
                if (q.Items[i].TypeIndex != option.TypeIndex) continue;
                if (i == 0 && !q.Items[0].Held) return Hold(player, option.Category, true);
                return new Command(0, player, 0, CommandKind.Cancel, EntityId.None, EntityId.None, default, (int)option.Category, i);
            }
        }
        return null;
    }

    /// <summary>What a left click on this option does: place a finished one, queue an unblocked one, else nothing.</summary>
    public static CameoClick LeftClick(PlayerHud hud, BuildOption option)
    {
        if (FrontHeld(hud, option.Category, option.TypeIndex)) return CameoClick.Resume;   // S15: a held item resumes on left click
        bool structure = option.Category is ProductionCategory.Structure or ProductionCategory.Defence;
        if (structure && ReadyType(hud, option.Category) == option.TypeIndex) return CameoClick.EnterPlacement;
        return option.Blocked == QueueBlockReason.None ? CameoClick.Build : CameoClick.None;
    }

    /// <summary>M11-10g: how many items one cameo click queues: five for Shift+click on a unit cameo, else one.</summary>
    public static int QueueCount(ProductionCategory category, bool shift) =>
        shift && category is ProductionCategory.Infantry or ProductionCategory.Vehicle or ProductionCategory.Aircraft ? ShiftQueue : 1;

    /// <summary>Items queued by a Shift+click on a unit cameo.</summary>
    public const int ShiftQueue = 5;

    /// <summary>The Build command for an option, or null when it is blocked.</summary>
    public static Command? Build(byte player, BuildOption option) =>
        option.Blocked != QueueBlockReason.None ? null
            : new Command(0, player, 0, CommandKind.Build, EntityId.None, EntityId.None, default, (int)option.Category, option.TypeIndex);

    /// <summary>
    /// The Cancel command (plain cancel, no hold step): the front-most queued item of the option's type (position 0 first), or null when none
    /// of that type is queued. A finished structure waiting for placement cannot be cancelled.
    /// </summary>
    public static Command? Cancel(byte player, PlayerHud hud, BuildOption option)
    {
        foreach (QueueView q in hud.Queues)
        {
            if (q.Category != option.Category) continue;
            for (int i = 0; i < q.Items.Count; i++)
                if (q.Items[i].TypeIndex == option.TypeIndex)
                    return new Command(0, player, 0, CommandKind.Cancel, EntityId.None, EntityId.None, default, (int)option.Category, i);
        }
        return null;
    }

    /// <summary>
    /// The Build command that researches upgrade <paramref name="upgradeIndex"/> (index in <c>SimRules.Upgrades</c>): the Upgrade
    /// category in Arg and the index in Arg2 (see <see cref="Command"/>). The sim picks the researching building.
    /// </summary>
    public static Command BuildUpgrade(byte player, int upgradeIndex) =>
        new(0, player, 0, CommandKind.Build, EntityId.None, EntityId.None, default, (int)ProductionCategory.Upgrade, upgradeIndex);

    /// <summary>
    /// The Cancel command for a queued upgrade: the Upgrade category in Arg and the item's position in the Upgrade queue in Arg2, or
    /// null when that upgrade is not queued.
    /// </summary>
    public static Command? CancelUpgrade(byte player, PlayerHud hud, int upgradeIndex)
    {
        foreach (QueueView q in hud.Queues)
        {
            if (q.Category != ProductionCategory.Upgrade) continue;
            for (int i = 0; i < q.Items.Count; i++)
                if (q.Items[i].TypeIndex == upgradeIndex)
                    return new Command(0, player, 0, CommandKind.Cancel, EntityId.None, EntityId.None, default, (int)ProductionCategory.Upgrade, i);
        }

        return null;
    }

    /// <summary>Place the finished structure of building type <paramref name="buildingType"/> with its top-left at <paramref name="topLeft"/>.</summary>
    public static Command Place(byte player, int buildingType, CellCoord topLeft) =>
        new(0, player, 0, CommandKind.Place, EntityId.None, EntityId.None, topLeft, buildingType);

    /// <summary>Sell an own building.</summary>
    public static Command Sell(byte player, EntityId building) =>
        new(0, player, 0, CommandKind.Sell, building, EntityId.None, default, (int)ActorKind.Building);

    /// <summary>Toggle repair on an own building.</summary>
    public static Command Repair(byte player, EntityId building) =>
        new(0, player, 0, CommandKind.Repair, building, EntityId.None, default, (int)ActorKind.Building);

    /// <summary>Make a factory the primary one and set its rally point.</summary>
    public static Command Rally(byte player, EntityId factory, CellCoord cell) =>
        new(0, player, 0, CommandKind.Rally, factory, EntityId.None, cell, 0);

    /// <summary>
    /// Top-left cell of a footprint that is centred on the cell under the cursor (the same centring the skirmish start uses).
    /// </summary>
    public static CellCoord TopLeftFor(CellCoord cursorCell, int footprintW, int footprintH) =>
        new(cursorCell.X - footprintW / 2, cursorCell.Y - footprintH / 2);
}
