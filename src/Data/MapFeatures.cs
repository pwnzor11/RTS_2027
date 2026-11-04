// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Sim;
using Rts.Sim.Core;

namespace Rts.Data;

/// <summary>Places a map's neutral features at setup (design 08 §3, Q-M4-b). OWNER: M4-15. Stub (M4-01): places nothing.</summary>
public static class MapFeatures
{
    /// <summary>Places every feature of <paramref name="meta"/> for <paramref name="neutralSlot"/> in list order via
    /// <see cref="Simulation.PlaceBuilding"/>, before tick 0 (so the setup log, and with it replays, reproduce them). Returns the ids
    /// in list order (None where placement failed).</summary>
    public static EntityId[] Place(Simulation sim, MapMeta meta, byte neutralSlot)
    {
        ArgumentNullException.ThrowIfNull(sim);
        ArgumentNullException.ThrowIfNull(meta);
        // M4-15 fills
        return Array.Empty<EntityId>();
    }
}
