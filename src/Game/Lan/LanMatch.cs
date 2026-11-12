// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Game.Screens;

namespace Rts.Game.Lan;

/// <summary>Starts a LAN match from a <see cref="LanMatchSetup"/> (M9 wave-2 contract; package W2-G fills the body).</summary>
public static class LanMatch
{
    /// <summary>Builds the screen that plays the match on this peer. The caller replaces the lobby with it.</summary>
    public static IScreen CreateScreen(GameServices services, LanMatchSetup setup) =>
        throw new NotImplementedException("M9 W2-G");
}
