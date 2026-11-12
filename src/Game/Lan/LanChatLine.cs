// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
namespace Rts.Game.Lan;

/// <summary>
/// One line of the in-game LAN chat list (M9-08, Q-M9-g): a player's chat line or a system notice (drops, resync). Lines fade out a few
/// seconds after <paramref name="AtMicroseconds"/> (the session clock).
/// </summary>
/// <param name="FromPeer">The sender's peer id, or -1 for a system notice.</param>
/// <param name="Player">The sender's player (its colour), or -1 for a spectator or a notice.</param>
/// <param name="Sender">The sender's display name ("" for a notice).</param>
/// <param name="Text">The text.</param>
/// <param name="Team">True for a team-only line.</param>
/// <param name="AtMicroseconds">When it arrived, on the session clock.</param>
public sealed record LanChatLine(int FromPeer, int Player, string Sender, string Text, bool Team, long AtMicroseconds)
{
    /// <summary>True for a system notice.</summary>
    public bool System => FromPeer < 0;
}
