// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
namespace Rts.Sim;

// Foundation Grouting enclosure and gates (design 05 §2, D-076). OWNER: M3-04. State: Ground.Hardened (bit per player slot),
// Ground.HardeningDirty (set by the building hooks in Simulation.M3.cs). Gate pathing and the walls-may-close placement rule
// are M3-04's own edits in Pathing/ and Simulation.cs CanPlace.
public sealed partial class Simulation
{
    /// <summary>Per tick (first in RunGround): when Ground.HardeningDirty, recompute every team's enclosure for players with the
    /// "grouting" upgrade effect, then clear the flag.</summary>
    private void RunHardening()
    {
        // M3-04 fills this
    }
}
