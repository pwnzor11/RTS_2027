// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Sim.Core;
using Rts.Sim.Rules;
using Rts.Sim.View;

namespace Rts.Game.Ui.Sidebar;

/// <summary>
/// Whether a ship in the vehicle lane has nowhere to leave from (M7-14, D-147 item 4): the sim accepts the order and the ship waits, so the
/// queue never reports a block. This recomputes the sim's own test (a factory whose footprint or the cells around it hold water the ship
/// can cross) from public data, for the sidebar tooltip only. Pure: the terrain question is passed in.
/// </summary>
public static class ShipyardReach
{
    /// <summary>
    /// True when vehicle type <paramref name="vehicleType"/> is a ship, the player has at least one vehicle factory, and none of those factories
    /// touches water the ship can cross. A player with no factory at all is not this case (that is <see cref="QueueBlockReason.NoFactory"/>).
    /// </summary>
    /// <param name="rules">The rules tables.</param>
    /// <param name="vehicleType">Index into <see cref="SimRules.Vehicles"/>.</param>
    /// <param name="own">The player's own actors (<c>PlayerView.CopyOwn</c>); enemy or ghost entries are ignored.</param>
    /// <param name="waterPassable">True when the cell can be crossed by the ship's locomotion (<c>PlayerView.IsPassable</c>).</param>
    public static bool NeedsShipyard(SimRules rules, int vehicleType, IReadOnlyList<ViewActor> own, Func<CellCoord, bool> waterPassable)
    {
        if (vehicleType < 0 || vehicleType >= rules.Vehicles.Count) return false;
        if (rules.Vehicles[vehicleType].Locomotion != Locomotion.Naval) return false;
        bool anyFactory = false;
        foreach (ViewActor a in own)
        {
            if (a.Kind != ActorKind.Building || a.Ghost || a.TypeIndex < 0 || a.TypeIndex >= rules.Buildings.Count) continue;
            if (rules.Buildings[a.TypeIndex].Produces != ProductionCategory.Vehicle) continue;
            anyFactory = true;
            if (TouchesWater(a, waterPassable)) return false;
        }
        return anyFactory;
    }

    // assumed — confirm: the sim checks the exit cell and the footprint; the exit is not reproduced here, so the cells one step around
    // the footprint stand in for it (a shore factory has water beside it either way).
    private static bool TouchesWater(ViewActor a, Func<CellCoord, bool> waterPassable)
    {
        for (int y = a.Cell.Y - 1; y <= a.Cell.Y + a.FootprintH; y++)
            for (int x = a.Cell.X - 1; x <= a.Cell.X + a.FootprintW; x++)
                if (waterPassable(new CellCoord(x, y))) return true;
        return false;
    }
}
