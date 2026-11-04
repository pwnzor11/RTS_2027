// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Sim.Core;

namespace Rts.Sim;

// Upgrade research (design 05 §4). OWNER: M3-06. Queue: ProductionState lane ProductionCategory.Upgrade (index 5, Arg2 = upgrade
// index). Finishing sets Players.Upgrades[player] |= 1UL << index.
public sealed partial class Simulation
{
    /// <summary>Build with Arg = ProductionCategory.Upgrade (Arg2 = upgrade index).</summary>
    private void ApplyBuildUpgrade(Command c)
    {
        // M3-06 fills this
    }

    /// <summary>Cancel with Arg = ProductionCategory.Upgrade (Arg2 = queue position).</summary>
    private void ApplyCancelUpgrade(Command c)
    {
        // M3-06 fills this
    }

    /// <summary>Per tick (after RunProductionTick): progress the Upgrade lane of every player.</summary>
    private void RunUpgradeResearch()
    {
        // M3-06 fills this
    }

    /// <summary>
    /// True if a Build(Upgrade, <paramref name="upgradeIndex"/>) by <paramref name="player"/> would be accepted now (faction
    /// matches, not yet researched, prerequisites met, a researching building alive, queue room). Feeds
    /// <see cref="PlayerHud.BuildableUpgrades"/>. M3-01 stub: always false.
    /// </summary>
    public bool CanResearch(byte player, int upgradeIndex)
    {
        // M3-06 fills this
        return false;
    }
}
