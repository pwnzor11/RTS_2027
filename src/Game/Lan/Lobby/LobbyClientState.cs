// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
namespace Rts.Game.Lan.Lobby;

/// <summary>Where a joining client is (M9-05).</summary>
public enum LobbyClientState
{
    /// <summary>Hello sent, waiting for Welcome or Reject.</summary>
    Connecting,
    /// <summary>Welcomed; the lobby is shown and can be changed.</summary>
    InLobby,
    /// <summary>The host refused us; <see cref="LobbyClient.Reason"/> says why.</summary>
    Rejected,
    /// <summary>The connection ended (host left, kicked, timeout); <see cref="LobbyClient.Reason"/> says why.</summary>
    Disconnected,
    /// <summary>The host started the match; <see cref="LobbyClient.Setup"/> is ready.</summary>
    Started,
}
