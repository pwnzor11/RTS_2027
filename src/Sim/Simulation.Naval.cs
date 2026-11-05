// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Sim.Core;
using Rts.Sim.Movement;
using Rts.Sim.Pathing;
using Rts.Sim.Rules;
using Rts.Sim.World;

namespace Rts.Sim;

// M7-03 (docs/design/11_m7_plan.md §2.5, D-124): ships. A ship is an ordinary one-cell vehicle of Locomotion.Naval; it uses the vehicle
// movement as it is (turn in place at the type's turn rate, one cell held plus one reserved, so two ships never share a cell). What
// this file adds is the connected-region labels of the sea and the immediate refusal of a move goal the mover can never reach.
// Large ships are drawn bigger than their cell by the game layer (draw_scale_permille); the sim sees one cell.
public sealed partial class Simulation
{
    /// <summary>True when the rules hold at least one ship type (otherwise the "naval" part stays empty and costs nothing).</summary>
    internal bool HasNavalRules
    {
        get
        {
            var vs = Setup.Rules.Vehicles;
            for (int i = 0; i < vs.Count; i++) if (vs[i].Locomotion == Locomotion.Naval) return true;
            return false;
        }
    }

    /// <summary>
    /// Mirrors the current ship region labels into the saved part. Called at one fixed point of the tick (the start of the movement
    /// phase) so the part never depends on who asked for labels in between (the AI view reads the cache, not the part).
    /// </summary>
    internal void EnsureNavalRegions()
    {
        if (!HasNavalRules) return;
        ushort[] labels = RegionLabels(Locomotion.Naval);
        if (!labels.AsSpan().SequenceEqual(_naval.NavalRegion)) labels.CopyTo(_naval.NavalRegion, 0);
    }

    /// <summary>Region labels of the terrain for a movement class (0 = the class cannot enter the cell); a derived cache, never the saved part.</summary>
    internal ushort[] RegionLabels(Locomotion moveClass) => _regions.Get(moveClass, _naval.LoadEpoch);

    /// <summary>
    /// A Move to <paramref name="goal"/> that can never succeed is dropped on the spot: no mission, no path search. A ship is refused
    /// when the goal is not sea or lies in another sea than the ship (a large ship cannot cross shallows, so they split seas for it);
    /// a ground mover is refused a goal on deep or shallow water it cannot enter (a cliff or a walled-in goal is still approached).
    /// </summary>
    private bool MoveGoalRefused(ActorKind kind, MobileStore store, int i, CellCoord goal)
    {
        Locomotion cls = Occupancy.LocoOf(this, kind, store, i);
        if (cls == Locomotion.Air || cls == Locomotion.Burrow) return false;
        if (MoveClass.IsNaval(cls))
        {
            ushort[] labels = RegionLabels(cls);
            CellCoord cur = Occupancy.CurCell(kind, store, i);
            if (!InMap(cur)) return false;
            int here = labels[cur.Y * Setup.Map.Width + cur.X], there = labels[goal.Y * Setup.Map.Width + goal.X];
            return there == 0 || (here != 0 && here != there);
        }
        if (_map.SpeedPercent(goal, cls, Setup.Rules.Terrain) > 0) return false;
        LandType land = _map.LandAt(goal);
        return land == LandType.Water || land == LandType.Shallow;
    }

    /// <summary>PlayerView: sea region of a cell for ships (0 = not sea). Terrain only, so it reveals nothing fog hides.</summary>
    internal int NavalRegionAt(CellCoord c, bool deepOnly)
    {
        if (!InMap(c)) return 0;
        return RegionLabels(deepOnly ? MoveClass.NavalDeep : Locomotion.Naval)[c.Y * Setup.Map.Width + c.X];
    }

    /// <summary>PlayerView: can the player's own ship reach <paramref name="target"/> by sea (same region, goal is sea)? False for anything else.</summary>
    internal bool OwnShipCanReach(byte player, EntityId ship, CellCoord target)
    {
        if (!_units.Slots.IsAlive(ship) || _units.Player[ship.Index] != player || !InMap(target)) return false;
        Locomotion cls = MoveClass.Of(Setup.Rules.Vehicles[_units.TypeIndex[ship.Index]]);
        if (!MoveClass.IsNaval(cls)) return false;
        CellCoord cur = _units.Position[ship.Index].ToCell();
        int a = NavalRegionAt(cur, cls == MoveClass.NavalDeep), b = NavalRegionAt(target, cls == MoveClass.NavalDeep);
        return a != 0 && a == b;
    }
}
