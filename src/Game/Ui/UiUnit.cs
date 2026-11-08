// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
namespace Rts.Game.Ui;

/// <summary>
/// The effective UI scale seen by the screens that lay out in "5x7 font pixels": screen pixels per font pixel, fractional, always
/// <c>2 x</c> the UI scale. Arithmetic with whole numbers (<c>5 * unit</c>, <c>unit / 2</c>, <c>x - unit</c>) rounds to whole screen pixels, so
/// code written for a whole-number scale follows any UI scale from 75 % to 200 % without being rewritten.
/// </summary>
public readonly struct UiUnit : IEquatable<UiUnit>
{
    /// <summary>Creates a unit of <paramref name="value"/> screen pixels per font pixel.</summary>
    public UiUnit(float value) => Value = value;

    /// <summary>Screen pixels per font pixel.</summary>
    public float Value { get; }

    /// <summary>The unit for a UI scale (1.0 gives 2).</summary>
    public static UiUnit FromScale(float uiScale) => new(uiScale * 2f);

    /// <summary>The unit rounded to whole screen pixels, at least 1.</summary>
    public int Whole => Math.Max(1, (int)MathF.Round(Value, MidpointRounding.AwayFromZero));

    /// <summary>The UI scale this unit came from.</summary>
    public float Scale => Value / 2f;

    private static int R(float v) => (int)MathF.Round(v, MidpointRounding.AwayFromZero);

    /// <summary>Units times a count of font pixels, in whole screen pixels.</summary>
    public static int operator *(int n, UiUnit u) => R(n * u.Value);

    /// <summary>Units times a count of font pixels, in whole screen pixels.</summary>
    public static int operator *(UiUnit u, int n) => R(n * u.Value);

    /// <summary>Units times a factor, in whole screen pixels.</summary>
    public static int operator *(float f, UiUnit u) => R(f * u.Value);

    /// <summary>Units times a factor, in whole screen pixels.</summary>
    public static int operator *(UiUnit u, float f) => R(f * u.Value);

    /// <summary>A fraction of a unit, in whole screen pixels.</summary>
    public static int operator /(UiUnit u, int n) => R(u.Value / n);

    /// <summary>A whole number plus a unit, in whole screen pixels.</summary>
    public static int operator +(int a, UiUnit u) => a + R(u.Value);

    /// <summary>A unit plus a whole number, in whole screen pixels.</summary>
    public static int operator +(UiUnit u, int a) => a + R(u.Value);

    /// <summary>A whole number minus a unit, in whole screen pixels.</summary>
    public static int operator -(int a, UiUnit u) => a - R(u.Value);

    /// <summary>A unit minus a whole number, in whole screen pixels.</summary>
    public static int operator -(UiUnit u, int a) => R(u.Value) - a;

    /// <summary>A whole number of screen pixels per font pixel (the original convention).</summary>
    public static implicit operator UiUnit(int whole) => new(whole);

    /// <summary>The value, for the float font overloads.</summary>
    public static implicit operator float(UiUnit u) => u.Value;

    /// <summary>The unit in whole screen pixels (at least 1).</summary>
    public static explicit operator int(UiUnit u) => u.Whole;

    /// <inheritdoc />
    public bool Equals(UiUnit other) => Value.Equals(other.Value);

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is UiUnit u && Equals(u);

    /// <inheritdoc />
    public override int GetHashCode() => Value.GetHashCode();

    /// <summary>Equality.</summary>
    public static bool operator ==(UiUnit a, UiUnit b) => a.Equals(b);

    /// <summary>Inequality.</summary>
    public static bool operator !=(UiUnit a, UiUnit b) => !a.Equals(b);

    /// <inheritdoc />
    public override string ToString() => Value.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
}
