// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Net;

namespace Rts.Game.Lan.Lobby;

/// <summary>
/// The whole lobby as the host broadcasts it (M9-05): host name, map, slots, spectators and command delay. Immutable; the host builds a new
/// value on every change and sends it (<see cref="LobbyCodec"/> turns it into the blob of <c>NetMessage.LobbyState</c>).
/// </summary>
/// <param name="HostName">The host's display name (also the lobby name in the join list).</param>
/// <param name="Map">The chosen map.</param>
/// <param name="Slots">2 to 4 slots, in screen order.</param>
/// <param name="Spectators">Watching peers (at most <see cref="NetProtocol.MaxSpectators"/>).</param>
/// <param name="CommandDelay">2, 3 or 4 ticks (Q-M9-c).</param>
public sealed record LobbyData(string HostName, LobbyMap Map, IReadOnlyList<LobbySlotState> Slots, IReadOnlyList<LobbySpectator> Spectators, int CommandDelay)
{
    /// <summary>Longest display name, in UTF-8 bytes.</summary>
    public const int MaxNameBytes = 24;

    /// <summary>Slot index of a peer, or -1 (spectators and strangers).</summary>
    public int SlotOf(int peer)
    {
        for (int i = 0; i < Slots.Count; i++) if (Slots[i].IsTaken && Slots[i].Peer == peer) return i;
        return -1;
    }

    /// <summary>Players in the lobby: taken Human slots plus spectators.</summary>
    public int People => Slots.Count(s => s.IsTaken) + Spectators.Count;

    /// <summary>Open Human slots.</summary>
    public int OpenSlots => Slots.Count(s => s.IsOpen);

    /// <summary>Value equality that compares the lists element by element.</summary>
    public bool Equals(LobbyData? other) =>
        other is not null && HostName == other.HostName && Map == other.Map && CommandDelay == other.CommandDelay
        && Slots.SequenceEqual(other.Slots) && Spectators.SequenceEqual(other.Spectators);

    /// <inheritdoc />
    public override int GetHashCode() => HashCode.Combine(HostName, Map, CommandDelay, Slots.Count, Spectators.Count);
}
