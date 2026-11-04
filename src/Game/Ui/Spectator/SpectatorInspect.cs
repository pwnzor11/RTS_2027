// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Microsoft.Xna.Framework;
using Rts.Game.Controls;
using Rts.Game.Render;
using Rts.Sim.Core;
using Rts.Sim.Rules;

namespace Rts.Game.Ui.Spectator;

/// <summary>Read-only "click a unit to see its info" for the spectator (design 08 §2). Picks any player's unit or structure; nothing it
/// returns can be ordered.</summary>
public static class SpectatorInspect
{
    /// <summary>The unit of any player nearest to a world point, within <see cref="Selection.PickRadiusPx"/> (or in the clicked cell).</summary>
    public static UnitView? PickUnit(IReadOnlyList<RenderUnit> units, Vector2 worldPoint)
    {
        CellCoord clicked = Camera.CellAt(worldPoint);
        UnitView? best = null;
        float bestDistSq = float.MaxValue;
        foreach (RenderUnit unit in units)
        {
            float distSq = Vector2.DistanceSquared(unit.World, worldPoint);
            if (Camera.CellAt(unit.World) != clicked && distSq > Selection.PickRadiusPx * Selection.PickRadiusPx) continue;
            if (distSq < bestDistSq)
            {
                bestDistSq = distSq;
                best = unit.View;
            }
        }

        return best;
    }

    /// <summary>The structure of any player whose footprint contains <paramref name="cell"/> (ghosts included: it is what the viewer knows).</summary>
    public static BuildingView? PickBuilding(IReadOnlyList<BuildingView> buildings, CellCoord cell)
    {
        foreach (BuildingView b in buildings)
            if (cell.X >= b.Cell.X && cell.X < b.Cell.X + b.FootprintW && cell.Y >= b.Cell.Y && cell.Y < b.Cell.Y + b.FootprintH) return b;
        return null;
    }

    /// <summary>Info lines for a unit.</summary>
    public static IReadOnlyList<string> Describe(UnitView u, SimRules rules)
    {
        (string id, int max) = u.Kind == UnitKind.Vehicle
            ? (rules.Vehicles[u.TypeIndex].Id, rules.Vehicles[u.TypeIndex].MaxHealth)
            : (rules.Infantry[u.TypeIndex].Id, rules.Infantry[u.TypeIndex].MaxHealth);
        return new[] { id, $"P{u.Player + 1}  health {u.Health}/{max}" };
    }

    /// <summary>Info lines for a structure.</summary>
    public static IReadOnlyList<string> Describe(BuildingView b, SimRules rules)
    {
        BuildingType t = rules.Buildings[b.TypeIndex];
        var lines = new List<string> { t.Id, $"P{b.Player + 1}  health {b.Health}/{t.MaxHealth}" + (b.Ghost ? "  (last seen)" : "") };
        if (b.Occupants > 0) lines.Add($"garrison {b.Occupants}");
        return lines;
    }
}
