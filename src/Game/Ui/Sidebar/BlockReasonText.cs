// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Sim.Core;

namespace Rts.Game.Ui.Sidebar;

/// <summary>The words shown on a greyed cameo's tooltip (D-066).</summary>
public static class BlockReasonText
{
    /// <summary>Tooltip text for a block reason; empty for <see cref="QueueBlockReason.None"/>.</summary>
    public static string Of(QueueBlockReason reason) => reason switch
    {
        QueueBlockReason.None => "",
        QueueBlockReason.QueueFull => "Queue is full",
        QueueBlockReason.StructureWaiting => "Place the finished structure first",
        QueueBlockReason.NoFactory => "Needs a factory",
        QueueBlockReason.Defeated => "You are defeated",
        QueueBlockReason.NotBuildable => "Not available",
        _ => "Not available",
    };
}
