// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
namespace Rts.Net.Discovery;

/// <summary>What a host announces to LAN probes.</summary>
/// <param name="GameId">Always <see cref="DiscoveryInfo.GameName"/> for this game; other values are ignored.</param>
/// <param name="Protocol"><see cref="NetProtocol.Version"/> of the host.</param>
/// <param name="LobbyName">Shown in the join list (cut to 64 UTF-8 bytes on the wire).</param>
/// <param name="Players">Players (and spectators) in the lobby now.</param>
/// <param name="MaxPlayers">Lobby capacity.</param>
/// <param name="GamePort">UDP port to connect to.</param>
/// <param name="Open">True while the lobby accepts joins.</param>
/// <param name="Started">True once the match runs.</param>
public sealed record DiscoveryInfo(string GameId, int Protocol, string LobbyName, int Players, int MaxPlayers, int GamePort, bool Open, bool Started)
{
    /// <summary>The game id every probe and reply carries.</summary>
    public const string GameName = "RTS_2027";

    /// <summary>The multicast group probes are also sent to.</summary>
    public const string MulticastGroup = "239.255.42.71";

    /// <summary>True when the host speaks this build's protocol.</summary>
    public bool Compatible => Protocol == NetProtocol.Version;
}
