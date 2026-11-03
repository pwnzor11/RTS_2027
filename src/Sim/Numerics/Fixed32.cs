// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using System.Text;

namespace Rts.Sim.Numerics;

/// <summary>
/// Signed 16.16 fixed-point number stored in an int (<see cref="Raw"/>). Integer math only.
/// Overflow is NOT checked: + - and unary - wrap in two's complement; * and / compute in 64 bits
/// and the final narrowing to 32 bits wraps. Division by zero throws DivideByZeroException.
/// </summary>
public readonly struct Fixed32 : IEquatable<Fixed32>, IComparable<Fixed32>
{
    private const int FracBits = 16;
    private const int OneRaw = 1 << FracBits;

    /// <summary>The raw 16.16 value.</summary>
    public int Raw { get; }

    private Fixed32(int raw) => Raw = raw;

    /// <summary>0.</summary>
    public static readonly Fixed32 Zero = new(0);
    /// <summary>1.</summary>
    public static readonly Fixed32 One = new(OneRaw);
    /// <summary>0.5.</summary>
    public static readonly Fixed32 Half = new(OneRaw / 2);

    /// <summary>Wraps a raw 16.16 value.</summary>
    public static Fixed32 FromRaw(int raw) => new(raw);

    /// <summary>Converts an integer (wraps if outside -32768..32767).</summary>
    public static Fixed32 FromInt(int value) => new(unchecked(value << FracBits));

    /// <summary>num/den rounded to nearest, ties away from zero.</summary>
    public static Fixed32 FromRatio(int num, int den)
    {
        if (den == 0) throw new DivideByZeroException();
        bool neg = (num < 0) != (den < 0);
        ulong n = (ulong)(num < 0 ? -(long)num : num) << FracBits;
        ulong d = (ulong)(den < 0 ? -(long)den : den);
        ulong q = (n + d / 2) / d;   // ties round up in magnitude, i.e. away from zero
        long r = neg ? -(long)q : (long)q;
        return new(unchecked((int)r));
    }

    /// <summary>Floor to integer (toward negative infinity).</summary>
    public int ToInt() => Raw >> FracBits;

    /// <summary>Round to nearest integer, ties away from zero.</summary>
    public int Round() => Raw >= 0
        ? (int)(((long)Raw + OneRaw / 2) >> FracBits)
        : -(int)((-(long)Raw + OneRaw / 2) >> FracBits);

    /// <summary>Absolute value (Abs of the minimum value wraps).</summary>
    public Fixed32 Abs() => Raw < 0 ? new(unchecked(-Raw)) : this;

    /// <summary>Smaller of two values.</summary>
    public static Fixed32 Min(Fixed32 a, Fixed32 b) => a.Raw <= b.Raw ? a : b;

    /// <summary>Larger of two values.</summary>
    public static Fixed32 Max(Fixed32 a, Fixed32 b) => a.Raw >= b.Raw ? a : b;

    /// <summary>this * value as an integer, truncated toward zero.</summary>
    public int MulInt(int value) => unchecked((int)(((long)Raw * value) / OneRaw));

    /// <summary>Wrapping addition.</summary>
    public static Fixed32 operator +(Fixed32 a, Fixed32 b) => new(unchecked(a.Raw + b.Raw));
    /// <summary>Wrapping subtraction.</summary>
    public static Fixed32 operator -(Fixed32 a, Fixed32 b) => new(unchecked(a.Raw - b.Raw));
    /// <summary>Wrapping negation.</summary>
    public static Fixed32 operator -(Fixed32 a) => new(unchecked(-a.Raw));
    /// <summary>Multiplication with a 64-bit intermediate; the result is rounded toward zero.</summary>
    public static Fixed32 operator *(Fixed32 a, Fixed32 b) => new(unchecked((int)(((long)a.Raw * b.Raw) / OneRaw)));
    /// <summary>Division with a 64-bit intermediate; the result is rounded toward zero.</summary>
    public static Fixed32 operator /(Fixed32 a, Fixed32 b) => new(unchecked((int)(((long)a.Raw << FracBits) / b.Raw)));

    /// <summary>Less than.</summary>
    public static bool operator <(Fixed32 a, Fixed32 b) => a.Raw < b.Raw;
    /// <summary>Greater than.</summary>
    public static bool operator >(Fixed32 a, Fixed32 b) => a.Raw > b.Raw;
    /// <summary>Less than or equal.</summary>
    public static bool operator <=(Fixed32 a, Fixed32 b) => a.Raw <= b.Raw;
    /// <summary>Greater than or equal.</summary>
    public static bool operator >=(Fixed32 a, Fixed32 b) => a.Raw >= b.Raw;
    /// <summary>Equality.</summary>
    public static bool operator ==(Fixed32 a, Fixed32 b) => a.Raw == b.Raw;
    /// <summary>Inequality.</summary>
    public static bool operator !=(Fixed32 a, Fixed32 b) => a.Raw != b.Raw;

    /// <inheritdoc />
    public bool Equals(Fixed32 other) => Raw == other.Raw;
    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is Fixed32 f && f.Raw == Raw;
    /// <inheritdoc />
    public override int GetHashCode() => Raw;
    /// <inheritdoc />
    public int CompareTo(Fixed32 other) => Raw.CompareTo(other.Raw);

    /// <summary>Decimal text with 4 fractional digits (rounded half up in magnitude), integer math only.</summary>
    public override string ToString()
    {
        long abs = Raw < 0 ? -(long)Raw : Raw;
        long whole = abs >> FracBits;
        long frac = (((abs & (OneRaw - 1)) * 10000) + OneRaw / 2) >> FracBits;
        if (frac >= 10000) { whole++; frac -= 10000; }
        var sb = new StringBuilder();
        if (Raw < 0) sb.Append('-');
        sb.Append(whole).Append('.');
        string f = frac.ToString();
        sb.Append('0', 4 - f.Length).Append(f);
        return sb.ToString();
    }
}
