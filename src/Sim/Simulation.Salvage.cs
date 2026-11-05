// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Sim.Core;

namespace Rts.Sim;

// M6-04 (OWNER: M6-04 package; hooks placed by the lead in session 9). Wrecks and salvage (10_m6_plan.md §3.2–3.3).
// Hooks: Apply (Harvest/Return) -> TryApplySalvageCommand first; StepCore -> RunSalvage (after RunEconomy);
// RunCleanup -> SpawnWreckFor (each dying vehicle or building, before removal); MissionKind.Salvage -> Missions/SalvageMission.cs.
public sealed partial class Simulation
{
    /// <summary>Harvest/Return from a unit with salvage_rate &gt; 0: route to the Salvage mission. True = handled (skip the economy).</summary>
    private bool TryApplySalvageCommand(Command c) => false;

    /// <summary>Per tick after the economy: wreck decay and expiry in slot order.</summary>
    private void RunSalvage() { }

    /// <summary>RunCleanup, for a dying vehicle or building (any team) before it is removed: add a wreck when wreck_ticks &gt; 0.</summary>
    private void SpawnWreckFor(ActorKind kind, int i) { }
}
