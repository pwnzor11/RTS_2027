using Rts.Sim.Core;

namespace Rts.Sim.Movement;

/// <summary>Deterministic square spiral of cells around a centre, used to spread a group over distinct destinations.</summary>
internal static class Spiral
{
    /// <summary>Largest ring searched around the target.</summary>
    public const int MaxRing = 24;

    /// <summary>
    /// Cell number <paramref name="n"/> of the spiral around <paramref name="c"/>: n = 0 is the centre, then ring 1
    /// (top row west to east, right column north to south, bottom row east to west, left column south to north), ring 2, ...
    /// Returns false when n lies beyond <see cref="MaxRing"/>.
    /// </summary>
    public static bool Cell(CellCoord c, int n, out CellCoord result)
    {
        result = c;
        if (n == 0) return true;
        int r = 1, before = 1;                 // ring r holds 8r cells
        while (n >= before + 8 * r) { before += 8 * r; r++; if (r > MaxRing) return false; }
        int k = n - before, side = 2 * r;
        if (k < side) result = new CellCoord(c.X - r + k, c.Y - r);
        else if (k < 2 * side) result = new CellCoord(c.X + r, c.Y - r + (k - side));
        else if (k < 3 * side) result = new CellCoord(c.X + r - (k - 2 * side), c.Y + r);
        else result = new CellCoord(c.X - r, c.Y + r - (k - 3 * side));
        return true;
    }
}
