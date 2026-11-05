// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Sim.Core;

namespace Rts.Game.Ui.Sidebar;

/// <summary>The words shown on a greyed cameo's tooltip (D-066).</summary>
public static class BlockReasonText
{
    /// <summary>
    /// Tooltip text for a block reason; empty for <see cref="QueueBlockReason.None"/>. The supply cap names the faction's supply building
    /// (<paramref name="supplyBuilding"/>, a display name); with none known the plain line is used.
    /// </summary>
    /// <param name="reason">Why the cameo is greyed.</param>
    /// <param name="supplyBuilding">Display name of the faction's supply building, or null when the faction has none.</param>
    public static string Of(QueueBlockReason reason, string? supplyBuilding = null) => reason switch
    {
        QueueBlockReason.None => "",
        QueueBlockReason.QueueFull => "Queue is full",
        QueueBlockReason.StructureWaiting => "Place the finished structure first",
        QueueBlockReason.NoFactory => "Needs a factory",
        QueueBlockReason.Defeated => "You are defeated",
        QueueBlockReason.NotBuildable => "Not available",
        QueueBlockReason.SupplyCap => string.IsNullOrEmpty(supplyBuilding)
            ? UiText.Get(UiText.SupplyCapReachedPlain)
            : UiText.Format(UiText.SupplyCapReached, "supplyBuilding", supplyBuilding),
        _ => "Not available",
    };
}
