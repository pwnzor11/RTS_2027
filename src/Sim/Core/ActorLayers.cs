// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
namespace Rts.Sim.Core;

/// <summary>Helpers for <see cref="ActorLayer"/>.</summary>
public static class ActorLayers
{
    /// <summary>True for Tunnel, Burrowed, Surfacing and Passenger: off the surface (no attack, untargetable, hidden from enemies).</summary>
    public static bool IsUnderground(ActorLayer layer) =>
        layer is ActorLayer.Tunnel or ActorLayer.Burrowed or ActorLayer.Surfacing or ActorLayer.Passenger;
}
