// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Game.Ui.Sidebar;

namespace Rts.Game.Tests.A43Text;

/// <summary>A43 ROSTER-LE-02: the Drydock gates the Cruiser and Carrier only; the Command Ship is not buildable (buildable = false).</summary>
public sealed class DrydockTooltipTests
{
    [Fact]
    public void Drydock_tooltip_does_not_promise_the_Command_Ship()
    {
        string tip = TodayText.Cameo("exp_drydock") ?? throw new InvalidOperationException("no Drydock tooltip");
        Assert.Contains("Builds the Cruiser and Carrier", tip);   // S44 M-03: the Drydock builds them
        Assert.DoesNotContain("Command Ship", tip);
    }
}
