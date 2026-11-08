// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using System.Globalization;
using Tomlyn.Model;

namespace Rts.Game.Render;

/// <summary>
/// The mood of a theater (D-184): a subtle colour grade laid over the world view (never the HUD) and the colour of the dust vehicles kick up.
/// Read from the optional <c>[mood]</c> table of <c>data/theaters/&lt;theater&gt;.toml</c>: <c>tint</c> (<c>#RRGGBB</c>), <c>strength</c> (0..255, how
/// much of the tint is blended in; 0 or no table = no grade, the look of today) and <c>dust</c> (<c>#RRGGBB</c>). View only.
/// </summary>
/// <param name="Tint">Tint colour 0xRRGGBB.</param>
/// <param name="Strength">Blend strength 0..255; 0 = off.</param>
/// <param name="Dust">Dust colour 0xRRGGBB, or null for the default grey-brown.</param>
public sealed record MoodGrade(uint Tint, int Strength, uint? Dust)
{
    /// <summary>Highest strength a data file may ask for, so a typo cannot hide the map. ASSUMED.</summary>
    public const int MaxStrength = 90;

    /// <summary>Dust colour when a theater names none.</summary>
    public const uint DefaultDust = 0xD0C3A4;

    /// <summary>No grade (the look of today).</summary>
    public static MoodGrade None { get; } = new(0xFFFFFF, 0, null);

    /// <summary>Set the <c>RTS_MOOD</c> environment variable to <c>off</c> to draw every theater ungraded (to compare with the look of today). <c>RTS_FXLOOK=off</c> does that and more (<see cref="FxLook.Off"/>).</summary>
    public static bool DisabledByEnvironment { get; set; } =
        string.Equals(Environment.GetEnvironmentVariable("RTS_MOOD"), "off", StringComparison.OrdinalIgnoreCase);

    /// <summary>The dust colour to use: the theater's, else <see cref="DefaultDust"/>.</summary>
    public uint DustColour => Dust ?? DefaultDust;

    /// <summary>Whether the grade changes anything.</summary>
    public bool Active => Strength > 0 && !DisabledByEnvironment && !FxLook.Off;

    /// <summary>Reads the <c>[mood]</c> table of a parsed theater file. A missing table, or a bad value, gives <see cref="None"/> or the default for that value.</summary>
    public static MoodGrade Parse(TomlTable root)
    {
        ArgumentNullException.ThrowIfNull(root);
        if (!root.TryGetValue("mood", out object? mv) || mv is not TomlTable mood) return None;
        uint tint = Colour(mood, "tint") ?? 0xFFFFFF;
        int strength = mood.TryGetValue("strength", out object? s) && s is long l ? (int)Math.Clamp(l, 0, MaxStrength) : 0;
        return new MoodGrade(tint, strength, Colour(mood, "dust"));
    }

    private static uint? Colour(TomlTable t, string key)
    {
        if (!t.TryGetValue(key, out object? v) || v is not string s || s.Length != 7 || s[0] != '#') return null;
        return uint.TryParse(s.AsSpan(1), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out uint rgb) ? rgb : null;
    }
}
