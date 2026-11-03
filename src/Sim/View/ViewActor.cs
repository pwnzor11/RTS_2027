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

    /// <summary>True for vehicles and infantry.</summary>
    public bool IsMobile => Kind is ActorKind.Vehicle or ActorKind.Infantry;
}
