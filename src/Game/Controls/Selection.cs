// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Microsoft.Xna.Framework;
using Rts.Game.Render;
using Rts.Game.Sandbox;
using Rts.Sim.Core;

namespace Rts.Game.Controls;

/// <summary>
/// The set of units the local player has selected, by <see cref="UnitKey"/>. Ids are generational, so a selected unit
/// that dies is dropped by <see cref="Prune"/> and never confused with a unit later spawned in the same slot.
/// </summary>
public sealed class Selection
{
    /// <summary>
    /// Pick radius around a unit, in zoom-1 world pixels. A click inside the same cell also hits. Grows with
    /// <see cref="SpriteLayout.UnitDrawScale"/> (M11-06) so a click on the larger visible body still selects it.
    /// </summary>
    public const float PickRadiusPx = 32f * SpriteLayout.UnitDrawScale;

    private readonly List<UnitKey> _units = new();

    /// <summary>Selected units in the order they were selected.</summary>
    public IReadOnlyList<UnitKey> Units => _units;

    /// <summary>Number of selected units.</summary>
    public int Count => _units.Count;

    /// <summary>True when the unit is selected.</summary>
    public bool Contains(UnitKey key) => _units.Contains(key);

    /// <summary>Removes everything from the selection.</summary>
    public void Clear() => _units.Clear();

    /// <summary>Replaces the selection with the given units.</summary>
    public void Replace(IEnumerable<UnitKey> keys)
    {
        List<UnitKey> copy = keys.ToList(); // copy first: the source may be this selection's own list
        _units.Clear();
        _units.AddRange(copy);
    }

    /// <summary>Drops every selected unit for which <paramref name="isAlive"/> is false.</summary>
    public void Prune(Func<UnitKey, bool> isAlive) => _units.RemoveAll(key => !isAlive(key));

    /// <summary>
    /// The unit of <paramref name="localPlayer"/> under a world point: the nearest one within <see cref="PickRadiusPx"/>,
    /// or any such unit in the clicked cell. Null when nothing is hit.
    /// </summary>
    public static UnitKey? Pick(IReadOnlyList<RenderUnit> units, Vector2 worldPoint, byte localPlayer)
    {
        CellCoord clicked = Camera.CellAt(worldPoint);
        UnitKey? best = null;
        float bestDistSq = float.MaxValue;
        foreach (RenderUnit unit in units)
        {
            if (unit.View.Player != localPlayer) continue;
            float distSq = Vector2.DistanceSquared(unit.World, worldPoint);
            bool sameCell = Camera.CellAt(unit.World) == clicked;
            if (!sameCell && distSq > PickRadiusPx * PickRadiusPx) continue;
            if (distSq < bestDistSq)
            {
                bestDistSq = distSq;
                best = UnitKey.Of(unit.View);
            }
        }
        return best;
    }

    /// <summary>Units of <paramref name="localPlayer"/> whose world position lies inside the box [min, max].</summary>
    public static IEnumerable<UnitKey> Inside(IReadOnlyList<RenderUnit> units, Vector2 min, Vector2 max, byte localPlayer)
    {
        foreach (RenderUnit unit in units)
        {
            if (unit.View.Player != localPlayer) continue;
            Vector2 p = unit.World;
            if (p.X >= min.X && p.X <= max.X && p.Y >= min.Y && p.Y <= max.Y)
            {
                yield return UnitKey.Of(unit.View);
            }
        }
    }
}
