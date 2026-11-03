using Rts.Sim.Core;

namespace Rts.Sim;

// Combat part of Simulation. OWNER: M1-09 (combat). Lead-created stub.
public sealed partial class Simulation
{
    /// <summary>Extra state parts owned by combat (e.g. projectiles), appended after "vision" in this order.</summary>
    private static ISimStatePart[] CombatParts() => Array.Empty<ISimStatePart>();

    /// <summary>Attack / AttackMove / Guard commands.</summary>
    private void ApplyCombatCommand(Command c) { }

    /// <summary>Combat system: weapons, projectiles, damage. Runs once per tick after movement.</summary>
    private void RunCombat() { }

    /// <summary>Called before an actor is freed; clear anything targeting it.</summary>
    private void OnCombatActorRemoving(ActorKind kind, int index) { }
}
