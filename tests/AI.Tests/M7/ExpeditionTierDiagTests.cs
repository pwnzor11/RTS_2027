// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Data;
using Rts.Sim.Rules;
using Xunit.Abstractions;

namespace AI.Tests.M7;

/// <summary>S12: Expedition AI at the lower tiers escorts convoys and strikes from the sea.</summary>
public sealed class ExpeditionTierDiagTests(ITestOutputHelper log)
{
    private static DifficultyProfile Tier(int tier) => TestWorld.Tiers[tier - 1];

    [Theory]
    [InlineData(1, Faction.Directorate)]
    [InlineData(3, Faction.Authority)]
    public void Diag(int tier, Faction enemy)
    {
        var rig = new ExpeditionRig(Tier(tier), enemy);
        for (int step = 0; step < 6; step++)
        {
            rig.Run(3000);
            log.WriteLine($"tick {rig.Sim.Tick}: waves {rig.Ai.Waves} strikes {rig.Ai.FleetStrikes} escorts {rig.Ai.EscortOrders} sorties {rig.Ai.Sorties} withdrawals {rig.Ai.Withdrawals} bld {rig.Buildings(0)}/{rig.Buildings(1)} :: {rig.Ai.MilitaryDebug} :: {rig.Ai.ExpeditionDebug}");
        }
    }
}
