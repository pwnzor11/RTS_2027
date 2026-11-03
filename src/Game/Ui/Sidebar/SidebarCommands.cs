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

    /// <summary>What a left click on this option does: place a finished one, queue an unblocked one, else nothing.</summary>
    public static CameoClick LeftClick(PlayerHud hud, BuildOption option)
    {
        bool structure = option.Category is ProductionCategory.Structure or ProductionCategory.Defence;
        if (structure && ReadyType(hud, option.Category) == option.TypeIndex) return CameoClick.EnterPlacement;
        return option.Blocked == QueueBlockReason.None ? CameoClick.Build : CameoClick.None;
    }

    /// <summary>The Build command for an option, or null when it is blocked.</summary>
    public static Command? Build(byte player, BuildOption option) =>
        option.Blocked != QueueBlockReason.None ? null
            : new Command(0, player, 0, CommandKind.Build, EntityId.None, EntityId.None, default, (int)option.Category, option.TypeIndex);

    /// <summary>
    /// The Cancel command for a right click: the front-most queued item of the option's type (position 0 first), or null when none
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
