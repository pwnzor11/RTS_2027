// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Sim.Core;
using Rts.Sim.Rules;

namespace Rts.AI.Recruit;

/// <summary>
/// M3-11 (flag UseUpgrades): queues the cheapest researchable upgrade when the structure that researches it stands, the army has a body
/// and spare cash covers it, at most one order per <see cref="GapTicks"/>. The research structure itself comes from the build order
/// (see <see cref="TierPlanner.ExtendBuild"/>).
/// </summary>
internal sealed class UpgradeManager
{
    /// <summary>Army size before research is bought.</summary>
    public const int MinArmy = 12;   // (tune) M4-12: 6 -> 12, research drained the army of tier-3+ AIs
    /// <summary>Credits left in hand after paying for an upgrade.</summary>
    public const int Cushion = 300;
    /// <summary>Ticks between research orders.</summary>
    public const int GapTicks = 400;

    private int _last = -GapTicks;

    /// <summary>Upgrades ordered.</summary>
    public int Ordered { get; private set; }

    public void Think(RecruitKnowledge k, ICommandSink sink, int reserve)
    {
        int tick = k.View.Tick;
        if (tick - _last < GapTicks || k.Army.Count < MinArmy || k.Hud.BuildableUpgrades.Count == 0) return;
        int best = -1;
        foreach (int u in k.Hud.BuildableUpgrades)
            if (best < 0 || k.Rules.Upgrades[u].Cost < k.Rules.Upgrades[best].Cost) best = u;
        if (best < 0 || k.Hud.Cash + k.Hud.Stored < k.Rules.Upgrades[best].Cost + Cushion + reserve) return;
        _last = tick;
        Ordered++;
        sink.Build(ProductionCategory.Upgrade, best);
    }
}
