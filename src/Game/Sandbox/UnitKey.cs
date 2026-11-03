using Rts.Sim.Core;

namespace Rts.Game.Sandbox;

/// <summary>
/// A unit by its store and id. Vehicles and infantry have separate id spaces, so an <see cref="EntityId"/> alone
/// does not tell two units apart.
/// </summary>
public readonly record struct UnitKey(ActorKind Kind, EntityId Id)
{
    /// <summary>The key of a unit as it appears in a snapshot.</summary>
    public static UnitKey Of(UnitView unit) =>
        new(unit.Kind == UnitKind.Vehicle ? ActorKind.Vehicle : ActorKind.Infantry, unit.Id);
}
