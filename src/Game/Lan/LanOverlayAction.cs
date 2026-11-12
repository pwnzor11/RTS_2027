// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
namespace Rts.Game.Lan;

/// <summary>A button the player clicked on the LAN overlay (M9-08, M9-10); the game screen carries it out.</summary>
/// <param name="Kind">Which button.</param>
/// <param name="Peer">The peer it is about (Drop), else -1.</param>
public readonly record struct LanOverlayAction(LanOverlayActionKind Kind, int Peer = -1);
