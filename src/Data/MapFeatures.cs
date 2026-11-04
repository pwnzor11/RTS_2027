// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Sim;
using Rts.Sim.Core;
using Rts.Sim.Rules;

namespace Rts.Data;

/// <summary>Places a map's neutral features at setup (design 08 §3, Q-M4-b). OWNER: M4-15.</summary>
public static class MapFeatures
{
    /// <summary>Places every feature of <paramref name="meta"/> for <paramref name="neutralSlot"/> in list order via
    /// <see cref="Simulation.PlaceBuilding"/>, before tick 0 (so the setup log, and with it replays, reproduce them). Returns the ids
    /// in list order (None where placement failed: unknown type or blocked cell).</summary>
    public static EntityId[] Place(Simulation sim, MapMeta meta, byte neutralSlot)
    {
        ArgumentNullException.ThrowIfNull(sim);
        ArgumentNullException.ThrowIfNull(meta);
        IReadOnlyList<MapFeature> features = meta.Features;
        var placed = new EntityId[features.Count];
        for (int k = 0; k < features.Count; k++)
        {
            int type = SimRules.IndexOf(sim.Setup.Rules.Buildings, features[k].Type, b => b.Id);
            placed[k] = type < 0 ? EntityId.None : sim.PlaceBuilding(neutralSlot, type, features[k].Cell);
        }
        return placed;
    }
}
