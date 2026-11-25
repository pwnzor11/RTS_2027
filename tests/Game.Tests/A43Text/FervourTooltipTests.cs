// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Game.Ui.Sidebar;
using Rts.Sim.Rules;

namespace Rts.Game.Tests.A43Text;

/// <summary>A43 ROSTER-AD-04: rate_of_fire is ticks between shots, so a x0.9 factor is a faster gun; the tooltip must not read as a nerf.</summary>
public sealed class FervourTooltipTests
{
    [Fact]
    public void Rate_of_fire_is_described_as_reload_time()
    {
        string line = UpgradeEffectText.Describe(new EffectDef("dir_fx_fervour_rof", "mul", "class:infantry", "rate_of_fire", 900));
        Assert.Contains("reload time", line);
        Assert.DoesNotContain("rate of fire", line);
    }
}
