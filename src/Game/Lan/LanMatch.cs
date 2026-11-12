// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Game.Screens;

namespace Rts.Game.Lan;

/// <summary>Starts a LAN match from a <see cref="LanMatchSetup"/> (M9 wave-2 contract; the body is M9-07).</summary>
public static class LanMatch
{
    /// <summary>Builds the screen that plays the match on this peer. The caller replaces the lobby with it.</summary>
    public static IScreen CreateScreen(GameServices services, LanMatchSetup setup)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(setup);
        LanSession session;
        try { session = new LanSession(setup, services.RulesDir, LanSessionOptions.ForGame(services.Settings)); }
        catch
        {
            setup.Transport.Dispose();   // the match owns the transport: nobody else will close it
            throw;
        }
        return GameScreen.CreateLan(services, session);
    }
}
