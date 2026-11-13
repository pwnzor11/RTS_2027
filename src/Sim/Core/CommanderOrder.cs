// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
namespace Rts.Sim.Core;

/// <summary>S24 (M5-12, D-241): what a <see cref="CommanderOrder"/> asks the tiered AI to do. Append only: values are persisted.</summary>
public enum CommanderOrderKind : byte
{
    /// <summary>Empty slot (also: "clear this slot").</summary>
    None = 0,
    /// <summary>Attack <see cref="CommanderOrder.Cell"/> at <see cref="CommanderOrder.DueTick"/> with at least Amount army value (credits).</summary>
    AttackAt = 1,
    /// <summary>Hold <see cref="CommanderOrder.Cell"/> with the main army until the slot is cleared (Amount unused).</summary>
    HoldCell = 2,
    /// <summary>Send a separate raid group of Amount units at <see cref="CommanderOrder.Cell"/> (harass; the main army stays).</summary>
    Raid = 3,
    /// <summary>Found a new base (expansion) at or near <see cref="CommanderOrder.Cell"/> now.</summary>
    ExpandTo = 4,
    /// <summary>Tech up to tier Amount (2..5) now: the tier gates come before units.</summary>
    TechTo = 5,
    /// <summary>Keep Amount credits in hand (spending reserve); units and structures leave it untouched.</summary>
    Reserve = 6,
}

/// <summary>
/// S24 (M5-12, D-241): one standing commander order in one of a player's <see cref="CommanderOrderCodec.Slots"/> slots. Arrives as
/// <see cref="CommandKind.CommanderOrder"/>, stored in the "commander_orders" state part, read by the AI through its player view.
/// Like <see cref="Directive"/> it is an AI input only (D-110): the sim never acts on it, and the AI falls back to its plain tier when no
/// order is set. Integer fields only.
/// </summary>
public readonly record struct CommanderOrder(
    CommanderOrderKind Kind,
    CellCoord Cell,     // Directive.NoCell when the kind has no cell (TechTo, Reserve)
    int Amount,         // army value in credits (AttackAt), units (Raid), tier (TechTo), credits (Reserve); 0 otherwise
    int DueTick,        // AttackAt: the tick to launch; others: 0
    int IssuedTick)     // the tick the order was applied
{
    /// <summary>An empty slot.</summary>
    public static readonly CommanderOrder Empty = new(CommanderOrderKind.None, Directive.NoCell, 0, 0, 0);

    /// <summary>True when the slot holds an order.</summary>
    public bool IsSet => Kind != CommanderOrderKind.None;
}

/// <summary>S24 (M5-12): constants and the packing of a <see cref="CommanderOrder"/> into a <see cref="Command"/>.</summary>
public static class CommanderOrderCodec
{
    /// <summary>Standing order slots per player.</summary>
    public const int Slots = 4;

    /// <summary>Largest Amount that packs (24 bits).</summary>
    public const int MaxAmount = 0xFFFFFF;

    /// <summary>Packs Arg: bits 0..3 kind, 4..7 slot (0..3), 8..31 amount (clamped to 0..<see cref="MaxAmount"/>).</summary>
    public static int Pack(CommanderOrderKind kind, int slot, int amount) =>
        ((byte)kind & 0xF) | (slot & 0xF) << 4 | Math.Clamp(amount, 0, MaxAmount) << 8;

    /// <summary>Unpacks <see cref="Pack"/> (no clamping of kind or slot).</summary>
    public static (CommanderOrderKind Kind, int Slot, int Amount) Unpack(int arg) =>
        ((CommanderOrderKind)(arg & 0xF), arg >> 4 & 0xF, (int)((uint)arg >> 8));

    /// <summary>Builds the command. <paramref name="cell"/> = <see cref="Directive.NoCell"/> for kinds without a cell; <paramref name="dueTick"/> 0 = at once.</summary>
    public static Command ToCommand(int execTick, byte player, ushort sequence, CommanderOrderKind kind, int slot, CellCoord cell, int amount, int dueTick) =>
        new(execTick, player, sequence, CommandKind.CommanderOrder, default, default, cell, Pack(kind, slot, amount), dueTick);

    /// <summary>The sim's clamp on apply: an unknown kind or a slot outside 0..3 drops the command (returns false); TechTo amount is clamped
    /// to 2..5; AttackAt with a DueTick in the past launches at once. The cell check (on the map and explored) is the sim's, as for directives.</summary>
    public static bool IsValid(CommanderOrderKind kind, int slot) => kind <= CommanderOrderKind.Reserve && slot is >= 0 and < Slots;
}
