// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Game.Sandbox;
using Rts.Sim.Core;

namespace Rts.Game.Controls;

/// <summary>
/// A43-QOL-16: the maths of a line-drag move. A right-button drag from cell A to cell B spreads the selected units evenly along the
/// line; each unit then gets an ordinary Move (or queued waypoint) to its own cell. Pure integer maths; the front end only decides where
/// the cells are, the sim still validates every order.
/// </summary>
public static class LineFormation
{
    /// <summary>A drag shorter than this many cells (Chebyshev) is a plain right-click, not a line.</summary>
    public const int MinLineCells = 2;

    /// <summary>True when the drag from <paramref name="a"/> to <paramref name="b"/> is long enough to be a line.</summary>
    public static bool IsLine(CellCoord a, CellCoord b) => Math.Max(Math.Abs(b.X - a.X), Math.Abs(b.Y - a.Y)) >= MinLineCells;

    /// <summary>
    /// <paramref name="count"/> cells spread evenly from <paramref name="a"/> to <paramref name="b"/> (both ends included; one unit takes the
    /// middle). Neighbouring slots can share a cell when the line is shorter than the group is large.
    /// </summary>
    public static List<CellCoord> Slots(CellCoord a, CellCoord b, int count)
    {
        var list = new List<CellCoord>(Math.Max(0, count));
        if (count <= 0) return list;
        if (count == 1)
        {
            list.Add(new CellCoord((a.X + b.X) / 2, (a.Y + b.Y) / 2));
            return list;
        }

        int dx = b.X - a.X, dy = b.Y - a.Y, den = count - 1;
        for (int i = 0; i < count; i++) list.Add(new CellCoord(a.X + Step(dx, i, den), a.Y + Step(dy, i, den)));
        return list;
    }

    // round(d * i / den), half away from zero, integers only
    private static int Step(int d, int i, int den)
    {
        long num = (long)d * i * 2 + Math.Sign((long)d * i) * den;
        return (int)(num / (2L * den));
    }

    /// <summary>
    /// Gives each unit one slot of the line: units are ordered by where they stand along the drag direction, so the group does not cross over
    /// itself. Ties break by kind and id, so the result never depends on input order.
    /// </summary>
    public static List<(UnitKey Unit, CellCoord Cell)> Assign(IReadOnlyList<(UnitKey Unit, LeptonCoord At)> units, CellCoord a, CellCoord b)
    {
        List<CellCoord> slots = Slots(a, b, units.Count);
        long dx = b.X - a.X, dy = b.Y - a.Y;
        var ordered = units.OrderBy(u => (long)u.At.X * dx + (long)u.At.Y * dy)
            .ThenBy(u => u.Unit.Kind).ThenBy(u => u.Unit.Id.Index).ThenBy(u => u.Unit.Id.Generation).ToList();
        var result = new List<(UnitKey, CellCoord)>(ordered.Count);
        for (int i = 0; i < ordered.Count; i++) result.Add((ordered[i].Unit, slots[i]));
        return result;
    }
}
