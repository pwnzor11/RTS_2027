// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Game.Input;

namespace Rts.Game.Controls;

/// <summary>Reads the keyboard by action: the key comes from <see cref="GameKeys.Active"/>, so game code never names a literal key.</summary>
public static class BoundInput
{
    /// <summary>True while the action's key is held and nobody consumed it.</summary>
    public static bool IsDown(this InputFrame input, GameAction action) => input.IsDown(GameKeys.Active.Get(action));

    /// <summary>True on the frame the action's key went down, if nobody consumed it.</summary>
    public static bool Pressed(this InputFrame input, GameAction action) => input.Pressed(GameKeys.Active.Get(action));

    /// <summary>If the action's key went down this frame, consumes it and returns true.</summary>
    public static bool ConsumePressed(this InputFrame input, GameAction action) => input.ConsumePressed(GameKeys.Active.Get(action));
}
