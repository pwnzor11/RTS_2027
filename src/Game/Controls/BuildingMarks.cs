// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Microsoft.Xna.Framework;
using Rts.Game.Render;
using Rts.Sim.Core;

namespace Rts.Game.Controls;

/// <summary>S47 U-13 (cnc#7 for buildings): which buildings a double-click on one of them marks.</summary>
public static class BuildingMarks
{
    /// <summary>
    /// The ids of the other own, finished buildings of <paramref name="type"/> whose centre lies inside the world box [topLeft, bottomRight]
    /// (zoom-1 pixels); <paramref name="primary"/> itself is left out.
    /// </summary>
    public static List<EntityId> SameTypeOnScreen(IReadOnlyList<BuildingView> buildings, byte owner, int type, EntityId primary, Vector2 topLeft, Vector2 bottomRight)
    {
        var ids = new List<EntityId>();
        foreach (BuildingView b in buildings)
        {
            if (b.Ghost || b.Player != owner || b.TypeIndex != type || b.Id == primary) continue;
            var centre = new Vector2((b.Cell.X + b.FootprintW / 2f) * Camera.CellPx, (b.Cell.Y + b.FootprintH / 2f) * Camera.CellPx);
            if (centre.X >= topLeft.X && centre.X <= bottomRight.X && centre.Y >= topLeft.Y && centre.Y <= bottomRight.Y) ids.Add(b.Id);
        }

        return ids;
    }
}
