// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
namespace Rts.Sim.Core;

/// <summary>v1 command kinds (design/01_architecture.md §4). Append only; values are persisted in replays.</summary>
public enum CommandKind : byte
{
    Move = 0, AttackMove = 1, Attack = 2, Stop = 3, Guard = 4, Scatter = 5, Deploy = 6, Harvest = 7,
    Return = 8, Enter = 9, Build = 10, Place = 11, Cancel = 12, Sell = 13, Repair = 14, Rally = 15,
    SetStance = 16, PowerToggle = 17, Special = 18, Debug = 19, Chat = 20,
    /// <summary>M3: upgrade a building in place (Hatch to Bore Head). Subject = building, Arg = target building type index.</summary>
    Upgrade = 21,
}

/// <summary>
/// The only way anything outside Simulation.Step changes sim state. Fixed generic payload for v1:
/// Subject = the acting entity, Target = target entity, Cell = target cell, Arg = kind-specific int
/// (type index for Build/Place, stance for SetStance, ...). Step sorts by (Player, Sequence).
/// For Move and Stop, Arg = (int)<see cref="ActorKind"/> of the Subject (0 = Vehicle, 1 = Infantry), because an
/// EntityId alone does not say which store it indexes. Move queues a Move mission to Cell; Stop queues Stop.
/// Attack / AttackMove / Guard (M1-09): Arg low byte = subject ActorKind, Arg bits 8..15 = target ActorKind (Attack),
/// Cell = destination (AttackMove) or guard point (Guard).
/// M2 payloads: Build = (Arg = <see cref="Rules.ProductionCategory"/>, Arg2 = type index in that category; queues an item);
/// Cancel = (Arg = category, Arg2 = queue position); Place = (Arg = building type index, Cell = top-left; only a ready
/// item outside setup); Rally = (Subject = factory building; sets it as the primary factory and the rally Cell);
/// Sell = (Subject = building); Repair = (Subject = building; toggles); Harvest = (Subject = vehicle, optional Cell
/// to harvest near); Return = (Subject = harvester; go dock and unload).
/// M3 payloads (design 05 §1–§4; routing in Simulation.Transport.cs / Upgrades.cs / Effects.cs):
/// Enter = (Subject = the unit, Arg low byte = subject ActorKind (Vehicle/Infantry), Arg bits 8..15 = target ActorKind,
/// Target = what to enter). Routed by target: an own or allied vehicle = board that transport (M3-06); an own building with
/// an entrance = tunnel transit with that building as the DESTINATION (the unit walks to the nearest own entrance that fits,
/// M3-02); any other building = engineer action (capture an enemy building below half health, repair a friendly one, M3-06).
/// Deploy = (Subject = vehicle; Cell = target cell for a burrowing deployer such as the Bore, ignored otherwise). Routed by the
/// vehicle type: Passengers &gt; 0 = unload (M3-06); DeploysInto &gt;= 0 = deploy (M3-03).
/// Upgrade = (Subject = building, Arg = target building type index; cost = difference, M3-03).
/// Special = (Subject = superweapon building, Cell = target; M3-13).
/// Build with Arg = <see cref="Rules.ProductionCategory.Upgrade"/> = (Arg2 = upgrade index in SimRules.Upgrades; researched at
/// a building with Researches, M3-06); Cancel with Arg = Upgrade = (Arg2 = queue position).
/// Arg2 (M1.5, review B-8) = second kind-specific int, default 0 (Debug: see DebugOp). Sorting is total over
/// (Player, Sequence, Kind, Subject, Target, Cell, Arg, Arg2), so ties never depend on input order.
/// </summary>
public readonly record struct Command(
    int ExecTick, byte Player, ushort Sequence, CommandKind Kind,
    EntityId Subject, EntityId Target, CellCoord Cell, int Arg, int Arg2 = 0);
