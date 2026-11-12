// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Microsoft.Xna.Framework;

namespace Rts.Game.Render;

/// <summary>
/// Light screen shake for big blasts (M10-14): a kick sets an amplitude in pixels that falls linearly to zero over
/// <see cref="DurationSeconds"/>; the offset jumps to a new hashed direction each update. Pure and view only: the caller shifts the drawn
/// camera by <see cref="Offset"/>, never the HUD or the camera used for input.
/// </summary>
public sealed class JuiceShake
{
    /// <summary>Time for a shake to die away.</summary>
    public const float DurationSeconds = 0.3f;

    /// <summary>Largest amplitude in pixels, however many blasts stack. ASSUMED.</summary>
    public const float MaxAmplitudePx = 8f;

    private float _peak;
    private float _elapsed = DurationSeconds;
    private int _step;

    /// <summary>Current amplitude in pixels (0 when idle).</summary>
    public float Amplitude => _elapsed >= DurationSeconds ? 0f : _peak * (1f - _elapsed / DurationSeconds);

    /// <summary>True while the screen is shaking.</summary>
    public bool Active => Amplitude > 0f;

    /// <summary>Screen offset for this step, in whole pixels (zero when idle).</summary>
    public Vector2 Offset
    {
        get
        {
            float a = Amplitude;
            if (a <= 0f) return Vector2.Zero;
            uint h = DecorHash.Mix(0x5348_414BUL, _step, 0, 7);
            float dx = ((h & 255) / 127.5f - 1f) * a, dy = ((h >> 8 & 255) / 127.5f - 1f) * a;
            return new Vector2(MathF.Round(dx), MathF.Round(dy));
        }
    }

    /// <summary>Adds a blast of <paramref name="amplitudePx"/> pixels. A second blast on top of a running shake adds a quarter of the smaller one; the total is capped.</summary>
    public void Kick(float amplitudePx)
    {
        if (amplitudePx <= 0f) return;
        float now = Amplitude;
        float big = MathF.Max(now, amplitudePx), small = MathF.Min(now, amplitudePx);
        _peak = MathF.Min(MaxAmplitudePx, big + 0.25f * small);
        _elapsed = 0f;
    }

    /// <summary>Moves time on by <paramref name="seconds"/> (negative is ignored) and picks the next offset.</summary>
    public void Update(float seconds)
    {
        if (seconds > 0f) _elapsed = MathF.Min(DurationSeconds, _elapsed + seconds);
        _step++;
    }

    /// <summary>Stops the shake.</summary>
    public void Clear()
    {
        _peak = 0f;
        _elapsed = DurationSeconds;
    }
}
