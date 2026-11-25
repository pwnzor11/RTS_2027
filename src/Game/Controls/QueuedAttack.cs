// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Game.Sandbox;
using Rts.Sim.Core;

namespace Rts.Game.Controls;

/// <summary>
/// U-05 (c): Shift + right-click on an enemy appends an attack to the unit's order queue instead of replacing it. The sim's order queue
/// holds waypoints (a cell, move or attack-move), so a queued attack is an attack-move waypoint on the target's cell: the unit walks
/// there when its turn comes and fights what it finds, the target included. It goes through the ordinary QueueOrder command (LAN-safe, no sim change).
/// </summary>
public static class QueuedAttack
{
    /// <summary>The cell to queue an attack-move on for <paramref name="target"/>: a building's centre cell, a unit's current cell; null when it is not in the snapshot.</summary>
    public static CellCoord? CellFor(ActorRef target, IReadOnlyList<UnitView> units, IReadOnlyList<BuildingView> buildings)
    {
        if (target.Kind == ActorKind.Building)
        {
            foreach (BuildingView b in buildings)
                if (b.Id == target.Id) return new CellCoord(b.Cell.X + b.FootprintW / 2, b.Cell.Y + b.FootprintH / 2);
            return null;
        }

        foreach (UnitView u in units)
            if (UnitKey.Of(u) == new UnitKey(target.Kind, target.Id)) return u.Position.ToCell();
        return null;
    }
}
