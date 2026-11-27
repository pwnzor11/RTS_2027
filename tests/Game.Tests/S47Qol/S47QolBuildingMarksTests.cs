// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Microsoft.Xna.Framework;
using Rts.Game.Controls;
using Rts.Game.Render;
using Rts.Sim.Core;

namespace Rts.Game.Tests.S47Qol;

/// <summary>S47 U-13 (cnc#7 for buildings): a double-click marks the other buildings of the same type on screen.</summary>
public sealed class S47QolBuildingMarksTests
{
    private static BuildingView B(ushort id, byte owner, int type, int x, int y, bool ghost = false) =>
        new(new EntityId(id, 0), owner, type, new CellCoord(x, y), 2, 2, 100, ghost);

    [Fact]
    public void Only_own_finished_buildings_of_the_type_inside_the_view_are_marked()
    {
        BuildingView[] all =
        {
            B(1, 0, 5, 10, 10),            // the one clicked
            B(2, 0, 5, 12, 10),            // marked
            B(3, 0, 5, 80, 80),            // off screen
            B(4, 0, 6, 14, 10),            // other type
            B(5, 1, 5, 16, 10),            // enemy
            B(6, 0, 5, 18, 10, ghost: true),
        };
        List<EntityId> ids = BuildingMarks.SameTypeOnScreen(all, 0, 5, new EntityId(1, 0), Vector2.Zero, new Vector2(40 * Camera.CellPx, 40 * Camera.CellPx));
        Assert.Equal(new[] { new EntityId(2, 0) }, ids);
    }
}
