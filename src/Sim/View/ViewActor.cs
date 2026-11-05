// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Sim.Core;
using Rts.Sim.Missions;

namespace Rts.Sim.View;

/// <summary>
/// One actor as a player may know it (see <see cref="PlayerView"/>). A value copy: it never aliases sim arrays.
/// Cell = the cell of a mobile actor, or the top-left cell of a building; Position = lepton position (the centre of
/// the footprint for buildings). Mission, Idle and Target are filled for the player's own actors only (they are
/// hidden for everyone else). For a ghost (last-seen enemy building now under fog) Id holds the building slot index
/// with generation 0, and Health is the last-seen value.
/// </summary>
public readonly record struct ViewActor(
    EntityId Id, ActorKind Kind, byte Owner, Relation Relation, int TypeIndex, CellCoord Cell, LeptonCoord Position,
    int Health, int MaxHealth, MissionKind Mission, bool Idle, EntityId Target, bool Ghost, int FootprintW, int FootprintH)
{
    /// <summary>The actor as an <see cref="ActorRef"/> (not valid for ghosts).</summary>
    public ActorRef Ref => new(Kind, Id);

    /// <summary>M3: the actor is underground (in a tunnel, burrowed, surfacing or a passenger). Own and allied actors carry it
    /// always; an enemy actor is listed underground only when a Seismograph reveals it.</summary>
    public bool Underground { get; init; }

    /// <summary>M3: the actor is currently stealthed (invisible to enemies). Set on own and allied actors; an enemy listed with
    /// it set is one the viewer's team has revealed.</summary>
    public bool Stealthed { get; init; }

    /// <summary>M3: an enemy actor that would be hidden (underground or stealthed) but is revealed to the viewer's team
    /// (Seismograph, Searchlight, proximity). Always false for own and allied actors.</summary>
    public bool Revealed { get; init; }

    /// <summary>M7-05: the ship is a dived submarine. Own and allied ships carry it always; an enemy listed with it set is one the
    /// viewer's team detects with sonar (an undetected enemy submarine is not listed at all).</summary>
    public bool Submerged { get; init; }

    /// <summary>VET-3: veterancy rank 0..3 of a listed vehicle or soldier (0 for buildings, ghosts and when veterancy is off). Only actors in
    /// the list carry it, so it is as fog-honest as the list itself: an enemy's rank is known exactly while it is visible.</summary>
    public int Rank { get; init; }

    /// <summary>True for vehicles and infantry.</summary>
    public bool IsMobile => Kind is ActorKind.Vehicle or ActorKind.Infantry;
}
