// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Sim.Core;

namespace Rts.Sim;

// M6-07 (OWNER: M6-07 package; hook placed by the lead in session 9). The Second Drift storm (10_m6_plan.md §3.2, effects v2 storms).
// Hook: StepCore -> RunStorms (after RunEffects, before combat). Firing goes through the existing superweapon path.
public sealed partial class Simulation
{
    /// <summary>Per tick: move storm timers on, contaminate cells, slow enemy vehicles inside a storm.</summary>
    private void RunStorms() { }

    /// <summary>Speed multiplier in permille that storms apply to a vehicle of <paramref name="player"/> at <paramref name="pos"/>
    /// (1000 = none; below 1000 inside an enemy storm). Read by the aura recompute (M6-06, Simulation.Effects.cs) every 10 ticks.</summary>
    internal int StormSpeedPermilleAt(byte player, LeptonCoord pos) => 1000;
}
