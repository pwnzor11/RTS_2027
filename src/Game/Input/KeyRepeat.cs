// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
namespace Rts.Game.Input;

/// <summary>
/// S18 GAMEUI-11: key repeat for a held text-editing key (Backspace in the chat box). Fires on the press, then after
/// <see cref="DelayFrames"/> frames every <see cref="IntervalFrames"/> frames. Counted in frames (the game steps at a fixed 60 Hz), so it is deterministic to test.
/// </summary>
public sealed class KeyRepeat
{
    /// <summary>Frames the key must be held before it repeats (about 400 ms at 60 Hz).</summary>
    public const int DelayFrames = 24;

    /// <summary>Frames between repeats once repeating (about 33 ms at 60 Hz).</summary>
    public const int IntervalFrames = 2;

    private int _held;

    /// <summary>Call once per frame with whether the key is down; true when the key should act this frame.</summary>
    public bool Step(bool down)
    {
        if (!down)
        {
            _held = 0;
            return false;
        }

        _held++;
        if (_held == 1) return true;
        return _held > DelayFrames && (_held - DelayFrames - 1) % IntervalFrames == 0;
    }
}
