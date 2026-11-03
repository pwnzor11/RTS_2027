// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Sim.Core;
using Rts.Sim.Numerics;

namespace Rts.Sim.Tests.MathTests;

public class LeptonMathTests
{
    [Fact]
    public void CellConversions()
    {
        Assert.Equal(new LeptonCoord(512, 768), LeptonMath.CellToLepton(new CellCoord(2, 3)));
        Assert.Equal(new LeptonCoord(640, 896), LeptonMath.CellCenter(new CellCoord(2, 3)));
    }

    [Theory]
    [InlineData(0UL, 0u)]
    [InlineData(1UL, 1u)]
    [InlineData(2UL, 1u)]
    [InlineData(3UL, 1u)]
    [InlineData(4UL, 2u)]
    [InlineData(15UL, 3u)]
    [InlineData(16UL, 4u)]
    [InlineData(4294967295UL, 65535u)]
    [InlineData(4294967296UL, 65536u)]
    [InlineData(1UL << 63, 3037000499u)]
    [InlineData(ulong.MaxValue, 4294967295u)]
    public void ISqrtEdges(ulong n, uint expected) => Assert.Equal(expected, LeptonMath.ISqrt(n));

    [Fact]
    public void ISqrtIsExactFloorAroundSquares()
    {
        foreach (uint r in new uint[] { 2, 255, 256, 65535, 65536, 1000003, 4294967295 })
        {
            ulong sq = (ulong)r * r;
            Assert.Equal(r, LeptonMath.ISqrt(sq));
            Assert.Equal(r - 1, LeptonMath.ISqrt(sq - 1));
            if (r != 4294967295) Assert.Equal(r, LeptonMath.ISqrt(sq + 1));
        }
    }

    [Fact]
    public void DistanceSquaredAndDistance()
    {
        var a = new LeptonCoord(0, 0);
        var b = new LeptonCoord(3, -4);
        Assert.Equal(25L, LeptonMath.DistanceSquared(a, b));
        Assert.Equal(5, LeptonMath.Distance(a, b));
        Assert.Equal(1, LeptonMath.Distance(a, new LeptonCoord(1, 1)));
        Assert.Equal(0, LeptonMath.Distance(a, a));
    }

    [Fact]
    public void Chebyshev()
    {
        Assert.Equal(5, LeptonMath.ChebyshevCells(new CellCoord(1, 1), new CellCoord(-4, 3)));
        Assert.Equal(0, LeptonMath.ChebyshevCells(new CellCoord(2, 2), new CellCoord(2, 2)));
    }

    [Fact]
    public void OffsetCardinal()
    {
        var o = new LeptonCoord(1000, 1000);
        Assert.Equal(new LeptonCoord(1000, 900), LeptonMath.Offset(o, 0, 100));
        Assert.Equal(new LeptonCoord(1100, 1000), LeptonMath.Offset(o, 64, 100));
        Assert.Equal(new LeptonCoord(1000, 1100), LeptonMath.Offset(o, 128, 100));
        Assert.Equal(new LeptonCoord(900, 1000), LeptonMath.Offset(o, 192, 100));
        Assert.Equal(o, LeptonMath.Offset(o, 37, 0));
    }

    [Fact]
    public void OffsetDiagonal()
    {
        var r = LeptonMath.Offset(new LeptonCoord(0, 0), 32, 256);
        Assert.Equal(181, r.X);
        Assert.Equal(-181, r.Y);
    }
}
