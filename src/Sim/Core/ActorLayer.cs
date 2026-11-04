// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
namespace Rts.Sim.Core;

/// <summary>
/// Where a mobile actor is relative to the surface (M3, design 05 §1/§4), kept per vehicle/infantry slot in the "tunnels"
/// part (<see cref="Tunnels.TunnelState.VehicleLayer"/> / <see cref="Tunnels.TunnelState.InfantryLayer"/>). Surface = normal. Queued = waiting in an entrance queue (still on the
/// surface, owner-only knowledge). Tunnel = drawn into a player's network (in transit; not on the map). Diving = visible
/// dive animation of a burrower. Burrowed = travelling under open ground. Surfacing = behind a surfacing telegraph.
/// Passenger = inside a transport. Underground (<see cref="ActorLayers.IsUnderground"/>) = Tunnel, Burrowed, Surfacing
/// or Passenger: such actors cannot attack or be attacked and are hidden from enemies (design 05 §1 "F").
/// Append only: values are persisted.
/// </summary>
public enum ActorLayer : byte { Surface = 0, Queued = 1, Tunnel = 2, Diving = 3, Burrowed = 4, Surfacing = 5, Passenger = 6 }
