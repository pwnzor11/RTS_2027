namespace Rts.Sim.Core;

/// <summary>v1 command kinds (design/01_architecture.md §4). Append only; values are persisted in replays.</summary>
public enum CommandKind : byte
{
    Move = 0, AttackMove = 1, Attack = 2, Stop = 3, Guard = 4, Scatter = 5, Deploy = 6, Harvest = 7,
    Return = 8, Enter = 9, Build = 10, Place = 11, Cancel = 12, Sell = 13, Repair = 14, Rally = 15,
    SetStance = 16, PowerToggle = 17, Special = 18, Debug = 19, Chat = 20,
}

/// <summary>
/// The only way anything outside Simulation.Step changes sim state. Fixed generic payload for v1:
/// Subject = the acting entity, Target = target entity, Cell = target cell, Arg = kind-specific int
/// (type index for Build/Place, stance for SetStance, ...). Step sorts by (Player, Sequence).
/// For Move and Stop, Arg = (int)<see cref="ActorKind"/> of the Subject (0 = Vehicle, 1 = Infantry), because an
/// EntityId alone does not say which store it indexes. Move queues a Move mission to Cell; Stop queues Stop.
/// Attack / AttackMove / Guard (M1-09): Arg low byte = subject ActorKind, Arg bits 8..15 = target ActorKind (Attack),
/// Cell = destination (AttackMove) or guard point (Guard).
/// </summary>
public readonly record struct Command(
    int ExecTick, byte Player, ushort Sequence, CommandKind Kind,
    EntityId Subject, EntityId Target, CellCoord Cell, int Arg);
