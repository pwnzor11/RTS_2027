// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
namespace Rts.Net.Lockstep;

/// <summary>
/// A peer left the match (M9-03, Q-M9-d). <paramref name="Player"/> is the slot it played (-1 when it had none); an AI takes that
/// slot from <paramref name="Tick"/>, the first tick whose commands the host has not sealed yet.
/// </summary>
public readonly record struct LockstepDrop(int Peer, int Player, int Tick, string Reason);
