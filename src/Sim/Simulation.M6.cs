// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Sim.Core;
using Rts.Sim.Rigs;
using Rts.Sim.Rules;
using Rts.Sim.Salvage;

namespace Rts.Sim;

// M6-02 (docs/design/10_m6_plan.md §3): contracts of The Left Behind. Accessors for the "rigs" and "salvage" parts. No behaviour yet:
// CommandKind.Pack is accepted and ignored until M6-03; the Salvage/Pack/Withdraw missions run the idle handler until M6-03..05.
public sealed partial class Simulation
{
    /// <summary>The "rigs" part (M6-02): pack state per rig building and per packed rig vehicle.</summary>
    public RigState Rigs => _rigs;

    /// <summary>The "salvage" part (M6-02): the wreck table.</summary>
    public SalvageState Salvage => _salvage;

    /// <summary>Product in permille of one upgrade field over the upgrades <paramref name="player"/> has researched (1000 = no change).
    /// A field value of 0 means "no effect" and is skipped. Lead helper (session 9) shared by M6-03/04/05.</summary>
    internal int UpgradePermille(byte player, Func<UpgradeType, int> field)
    {
        IReadOnlyList<UpgradeType> ups = RulesOf(player).Upgrades;
        long p = 1000;
        for (int u = 0; u < ups.Count; u++)
        {
            if (!HasUpgrade(player, u)) continue;
            int v = field(ups[u]);
            if (v > 0) p = p * v / 1000;
        }
        return (int)Math.Clamp(p, 0, int.MaxValue);
    }

    /// <summary>True if any researched upgrade of <paramref name="player"/> sets the flag.</summary>
    internal bool HasUpgradeFlag(byte player, Func<UpgradeType, bool> flag)
    {
        IReadOnlyList<UpgradeType> ups = RulesOf(player).Upgrades;
        for (int u = 0; u < ups.Count; u++) if (HasUpgrade(player, u) && flag(ups[u])) return true;
        return false;
    }
}
