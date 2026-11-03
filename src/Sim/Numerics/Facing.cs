// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
namespace Rts.Sim.Numerics;

/// <summary>
/// 8-bit facing helpers. Convention: 0 = north (-Y), 64 = east (+X), 128 = south, 192 = west;
/// angles increase clockwise. Integer math and lookup tables only.
/// </summary>
public static class Facing
{
    // Generated ONCE at authoring time with python3:
    //   Sin[i] = round(256 * sin(2*pi*i/256)),  Cos[i] = round(256 * cos(2*pi*i/256)),  i in 0..255
    //   AtanTable[i] = round(atan(i/256) * 256 / (2*pi)),  i in 0..256   (0..32 facing steps)
    // No trigonometry is evaluated at runtime.
    private static ReadOnlySpan<short> SinTable => new short[]
    {
        0, 6, 13, 19, 25, 31, 38, 44, 50, 56, 62, 68, 74, 80, 86, 92,
        98, 104, 109, 115, 121, 126, 132, 137, 142, 147, 152, 157, 162, 167, 172, 177,
        181, 185, 190, 194, 198, 202, 206, 209, 213, 216, 220, 223, 226, 229, 231, 234,
        237, 239, 241, 243, 245, 247, 248, 250, 251, 252, 253, 254, 255, 255, 256, 256,
        256, 256, 256, 255, 255, 254, 253, 252, 251, 250, 248, 247, 245, 243, 241, 239,
        237, 234, 231, 229, 226, 223, 220, 216, 213, 209, 206, 202, 198, 194, 190, 185,
        181, 177, 172, 167, 162, 157, 152, 147, 142, 137, 132, 126, 121, 115, 109, 104,
        98, 92, 86, 80, 74, 68, 62, 56, 50, 44, 38, 31, 25, 19, 13, 6,
        0, -6, -13, -19, -25, -31, -38, -44, -50, -56, -62, -68, -74, -80, -86, -92,
        -98, -104, -109, -115, -121, -126, -132, -137, -142, -147, -152, -157, -162, -167, -172, -177,
        -181, -185, -190, -194, -198, -202, -206, -209, -213, -216, -220, -223, -226, -229, -231, -234,
        -237, -239, -241, -243, -245, -247, -248, -250, -251, -252, -253, -254, -255, -255, -256, -256,
        -256, -256, -256, -255, -255, -254, -253, -252, -251, -250, -248, -247, -245, -243, -241, -239,
        -237, -234, -231, -229, -226, -223, -220, -216, -213, -209, -206, -202, -198, -194, -190, -185,
        -181, -177, -172, -167, -162, -157, -152, -147, -142, -137, -132, -126, -121, -115, -109, -104,
        -98, -92, -86, -80, -74, -68, -62, -56, -50, -44, -38, -31, -25, -19, -13, -6,
    };

    private static ReadOnlySpan<short> CosTable => new short[]
    {
        256, 256, 256, 255, 255, 254, 253, 252, 251, 250, 248, 247, 245, 243, 241, 239,
        237, 234, 231, 229, 226, 223, 220, 216, 213, 209, 206, 202, 198, 194, 190, 185,
        181, 177, 172, 167, 162, 157, 152, 147, 142, 137, 132, 126, 121, 115, 109, 104,
        98, 92, 86, 80, 74, 68, 62, 56, 50, 44, 38, 31, 25, 19, 13, 6,
        0, -6, -13, -19, -25, -31, -38, -44, -50, -56, -62, -68, -74, -80, -86, -92,
        -98, -104, -109, -115, -121, -126, -132, -137, -142, -147, -152, -157, -162, -167, -172, -177,
        -181, -185, -190, -194, -198, -202, -206, -209, -213, -216, -220, -223, -226, -229, -231, -234,
        -237, -239, -241, -243, -245, -247, -248, -250, -251, -252, -253, -254, -255, -255, -256, -256,
        -256, -256, -256, -255, -255, -254, -253, -252, -251, -250, -248, -247, -245, -243, -241, -239,
        -237, -234, -231, -229, -226, -223, -220, -216, -213, -209, -206, -202, -198, -194, -190, -185,
        -181, -177, -172, -167, -162, -157, -152, -147, -142, -137, -132, -126, -121, -115, -109, -104,
        -98, -92, -86, -80, -74, -68, -62, -56, -50, -44, -38, -31, -25, -19, -13, -6,
        0, 6, 13, 19, 25, 31, 38, 44, 50, 56, 62, 68, 74, 80, 86, 92,
        98, 104, 109, 115, 121, 126, 132, 137, 142, 147, 152, 157, 162, 167, 172, 177,
        181, 185, 190, 194, 198, 202, 206, 209, 213, 216, 220, 223, 226, 229, 231, 234,
        237, 239, 241, 243, 245, 247, 248, 250, 251, 252, 253, 254, 255, 255, 256, 256,
    };

