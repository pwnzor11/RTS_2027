// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Game.Input;

namespace Rts.Game.Controls;

/// <summary>
/// Reads the keyboard by action: the key comes from <see cref="GameKeys.Active"/>, so game code never names a literal key. A binding is a
/// <see cref="KeyChord"/>: it matches only when its key is down AND Alt is held exactly when the chord says so (Shift and Ctrl are ignored).
/// </summary>
public static class BoundInput
{
    /// <summary>True while the action's key is held and nobody consumed it.</summary>
    public static bool IsDown(this InputFrame input, GameAction action) =>
        Matches(input, GameKeys.Active.GetChord(action), input.IsDown) || Matches(input, GameKeys.Active.GetAltChord(action), input.IsDown);

    /// <summary>True on the frame the action's key went down, if nobody consumed it.</summary>
    public static bool Pressed(this InputFrame input, GameAction action) =>
        Matches(input, GameKeys.Active.GetChord(action), input.Pressed) || Matches(input, GameKeys.Active.GetAltChord(action), input.Pressed);

    /// <summary>If the action's key went down this frame, consumes it and returns true.</summary>
    public static bool ConsumePressed(this InputFrame input, GameAction action) =>
        ConsumeEither(input, GameKeys.Active.GetChord(action), GameKeys.Active.GetAltChord(action));

    // M16 U-06: the key must match and Alt must be held exactly when the chord says so; an empty chord never matches.
    private static bool Matches(InputFrame input, KeyChord chord, Func<Microsoft.Xna.Framework.Input.Keys, bool> test) =>
        !chord.IsNone && input.IsAltDown() == chord.Alt && test(chord.Key);

    // Both keys are consumed (no short-circuit), so a second key never leaks to another handler.
    private static bool ConsumeEither(InputFrame input, KeyChord first, KeyChord second)
    {
        bool a = ConsumeChord(input, first);
        bool b = ConsumeChord(input, second);
        return a || b;
    }

    private static bool ConsumeChord(InputFrame input, KeyChord chord) =>
        !chord.IsNone && input.IsAltDown() == chord.Alt && input.ConsumePressed(chord.Key);
}
