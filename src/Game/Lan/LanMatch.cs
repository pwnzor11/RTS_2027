// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Game.Screens;
using Rts.Net;

namespace Rts.Game.Lan;

/// <summary>Starts a LAN match from a <see cref="LanMatchSetup"/> (M9 wave-2 contract; the body is M9-07).</summary>
public static class LanMatch
{
    /// <summary>Builds the screen that plays the match on this peer. The caller replaces the lobby with it.</summary>
    public static IScreen CreateScreen(GameServices services, LanMatchSetup setup)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(setup);
        LanSession? session = null;
        try
        {
            session = new LanSession(setup, services.RulesDir, LanSessionOptions.ForGame(services.Settings));
            return GameScreen.CreateLan(services, session);
        }
        catch
        {
            if (session is not null) session.Dispose();   // Bye to the others, then the transport closes
            else Abandon(setup);
            throw;
        }
    }

    /// <summary>
    /// This side's match could not start (M9-14): tells the others (the host sends every client a Bye so nobody waits for it; a client
    /// tells the host it is gone) and closes the transport, which the match owned. Safe to call more than once.
    /// </summary>
    public static void Abandon(LanMatchSetup setup)
    {
        ArgumentNullException.ThrowIfNull(setup);
        try
        {
            if (setup.IsHost) setup.Transport.Broadcast(new NetMessage.Bye(LanText.HostStartFailed));
            else setup.Transport.Send(0, new NetMessage.Bye(LanText.ClientStartFailed));
        }
        catch (Exception e) when (e is ObjectDisposedException or InvalidOperationException or System.Net.Sockets.SocketException)
        {
            // best effort: the others time us out
        }
        setup.Transport.Dispose();   // flushes the Bye first
    }
}
