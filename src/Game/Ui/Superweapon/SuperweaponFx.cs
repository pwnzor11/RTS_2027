// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Microsoft.Xna.Framework;
using Rts.Sim.Core;

namespace Rts.Game.Ui.Superweapon;

/// <summary>
/// The big flash and screen shake of a superweapon blast (M3-13), driven by <see cref="FxKind.Superweapon"/> events. Pure state:
/// <see cref="Observe"/> once per new snapshot, then read <see cref="FlashAlpha"/> (0..255, draw a white full-screen quad) and
/// <see cref="ShakeOffset"/> (pixels to add to the camera) for the snapshot's tick. Both fade out linearly; the shake is a fixed
/// deterministic wobble, never random.
/// </summary>
public sealed class SuperweaponFx
{
    /// <summary>Ticks the flash lasts.</summary>
    public const int FlashTicks = 24;
    /// <summary>Ticks the shake lasts.</summary>
    public const int ShakeTicks = 45;
    /// <summary>Peak shake in pixels at the start of the shake.</summary>
    public const float ShakePixels = 10f;

    private int _startTick = int.MinValue;
    private int _lastObserved = int.MinValue;

    /// <summary>Looks at a snapshot's tick and events and (re)starts the effect for a superweapon event. Safe to call every frame.</summary>
    public void Observe(int tick, IReadOnlyList<FxEvent>? events)
    {
        if (tick == _lastObserved) return;
        _lastObserved = tick;
        if (events is null) return;
        foreach (FxEvent e in events)
            if (e.Kind == FxKind.Superweapon) _startTick = tick;
    }

    private int Age(int tick) => _startTick == int.MinValue ? int.MaxValue : tick - _startTick;

    /// <summary>White overlay alpha 0..255 at this tick.</summary>
    public int FlashAlpha(int tick)
    {
        int age = Age(tick);
        return age < 0 || age >= FlashTicks ? 0 : 255 * (FlashTicks - age) / FlashTicks;
    }

    /// <summary>Camera offset in pixels at this tick (zero when idle).</summary>
    public Vector2 ShakeOffset(int tick)
    {
        int age = Age(tick);
        if (age < 0 || age >= ShakeTicks) return Vector2.Zero;
        float amp = ShakePixels * (ShakeTicks - age) / ShakeTicks;
        // Two out-of-phase square-ish wobbles from the tick number: deterministic, no RNG, no trig.
        float x = ((age * 7) % 4) switch { 0 => 1f, 1 => 0f, 2 => -1f, _ => 0f };
        float y = ((age * 5) % 4) switch { 0 => 0f, 1 => 1f, 2 => 0f, _ => -1f };
        return new Vector2(x * amp, y * amp);
    }
}
