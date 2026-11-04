// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Sim.Core;

namespace Rts.Sim;

// Spectator stats counters (design 08 §2). OWNER: M4-06. State: Stats (the "stats" part). Stubs and call sites placed by M4-01:
//   OnLethalHit       ApplyPendingDamage (Simulation.Combat.cs), when a hit takes health from > 0 to <= 0
//   OnStatsBuilt      SpawnProducedVehicle / SpawnProducedInfantry and ApplyPlace (Simulation.Production.cs)
//   OnStatsLost       RunCleanup (Simulation.cs), for each dead actor before it is removed
//   OnStatsHarvested  HarvestMission unload (credits actually stored)
//   OnStatsSpent      Spend (Simulation.Economy.cs), credits actually spent
public sealed partial class Simulation
{
    /// <summary>A hit took <paramref name="target"/> from positive health to zero or below; <paramref name="source"/> owns the hit.</summary>
    private void OnLethalHit(ActorRef target, byte source)
    {
        // M4-06 fills
    }

    /// <summary>Player <paramref name="p"/> produced a unit or placed a building of kind <paramref name="k"/>.</summary>
    private void OnStatsBuilt(byte p, ActorKind k)
    {
        // M4-06 fills
    }

    /// <summary>Player <paramref name="p"/> lost an actor of kind <paramref name="k"/> (it died).</summary>
    private void OnStatsLost(byte p, ActorKind k)
    {
        // M4-06 fills
    }

    /// <summary>Player <paramref name="p"/> stored <paramref name="credits"/> from a harvester unload.</summary>
    internal void OnStatsHarvested(byte p, int credits)
    {
        // M4-06 fills
    }

    /// <summary>Player <paramref name="p"/> spent <paramref name="credits"/> (Spend's return value).</summary>
    private void OnStatsSpent(byte p, int credits)
    {
        // M4-06 fills
    }
}
