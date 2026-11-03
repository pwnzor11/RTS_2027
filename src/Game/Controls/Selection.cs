using Microsoft.Xna.Framework;
using Rts.Sim.Core;

namespace Rts.Game.Controls;

/// <summary>
/// The set of units the local player has selected, by id. Ids are generational, so a selected unit that dies
/// is dropped by <see cref="Prune"/> and never confused with a unit later spawned in the same slot.
/// </summary>
public sealed class Selection
{
    /// <summary>Pick radius around a unit, in zoom-1 world pixels. A click inside the same cell also hits.</summary>
    public const float PickRadiusPx = 32f;

    private readonly List<EntityId> _ids = new();

    /// <summary>Selected ids in the order they were selected.</summary>
    public IReadOnlyList<EntityId> Ids => _ids;

    /// <summary>Number of selected units.</summary>
    public int Count => _ids.Count;

    /// <summary>True when the unit is selected.</summary>
    public bool Contains(EntityId id) => _ids.Contains(id);

    /// <summary>Removes everything from the selection.</summary>
    public void Clear() => _ids.Clear();

    /// <summary>Replaces the selection with the given ids.</summary>
    public void Replace(IEnumerable<EntityId> ids)
    {
        List<EntityId> copy = ids.ToList(); // copy first: the source may be this selection's own list
        _ids.Clear();
        _ids.AddRange(copy);
    }

    /// <summary>Drops every selected id that is not in <paramref name="alive"/>.</summary>
    public void Prune(IReadOnlyList<UnitView> alive)
    {
        if (_ids.Count == 0) return;
        var living = new HashSet<EntityId>();
        foreach (UnitView unit in alive) living.Add(unit.Id);
        _ids.RemoveAll(id => !living.Contains(id));
    }

    /// <summary>
    /// The owned unit under a world point: the nearest one within <see cref="PickRadiusPx"/>, or any owned unit in
    /// the clicked cell. Returns <see cref="EntityId.None"/> when nothing is hit.
    /// </summary>
    public static EntityId Pick(IReadOnlyList<RenderUnit> units, Vector2 worldPoint, byte localPlayer)
    {
        int cellX = CellOf(worldPoint.X);
        int cellY = CellOf(worldPoint.Y);
        EntityId best = EntityId.None;
        float bestDistSq = float.MaxValue;
        foreach (RenderUnit unit in units)
        {
            if (unit.View.Player != localPlayer) continue;
            float distSq = Vector2.DistanceSquared(unit.World, worldPoint);
            bool sameCell = CellOf(unit.World.X) == cellX && CellOf(unit.World.Y) == cellY;
            if (!sameCell && distSq > PickRadiusPx * PickRadiusPx) continue;
            if (distSq < bestDistSq)
            {
                bestDistSq = distSq;
                best = unit.View.Id;
            }
        }
        return best;
    }

    /// <summary>Owned units whose world position lies inside the box [min, max].</summary>
    public static IEnumerable<EntityId> Inside(IReadOnlyList<RenderUnit> units, Vector2 min, Vector2 max, byte localPlayer)
    {
        foreach (RenderUnit unit in units)
        {
            if (unit.View.Player != localPlayer) continue;
            Vector2 p = unit.World;
            if (p.X >= min.X && p.X <= max.X && p.Y >= min.Y && p.Y <= max.Y)
            {
                yield return unit.View.Id;
            }
        }
    }

    private static int CellOf(float worldPx) => (int)MathF.Floor(worldPx / GridRenderer.CellPx);
}
