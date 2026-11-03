namespace Rts.Sim.Core;

/// <summary>Which entity store an <see cref="EntityId"/> belongs to. Persisted in replays; append only.</summary>
public enum ActorKind : byte { Vehicle = 0, Infantry = 1, Building = 2, Projectile = 3 }
