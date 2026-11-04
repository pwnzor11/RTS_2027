// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Sim.Core;

namespace Rts.Sim;

// Auras and death weapons (design 05 §4). OWNER: M3-06. State: Effects.*RofPermille / *SpeedPermille.
// Air units and weapon Targets are M3-06's own edits in Movement/ and Combat.
public sealed partial class Simulation
{
    /// <summary>Per tick (first in RunEffects): every 10 ticks recompute aura multipliers.</summary>
    private void RunAuras()
    {
        // M3-06 fills this
    }

    /// <summary>A mobile actor is dying (RunCleanup, before removal): fire its DeathWeapon at its cell.</summary>
    private void OnDeathWeapon(ActorKind kind, int i)
    {
        // M3-06 fills this
    }
}
