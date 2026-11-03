// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Microsoft.Xna.Framework;
using Rts.Game.Render;
using Rts.Game.Sandbox;
using Rts.Sim.Core;

namespace Rts.Game.Controls;

/// <summary>
/// Finds the enemy under the cursor for an Attack order. Units first (nearest within the pick radius), then buildings
/// whose footprint holds the clicked cell. Last-seen building ghosts are never targets: they are not live entities.
/// </summary>
public static class TargetPicker
{
    /// <summary>The enemy under a zoom-1 world point, or null.</summary>
    public static ActorRef? Pick(IReadOnlyList<RenderUnit> units, SimSnapshot snapshot, Vector2 world, byte localPlayer)
    {
        CellCoord clicked = Camera.CellAt(world);
        ActorRef? best = null;
        float bestDistSq = float.MaxValue;
        foreach (RenderUnit unit in units)
        {
            if (unit.View.Player == localPlayer) continue;
            float distSq = Vector2.DistanceSquared(unit.World, world);
            bool sameCell = Camera.CellAt(unit.World) == clicked;
            if (!sameCell && distSq > Selection.PickRadiusPx * Selection.PickRadiusPx) continue;
            if (distSq < bestDistSq)
            {
                bestDistSq = distSq;
                best = new ActorRef(UnitKey.Of(unit.View).Kind, unit.View.Id);
            }
        }
        if (best is not null) return best;

        foreach (BuildingView building in snapshot.Buildings)
        {
            if (building.Ghost || building.Player == localPlayer) continue;
            bool inside = clicked.X >= building.Cell.X && clicked.X < building.Cell.X + building.FootprintW
                && clicked.Y >= building.Cell.Y && clicked.Y < building.Cell.Y + building.FootprintH;
            if (inside) return new ActorRef(ActorKind.Building, building.Id);
        }
        return null;
    }
}