    private static ReadOnlySpan<byte> AtanTable => new byte[]
    {
        0, 0, 0, 0, 1, 1, 1, 1, 1, 1, 2, 2, 2, 2, 2, 2,
        3, 3, 3, 3, 3, 3, 3, 4, 4, 4, 4, 4, 4, 5, 5, 5,
        5, 5, 5, 6, 6, 6, 6, 6, 6, 6, 7, 7, 7, 7, 7, 7,
        8, 8, 8, 8, 8, 8, 8, 9, 9, 9, 9, 9, 9, 10, 10, 10,
        10, 10, 10, 10, 11, 11, 11, 11, 11, 11, 11, 12, 12, 12, 12, 12,
        12, 12, 13, 13, 13, 13, 13, 13, 13, 14, 14, 14, 14, 14, 14, 14,
        15, 15, 15, 15, 15, 15, 15, 16, 16, 16, 16, 16, 16, 16, 17, 17,
        17, 17, 17, 17, 17, 17, 18, 18, 18, 18, 18, 18, 18, 19, 19, 19,
        19, 19, 19, 19, 19, 20, 20, 20, 20, 20, 20, 20, 20, 21, 21, 21,
        21, 21, 21, 21, 21, 21, 22, 22, 22, 22, 22, 22, 22, 22, 23, 23,
        23, 23, 23, 23, 23, 23, 23, 24, 24, 24, 24, 24, 24, 24, 24, 24,
        25, 25, 25, 25, 25, 25, 25, 25, 25, 25, 26, 26, 26, 26, 26, 26,
        26, 26, 26, 27, 27, 27, 27, 27, 27, 27, 27, 27, 27, 28, 28, 28,
        28, 28, 28, 28, 28, 28, 28, 28, 29, 29, 29, 29, 29, 29, 29, 29,
        29, 29, 29, 30, 30, 30, 30, 30, 30, 30, 30, 30, 30, 30, 31, 31,
        31, 31, 31, 31, 31, 31, 31, 31, 31, 31, 32, 32, 32, 32, 32, 32,
        32,
    };

    /// <summary>sin of the facing angle scaled by 256 (range -256..256). East (64) = +256.</summary>
    public static int Sin(byte facing) => SinTable[facing];

    /// <summary>cos of the facing angle scaled by 256 (range -256..256). North (0) = +256.</summary>
    public static int Cos(byte facing) => CosTable[facing];

    /// <summary>
    /// Facing of the vector (dx, dy) in map coordinates (+Y is south). (0,0) returns 0.
    /// Integer atan2 via octant reduction and a 257-entry table; error is at most 1 step.
    /// </summary>
    public static byte FromDelta(int dx, int dy)
    {
        if (dx == 0 && dy == 0) return 0;
        long ax = dx < 0 ? -(long)dx : dx;
        long ay = dy < 0 ? -(long)dy : dy;   // dy is negated y-up, so |dy| is unchanged
        int baseAngle;                        // 0..64: angle within the north-east quadrant
        if (ax <= ay)
        {
            int r = (int)((ax * 256 + ay / 2) / ay);
            baseAngle = AtanTable[r];
        }
        else
        {
            int r = (int)((ay * 256 + ax / 2) / ax);
            baseAngle = 64 - AtanTable[r];
        }
        // Screen y grows southward: dy < 0 is the northern half.
        int angle;
        if (dx >= 0) angle = dy <= 0 ? baseAngle : 128 - baseAngle;
        else angle = dy > 0 ? 128 + baseAngle : 256 - baseAngle;
        return (byte)angle;
    }

    /// <summary>
    /// Sprite frame index for a sprite sheet with <paramref name="count"/> facings (8, 16 or 32).
    /// Rounds to the nearest frame (ties round up); frame 0 = north, clockwise.
    /// </summary>
    public static int ToFacings(byte facing, int count)
    {
        if (count != 8 && count != 16 && count != 32) throw new ArgumentOutOfRangeException(nameof(count));
        int step = 256 / count;
        return (facing + step / 2) / step % count;
    }

    /// <summary>Signed shortest turn from a to b in [-128,127] (positive = clockwise).</summary>
    public static int Difference(byte a, byte b) => (sbyte)(byte)(b - a);

    /// <summary>Turns current toward target by at most rate steps (rate &lt;= 0 means no turn), shortest way.</summary>
    public static byte TurnToward(byte current, byte target, int rate)
    {
        int d = Difference(current, target);
        if (rate < 0) rate = 0;
        if (d >= -rate && d <= rate) return rate == 0 ? current : target;
        return (byte)(current + (d > 0 ? rate : -rate));
    }
}
