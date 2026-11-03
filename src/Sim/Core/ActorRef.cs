namespace Rts.Sim.Core;

/// <summary>An <see cref="EntityId"/> together with the store it indexes into.</summary>
public readonly record struct ActorRef(ActorKind Kind, EntityId Id);
