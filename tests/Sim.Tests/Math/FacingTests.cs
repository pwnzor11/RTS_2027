// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Sim.Core;
using Rts.Sim.Numerics;

namespace Rts.Sim.Tests.MathTests;

public class FacingTests
{
    [Fact]
    public void SinCosKeyPoints()
    {
        Assert.Equal(0, Facing.Sin(0));
        Assert.Equal(256, Facing.Cos(0));
        Assert.Equal(256, Facing.Sin(64));
        Assert.Equal(0, Facing.Cos(64));
        Assert.Equal(-256, Facing.Cos(128));
        Assert.Equal(-256, Facing.Sin(192));
    }

    [Fact]
    public void SinCosMatchReferenceWithinOne()
    {
        for (int i = 0; i < 256; i++)
        {
            double a = 2 * System.Math.PI * i / 256;
            Assert.InRange(Facing.Sin((byte)i), (int)System.Math.Round(256 * System.Math.Sin(a)) - 1, (int)System.Math.Round(256 * System.Math.Sin(a)) + 1);
            Assert.InRange(Facing.Cos((byte)i), (int)System.Math.Round(256 * System.Math.Cos(a)) - 1, (int)System.Math.Round(256 * System.Math.Cos(a)) + 1);
        }
    }

    [Theory]
    [InlineData(0, -10, 0)]
    [InlineData(10, -10, 32)]
    [InlineData(10, 0, 64)]
    [InlineData(10, 10, 96)]
    [InlineData(0, 10, 128)]
    [InlineData(-10, 10, 160)]
    [InlineData(-10, 0, 192)]
    [InlineData(-10, -10, 224)]
    public void FromDeltaCompass(int dx, int dy, int expected) => Assert.Equal(expected, Facing.FromDelta(dx, dy));

    [Fact]
    public void FromDeltaZero() => Assert.Equal(0, Facing.FromDelta(0, 0));

    private static int Expected(int dx, int dy)
    {
        double turns = System.Math.Atan2(dx, -dy) / (2 * System.Math.PI) * 256;
        return ((int)System.Math.Round(turns) % 256 + 256) % 256;
    }

    private static int CircDiff(int a, int b)
    {
        int d = ((a - b) % 256 + 256) % 256;
        return d > 128 ? 256 - d : d;
    }

    [Fact]
    public void FromDeltaSeededRandomWithinOne()
    {
        var rng = new SimRandom(1);
        for (int i = 0; i < 1000; i++)
        {
            int dx = rng.Next(4001) - 2000;
            int dy = rng.Next(4001) - 2000;
            if (dx == 0 && dy == 0) continue;
            int got = Facing.FromDelta(dx, dy);
            Assert.True(CircDiff(got, Expected(dx, dy)) <= 1, $"({dx},{dy}) got {got} expected {Expected(dx, dy)}");
        }
    }

    [Fact]
    public void FromDeltaRoundTripsWithOffset()
    {
        for (int f = 0; f < 256; f++)
        {
            var p = LeptonMath.Offset(new LeptonCoord(0, 0), (byte)f, 4000);
            Assert.True(CircDiff(Facing.FromDelta(p.X, p.Y), f) <= 1, $"facing {f}");
        }
    }

    [Theory]
    [InlineData(0, 8, 0)]
    [InlineData(15, 8, 0)]
    [InlineData(16, 8, 1)]
    [InlineData(32, 8, 1)]
    [InlineData(47, 8, 1)]
    [InlineData(48, 8, 2)]
    [InlineData(239, 8, 7)]
    [InlineData(240, 8, 0)]
    [InlineData(255, 8, 0)]
    [InlineData(7, 16, 0)]
    [InlineData(8, 16, 1)]
    [InlineData(248, 16, 0)]
    [InlineData(247, 16, 15)]
    [InlineData(3, 32, 0)]
    [InlineData(4, 32, 1)]
    [InlineData(252, 32, 0)]
    [InlineData(251, 32, 31)]
    public void ToFacingsBoundaries(int facing, int count, int frame) =>
        Assert.Equal(frame, Facing.ToFacings((byte)facing, count));

    [Fact]
    public void ToFacingsRejectsBadCount() =>
        Assert.Throws<ArgumentOutOfRangeException>(() => Facing.ToFacings(0, 12));

    [Theory]
    [InlineData(0, 0, 0)]
    [InlineData(0, 10, 10)]
    [InlineData(10, 0, -10)]
    [InlineData(250, 5, 11)]
    [InlineData(5, 250, -11)]
    [InlineData(0, 128, -128)]
    [InlineData(128, 0, -128)]
    [InlineData(0, 127, 127)]
    public void DifferenceSigned(int a, int b, int expected) => Assert.Equal(expected, Facing.Difference((byte)a, (byte)b));

    [Theory]
    [InlineData(250, 10, 4, 254)]   // wraps clockwise through 0
    [InlineData(250, 10, 100, 10)]  // within rate: snaps to target
    [InlineData(5, 250, 4, 1)]      // wraps counter-clockwise
    [InlineData(5, 250, 6, 255)]
    [InlineData(10, 100, 5, 15)]
    [InlineData(100, 10, 5, 95)]
    [InlineData(10, 10, 5, 10)]
    [InlineData(10, 100, 0, 10)]
    [InlineData(10, 100, -3, 10)]
    public void TurnTowardWraps(int cur, int target, int rate, int expected) =>
        Assert.Equal(expected, Facing.TurnToward((byte)cur, (byte)target, rate));
}
