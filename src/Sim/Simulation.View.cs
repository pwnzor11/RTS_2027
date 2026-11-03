// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Sim.View;

namespace Rts.Sim;

// Player-view part of Simulation. OWNER: M1.5-06 (AI host). Additive only: no state, no sim behaviour.
public sealed partial class Simulation
{
    /// <summary>A read-only, fog-honest view of this simulation for one player (D-051). Allocation-free; cheap to
    /// call every tick. The view reads live state, so use it between Steps, never inside one.</summary>
    public PlayerView ViewFor(byte player)
    {
        if (player >= Setup.PlayerCount) throw new ArgumentOutOfRangeException(nameof(player));
        return new PlayerView(this, player);
    }
}
