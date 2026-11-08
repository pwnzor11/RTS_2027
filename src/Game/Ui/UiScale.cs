// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
namespace Rts.Game.Ui;

/// <summary>The "Text size" setting: scales fonts only, never boxes (M11-10h).</summary>
public enum TextSize
{
    /// <summary>85 % text.</summary>
    Small = 0,

    /// <summary>100 % text (default).</summary>
    Normal = 1,

    /// <summary>125 % text.</summary>
    Large = 2,

    /// <summary>150 % text.</summary>
    ExtraLarge = 3,
}

/// <summary>The result of <see cref="UiScale.Resolve"/>: the scale every HUD and menu element draws at.</summary>
/// <param name="Scale">The effective UI scale: 1.0 draws a 12 px font and a 20 px sidebar row.</param>
/// <param name="Requested">The scale the automatic rule and the setting asked for, before the window-size clamp.</param>
public readonly record struct UiScaleResult(float Scale, float Requested)
{
    /// <summary>True when the window is too small for the requested scale and <see cref="Scale"/> was lowered.</summary>
    public bool Limited => Scale < Requested - 0.001f;
}

/// <summary>
/// THE UI scale rule (audit H4/H5, M11-10h). Every HUD and menu element takes its scale from <see cref="Resolve"/>: the automatic
/// scale from the window height (1 up to 800 px, 1.5 up to 1200 px, 2 above) times the player's "UI scale" setting
/// (75 to 200 %), clamped so the layout still fits the window. A separate <see cref="TextFactor"/> (the "Text size" setting)
/// scales fonts only. Text is rasterised at the nearest atlas size, so nothing is stretched.
/// </summary>
public static class UiScale
{
    /// <summary>The UI scale steps of the setting, in percent.</summary>
    public static IReadOnlyList<int> Percents { get; } = new[] { 75, 100, 125, 150, 175, 200 };

    /// <summary>The default UI scale in percent.</summary>
    public const int DefaultPercent = 100;

    /// <summary>The layout needs this many scaled-away units of window width (the sidebar plus a map view that fits the selection card).</summary>
    public const int MinWidthUnits = 800;

    /// <summary>The layout needs this many scaled-away units of window height (credits, tabs, a few cameo rows and the minimap).</summary>
    public const int MinHeightUnits = 450;

    /// <summary>The lowest scale the clamp can give.</summary>
    public const float FloorScale = 0.5f;

    /// <summary>Multiplier applied to font sizes only (the Text size setting); set by <c>GameServices</c> when the settings change.</summary>
    public static float TextFactor { get; set; } = 1f;

    /// <summary>The font multiplier of a text size.</summary>
    public static float FactorOf(TextSize size) => size switch
    {
        TextSize.Small => 0.85f,
        TextSize.Large => 1.25f,
        TextSize.ExtraLarge => 1.5f,
        _ => 1f,
    };

    /// <summary>The display name of a text size.</summary>
    public static string NameOf(TextSize size) => size switch
    {
        TextSize.Small => "Small",
        TextSize.Large => "Large",
        TextSize.ExtraLarge => "Extra large",
        _ => "Normal",
    };

    /// <summary>The automatic scale for a backbuffer of <paramref name="height"/> pixels: 1 up to 800, 1.5 up to 1200, 2 above.</summary>
    public static float Auto(int height)
    {
        if (height <= 800) return 1f;
        if (height <= 1200) return 1.5f;
        return 2f;
    }

    /// <summary>The largest scale at which a window of this size still holds the layout.</summary>
    public static float MaxFit(int width, int height) =>
        Math.Max(FloorScale, Math.Min(width / (float)MinWidthUnits, height / (float)MinHeightUnits));

    /// <summary>Snaps a percent to the nearest step of <see cref="Percents"/>.</summary>
    public static int SnapPercent(int percent)
    {
        int best = Percents[0];
        foreach (int p in Percents)
            if (Math.Abs(p - percent) < Math.Abs(best - percent)) best = p;
        return best;
    }

    /// <summary>The next step of the setting in direction <paramref name="d"/> (+1 or -1), stopping at the ends.</summary>
    public static int StepPercent(int percent, int d)
    {
        int i = 0;
        for (int k = 0; k < Percents.Count; k++)
            if (Percents[k] == SnapPercent(percent)) i = k;
        return Percents[Math.Clamp(i + d, 0, Percents.Count - 1)];
    }

    /// <summary>The effective scale for a window and a setting.</summary>
    /// <param name="width">Window width in pixels.</param>
    /// <param name="height">Window height in pixels.</param>
    /// <param name="percent">The UI scale setting, 75 to 200.</param>
    public static UiScaleResult Resolve(int width, int height, int percent)
    {
        float requested = Auto(height) * Math.Clamp(percent, Percents[0], Percents[^1]) / 100f;
        float fit = MathF.Floor(MaxFit(width, height) * 20f) / 20f;
        float scale = MathF.Max(FloorScale, MathF.Min(requested, fit));
        return new UiScaleResult(scale, requested);
    }
}
