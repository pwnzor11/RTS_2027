// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Sim.Core;
using Rts.Sim.Missions;
using Rts.Sim.Rules;

namespace Rts.AI.Recruit;

/// <summary>Economy for the Recruit tier: keeps the free scraper harvesting (it is never replaced, per the design) and
/// answers "is power short?" so the build order can insert a power plant. A scraper whose home patch runs out is left to
/// the sim: its harvest mission searches the whole map ring by ring (M2-19), so the AI sends no probe orders.</summary>
internal sealed class EconomyManager
{
    /// <summary>Re-issues Harvest to a harvester that has gone idle (the sim starts harvesters on its own; this covers a
    /// scraper that was interrupted).</summary>
    public void Think(RecruitKnowledge k, ICommandSink sink)
    {
        foreach (var h in k.Harvesters)
            if (h.Idle && h.Mission != MissionKind.Harvest)
                sink.Issue(CommandKind.Harvest, h.Id, ActorKind.Vehicle);
    }

    /// <summary>True if building something that drains <paramref name="extraDrain"/> power would leave use above output.</summary>
    public static bool PowerShort(RecruitKnowledge k, int extraDrain) =>
        k.Hud.PowerUse + extraDrain > k.Hud.PowerOut;
}
