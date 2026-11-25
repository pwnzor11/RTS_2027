// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Microsoft.Xna.Framework;
using Rts.Game.Sandbox;
using Rts.Sim.Core;

namespace Rts.Game.Render;

/// <summary>Builds the units to draw, placed between the previous and current snapshot.</summary>
public static class UnitInterpolator
{
    [ThreadStatic]
    private static Dictionary<UnitKey, UnitView>? Before;   // scratch map, one per thread

    // What Before currently holds: the previous snapshot's unit list, or null when it is empty. The map only depends on that list,
    // not on alpha, so it is refilled when the previous snapshot changes (once per tick), not once per frame.
    [ThreadStatic]
    private static IReadOnlyList<UnitView>? _filledFrom;
    [ThreadStatic]
    private static int _filledCount;
    [ThreadStatic]
    private static bool _filledValid;

    /// <summary>
    /// Fills <paramref name="output"/> with every unit of <paramref name="current"/> at its position between
    /// <paramref name="previous"/> and <paramref name="current"/>. Units are matched by kind and id; a unit with no
    /// match (newly spawned) is drawn at its current position.
    /// </summary>
    /// <param name="alpha">0 shows the previous positions, 1 the current ones.</param>
    public static void Build(SimSnapshot current, SimSnapshot previous, float alpha, List<RenderUnit> output)
    {
        output.Clear();
        Dictionary<UnitKey, UnitView> before = Before ??= new Dictionary<UnitKey, UnitView>();   // reused: no dictionary per frame
        IReadOnlyList<UnitView>? source = ReferenceEquals(current, previous) ? null : previous.Units;
        if (!_filledValid || !ReferenceEquals(source, _filledFrom) || (source?.Count ?? 0) != _filledCount)
        {
            before.Clear();
            if (source is not null)
            {
                for (int i = 0; i < source.Count; i++) before[UnitKey.Of(source[i])] = source[i];
            }

            _filledFrom = source;
            _filledCount = source?.Count ?? 0;
            _filledValid = true;
        }

        IReadOnlyList<UnitView> units = current.Units;
        for (int i = 0; i < units.Count; i++)
        {
            UnitView unit = units[i];
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
