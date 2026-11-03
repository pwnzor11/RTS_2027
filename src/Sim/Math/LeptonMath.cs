using Rts.Sim.Core;

namespace Rts.Sim.Math;

/// <summary>Integer helpers for lepton / cell coordinates.</summary>
public static class LeptonMath
{
    /// <summary>Top-left lepton of a cell.</summary>
    public static LeptonCoord CellToLepton(CellCoord c) =>
        new(c.X << SimConstants.LeptonShift, c.Y << SimConstants.LeptonShift);

    /// <summary>Centre lepton of a cell.</summary>
    public static LeptonCoord CellCenter(CellCoord c) => LeptonCoord.CenterOf(c);

    /// <summary>Squared distance in leptons (exact for coordinates within +-2^30).</summary>
    public static long DistanceSquared(LeptonCoord a, LeptonCoord b)
    {
        long dx = (long)a.X - b.X;
        long dy = (long)a.Y - b.Y;
        return dx * dx + dy * dy;
    }

    /// <summary>Distance in leptons, floor of the true Euclidean distance.</summary>
    public static int Distance(LeptonCoord a, LeptonCoord b) => (int)ISqrt((ulong)DistanceSquared(a, b));

    /// <summary>Exact floor of the square root.</summary>
    public static uint ISqrt(ulong n)
    {
        ulong res = 0;
        ulong bit = 1UL << 62;
        while (bit > n) bit >>= 2;
        while (bit != 0)
        {
            if (n >= res + bit)
            {
                n -= res + bit;
                res = (res >> 1) + bit;
            }
            else
            {
                res >>= 1;
            }
            bit >>= 2;
        }
        return (uint)res;
    }

    /// <summary>Chebyshev (king-move) distance between cells.</summary>
    public static int ChebyshevCells(CellCoord a, CellCoord b)
    {
        int dx = a.X - b.X; if (dx < 0) dx = -dx;
        int dy = a.Y - b.Y; if (dy < 0) dy = -dy;
        return dx > dy ? dx : dy;
    }

    /// <summary>
    /// Point <paramref name="distance"/> leptons away along <paramref name="facing"/> (0 = north, -Y).
    /// Each axis is rounded to nearest (halves round up).
    /// </summary>
    public static LeptonCoord Offset(LeptonCoord from, byte facing, int distance)
    {
        long ox = ((long)Facing.Sin(facing) * distance + 128) >> 8;
        long oy = ((long)Facing.Cos(facing) * distance + 128) >> 8;
        return new LeptonCoord(from.X + (int)ox, from.Y - (int)oy);
    }
}
