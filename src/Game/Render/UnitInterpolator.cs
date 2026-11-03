using Microsoft.Xna.Framework;
using Rts.Game.Sandbox;
using Rts.Sim.Core;

namespace Rts.Game.Render;

/// <summary>Builds the units to draw, placed between the previous and current snapshot.</summary>
public static class UnitInterpolator
{
    /// <summary>
    /// Fills <paramref name="output"/> with every unit of <paramref name="current"/> at its position between
    /// <paramref name="previous"/> and <paramref name="current"/>. Units are matched by kind and id; a unit with no
    /// match (newly spawned) is drawn at its current position.
    /// </summary>
    /// <param name="alpha">0 shows the previous positions, 1 the current ones.</param>
    public static void Build(SimSnapshot current, SimSnapshot previous, float alpha, List<RenderUnit> output)
    {
        output.Clear();
        var before = new Dictionary<UnitKey, UnitView>();
        if (!ReferenceEquals(current, previous))
        {
            foreach (UnitView unit in previous.Units) before[UnitKey.Of(unit)] = unit;
        }

        foreach (UnitView unit in current.Units)
        {
            Vector2 now = Camera.WorldFromLepton(unit.Position);
            Vector2 at = now;
            if (before.TryGetValue(UnitKey.Of(unit), out UnitView old))
            {
                at = Vector2.Lerp(Camera.WorldFromLepton(old.Position), now, alpha);
            }
            output.Add(new RenderUnit(unit, at));
        }
    }
}
