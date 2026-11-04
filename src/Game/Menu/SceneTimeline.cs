// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
namespace Rts.Game.Menu;

/// <summary>Which scene is showing at a given time, and how far it has cross-faded into the next one.</summary>
/// <param name="Current">Index of the scene on screen.</param>
/// <param name="Next">Index of the scene fading in (equals <see cref="Current"/> when <see cref="Mix"/> is 0).</param>
/// <param name="Mix">0 = only <see cref="Current"/>, 1 = only <see cref="Next"/> (smoothed).</param>
/// <param name="LocalTime">Seconds on <see cref="Current"/>'s own clock. A scene's clock starts when it begins fading in, so it carries on without a jump when it takes over.</param>
/// <param name="NextLocalTime">Seconds on <see cref="Next"/>'s own clock (0 when the fade starts).</param>
public readonly record struct SceneCursor(int Current, int Next, float Mix, float LocalTime, float NextLocalTime = 0f);

/// <summary>Pure scene scheduling: each scene holds for its duration, the last <c>fade</c> seconds of it cross-fade into the next, and the list loops.</summary>
public static class SceneTimeline
{
    /// <summary>Locates the cursor for <paramref name="seconds"/> since the menu opened.</summary>
    /// <param name="durations">Seconds per scene (each at least <paramref name="fade"/>; smaller values are raised to it).</param>
    /// <param name="fade">Cross-fade length in seconds.</param>
    /// <param name="seconds">Time since the start; negative is treated as 0.</param>
    public static SceneCursor Locate(IReadOnlyList<float> durations, float fade, float seconds)
    {
        int n = durations.Count;
        if (n == 0) return new SceneCursor(0, 0, 0f, 0f);
        if (n == 1) return new SceneCursor(0, 0, 0f, MathF.Max(0f, seconds));

        float total = 0f;
        for (int i = 0; i < n; i++) total += MathF.Max(durations[i], fade);
        float t = ParallaxMath.Wrap(MathF.Max(0f, seconds), total);
        for (int i = 0; i < n; i++)
        {
            float d = MathF.Max(durations[i], fade);
            if (t < d)
            {
                float intoFade = t - (d - fade);
                float mix = fade <= 0f || intoFade <= 0f ? 0f : ParallaxMath.Smooth(intoFade / fade);
                return new SceneCursor(i, mix > 0f ? (i + 1) % n : i, mix, t + fade, MathF.Max(0f, intoFade));
            }
            t -= d;
        }
        return new SceneCursor(0, 0, 0f, 0f);
    }
}
