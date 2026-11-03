// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Sim;
using Rts.Sim.Core;
using Rts.Sim.Rules;

namespace Rts.Data;

/// <summary>
/// The M2 skirmish start (D-062, D-067): every non-neutral player gets one Construction Yard at (or as near as
/// possible to) its map start position, plus the economy's starting cash (set by the sim). Shared by the game's
/// skirmish screen, the AI harness and CI so every skirmish starts the same way. Call before the first Step.
/// </summary>
public static class SkirmishStart
{
    /// <summary>Id of the starting structure.</summary>
    public const string StartBuildingId = "auth_conyard";

    /// <summary>Places each player's start building. Returns the placed ids by player (None if no spot was found).</summary>
    public static EntityId[] Apply(Simulation sim)
    {
        ArgumentNullException.ThrowIfNull(sim);
        SimRules rules = sim.Setup.Rules;
        int type = SimRules.IndexOf(rules.Buildings, StartBuildingId, b => b.Id);
        if (type < 0) throw new InvalidOperationException($"rules have no '{StartBuildingId}'");
        BuildingType bt = rules.Buildings[type];
        var map = sim.Setup.Map;
        var placed = new EntityId[sim.Setup.PlayerCount];
        for (int p = 0; p < sim.Setup.PlayerCount; p++)
        {
            placed[p] = EntityId.None;
            if (sim.Players.IsNeutral((byte)p) || p >= map.StartPositions.Count) continue;
            CellCoord s = map.StartPositions[p];
            // centre the footprint on the start cell, then search rings outward in a fixed order
            int cx = s.X - bt.FootprintW / 2, cy = s.Y - bt.FootprintH / 2;
            for (int r = 0; r <= 8 && placed[p] == EntityId.None; r++)
                for (int dy = -r; dy <= r && placed[p] == EntityId.None; dy++)
                    for (int dx = -r; dx <= r && placed[p] == EntityId.None; dx++)
                    {
                        if (Math.Max(Math.Abs(dx), Math.Abs(dy)) != r) continue;
                        var tl = new CellCoord(cx + dx, cy + dy);
                        if (tl.X < 0 || tl.Y < 0 || tl.X + bt.FootprintW > map.Width || tl.Y + bt.FootprintH > map.Height) continue;
                        placed[p] = sim.PlaceBuilding((byte)p, type, tl);
                    }
        }
        return placed;
    }
}
