using Rts.Sim.Core;
using Rts.Sim.Missions;

namespace Rts.Sim;

// Movement part of Simulation. OWNER: M1-08 (movement and locomotion). Lead-created stub.
public sealed partial class Simulation
{
    /// <summary>Move / Stop / Scatter commands (Subject kind in Command.Arg, see Command.cs).</summary>
    private void ApplyMovementCommand(Command c)
    {
        switch (c.Kind)
        {
            case CommandKind.Move:
                if (OwnsSubject(c, out var store) && InMap(c.Cell))
                    Queue(store!, c.Subject.Index, MissionKind.Move, c.Cell, default);
                break;
            case CommandKind.Stop:
                if (OwnsSubject(c, out store)) Queue(store!, c.Subject.Index, MissionKind.Stop, default, default);
                break;
        }
    }

    /// <summary>Movement system: runs once per tick after missions. Path requests are solved here.</summary>
    private void RunMovement()
    {
        _paths.Process();
    }

    /// <summary>Called after a vehicle or infantry slot is created (spawn or production). M1-08: claim occupancy.</summary>
    private void OnActorSpawned(ActorKind kind, int index) { }

    /// <summary>Called just before a dead vehicle or infantry slot is freed. M1-08: release occupancy; M1-09: drop targets.</summary>
    private void OnActorRemoving(ActorKind kind, int index) { OnCombatActorRemoving(kind, index); }
}
