// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Game.Ui.Sidebar;
using Rts.Sim;
using Rts.Sim.Core;
using Rts.Sim.Rules;

namespace Rts.Game.Tests.S19;

/// <summary>M13-06: the sidebar's Cancel and right click on a finished structure send the ready-cancel (Arg2 = -1).</summary>
public sealed class ReadyCancelUiTests
{
    private static PlayerHud Hud(int ready, params QueueItemView[] items)
    {
        var queues = new List<QueueView>();
        foreach (ProductionCategory c in new[] { ProductionCategory.Infantry, ProductionCategory.Vehicle, ProductionCategory.Aircraft,
                     ProductionCategory.Structure, ProductionCategory.Defence })
            queues.Add(c == ProductionCategory.Structure ? new QueueView(c, items, ready) : new QueueView(c, Array.Empty<QueueItemView>(), -1));
        return new PlayerHud(100, 0, 1000, 0, 0, queues, Array.Empty<(ProductionCategory, int)>(), false);
    }

    private static BuildOption Opt(int t) => new(ProductionCategory.Structure, t, true, QueueBlockReason.StructureWaiting, 100);

    [Fact]
    public void CancelAndRightClick_OnTheReadyCameo_SendTheReadyCancel()
    {
        PlayerHud hud = Hud(4, new QueueItemView(7, 0, false));
        foreach (Command c in new[] { SidebarCommands.Cancel(0, hud, Opt(4))!.Value, SidebarCommands.RightClick(0, hud, Opt(4))!.Value })
        {
            Assert.Equal(CommandKind.Cancel, c.Kind);
            Assert.Equal((int)ProductionCategory.Structure, c.Arg);
            Assert.Equal(Simulation.ReadyCancelPosition, c.Arg2);
        }
        Assert.Equal(0, SidebarCommands.Cancel(0, hud, Opt(7))!.Value.Arg2);   // the item behind keeps its queue position
    }
}
