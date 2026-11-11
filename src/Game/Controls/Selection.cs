// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Microsoft.Xna.Framework;
using Rts.Game.Render;
using Rts.Game.Sandbox;
using Rts.Sim.Core;
using Rts.Sim.Rules;

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

    /// <summary>Removes one unit from the selection (shift-click on its cameo in the selection card). False when it was not selected.</summary>
    public bool Remove(UnitKey key) => _units.Remove(key);

    /// <summary>Replaces the selection with the given units.</summary>
    public void Replace(IEnumerable<UnitKey> keys)
    {
        List<UnitKey> copy = keys.ToList(); // copy first: the source may be this selection's own list
        _units.Clear();
        _units.AddRange(copy);
    }

    /// <summary>Adds units that are not selected yet, keeping the order (Shift+box, Shift+double-click, control-group add). Returns how many were added.</summary>
    public int AddRange(IEnumerable<UnitKey> keys)
    {
        int added = 0;
        foreach (UnitKey key in keys.ToList())
        {
            if (_units.Contains(key)) continue;
            _units.Add(key);
            added++;
        }

        return added;
    }

    /// <summary>Shift+click on a unit: removes it when selected, adds it otherwise. Returns true when it is selected afterwards.</summary>
    public bool Toggle(UnitKey key)
    {
        if (_units.Remove(key)) return false;
        _units.Add(key);
        return true;
    }

    /// <summary>
    /// Applies a pick or a box result: with <paramref name="additive"/> (Shift held) the units join the selection, otherwise they replace it.
    /// </summary>
    public void Apply(IEnumerable<UnitKey> keys, bool additive)
    {
        if (additive) AddRange(keys);
        else Replace(keys);
    }

    /// <summary>Drops every selected unit for which <paramref name="isAlive"/> is false.</summary>
    public void Prune(Func<UnitKey, bool> isAlive) => _units.RemoveAll(key => !isAlive(key));

    /// <summary>
    /// S19 D-205: drops every selected unit that the snapshot lists as carried (a soldier ordered to Enter a building leaves the selection
    /// once it is inside; it cannot be ordered until its carrier unloads it).
    /// </summary>
    public void DropCarried(IReadOnlyList<RenderUnit> units) => _units.RemoveAll(key => IsCarried(units, key));

    /// <summary>S19 D-205: <paramref name="keys"/> without the units the snapshot lists as carried (control-group recall keeps the membership but selects only free units).</summary>
    public static List<UnitKey> WithoutCarried(IEnumerable<UnitKey> keys, IReadOnlyList<RenderUnit> units) => keys.Where(key => !IsCarried(units, key)).ToList();

    private static bool IsCarried(IReadOnlyList<RenderUnit> units, UnitKey key)
    {
        foreach (RenderUnit unit in units)
        {
            if (unit.View.Carried && UnitKey.Of(unit.View) == key) return true;
        }

        return false;
    }

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
            if (unit.View.Player != localPlayer || unit.View.Carried) continue;   // S19 D-205: a click on a garrisoned building picks the building
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
            if (unit.View.Player != localPlayer || unit.View.Carried) continue;   // S19 D-205
            Vector2 p = unit.World;
            if (p.X >= min.X && p.X <= max.X && p.Y >= min.Y && p.Y <= max.Y)
            {
                yield return UnitKey.Of(unit.View);
            }
        }
    }

    /// <summary>
    /// Every unit of <paramref name="localPlayer"/> inside the world box [min, max] that has the same kind and type as <paramref name="like"/>
    /// (double-click or Ctrl+click: "all of this type on screen").
    /// </summary>
    public static IEnumerable<UnitKey> SameTypeInside(IReadOnlyList<RenderUnit> units, Vector2 min, Vector2 max, byte localPlayer, UnitKey like)
    {
        UnitView? model = null;
        foreach (RenderUnit unit in units)
        {
            if (UnitKey.Of(unit.View) == like) { model = unit.View; break; }
        }

        if (model is not UnitView m) yield break;
        foreach (RenderUnit unit in units)
        {
            UnitView v = unit.View;
            if (v.Player != localPlayer || v.Carried || v.Kind != m.Kind || v.TypeIndex != m.TypeIndex) continue;   // S19 D-205
            Vector2 p = unit.World;
            if (p.X >= min.X && p.X <= max.X && p.Y >= min.Y && p.Y <= max.Y) yield return UnitKey.Of(v);
        }
    }

    /// <summary>
    /// The select-all-army set: every unit of <paramref name="localPlayer"/> for which <paramref name="isCombat"/> is true, anywhere on the map
    /// (harvesters, builders and other non-fighters are the caller's <paramref name="isCombat"/> to leave out).
    /// </summary>
    public static IEnumerable<UnitKey> Army(IReadOnlyList<RenderUnit> units, byte localPlayer, Func<UnitView, bool> isCombat)
    {
        foreach (RenderUnit unit in units)
        {
            if (unit.View.Player == localPlayer && !unit.View.Carried && isCombat(unit.View)) yield return UnitKey.Of(unit.View);
        }
    }

    /// <summary>
    /// True for a unit that fights: it has a primary weapon and is not a harvester, a deployable builder (MCV type), a salvager or an engineer.
    /// </summary>
    public static bool IsCombatUnit(SimRules rules, UnitView unit)
    {
        if (unit.Kind == UnitKind.Vehicle)
        {
            if ((uint)unit.TypeIndex >= (uint)rules.Vehicles.Count) return false;
            VehicleType v = rules.Vehicles[unit.TypeIndex];
            return v.PrimaryWeapon >= 0 && v.HarvestCapacity <= 0 && v.DeploysInto < 0 && v.SalvageRate <= 0;
        }

        if ((uint)unit.TypeIndex >= (uint)rules.Infantry.Count) return false;
        InfantryType i = rules.Infantry[unit.TypeIndex];
        return i.PrimaryWeapon >= 0 && !i.Engineer && i.SalvageRate <= 0;
    }
}
