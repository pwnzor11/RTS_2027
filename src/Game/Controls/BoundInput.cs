// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Game.Input;

namespace Rts.Game.Controls;

/// <summary>Reads the keyboard by action: the key comes from <see cref="GameKeys.Active"/>, so game code never names a literal key.</summary>
public static class BoundInput
{
    /// <summary>True while the action's key is held and nobody consumed it.</summary>
    public static bool IsDown(this InputFrame input, GameAction action) => input.IsDown(GameKeys.Active.Get(action)) || AltIs(GameKeys.Active.GetAlt(action), k => input.IsDown(k));

    /// <summary>True on the frame the action's key went down, if nobody consumed it.</summary>
    public static bool Pressed(this InputFrame input, GameAction action) => input.Pressed(GameKeys.Active.Get(action)) || AltIs(GameKeys.Active.GetAlt(action), k => input.Pressed(k));

    /// <summary>If the action's key went down this frame, consumes it and returns true.</summary>
    public static bool ConsumePressed(this InputFrame input, GameAction action) => ConsumeEither(input, GameKeys.Active.Get(action), GameKeys.Active.GetAlt(action));

    // QOL-20 (D-313): the second key of an action; Keys.None means there is none.
    private static bool AltIs(Microsoft.Xna.Framework.Input.Keys alt, Func<Microsoft.Xna.Framework.Input.Keys, bool> test) =>
        alt != Microsoft.Xna.Framework.Input.Keys.None && test(alt);

    // Both keys are consumed (no short-circuit), so a second key never leaks to another handler.
    private static bool ConsumeEither(InputFrame input, Microsoft.Xna.Framework.Input.Keys key, Microsoft.Xna.Framework.Input.Keys alt)
    {
        bool first = input.ConsumePressed(key);
        bool second = alt != Microsoft.Xna.Framework.Input.Keys.None && input.ConsumePressed(alt);
        return first || second;
    }
}
