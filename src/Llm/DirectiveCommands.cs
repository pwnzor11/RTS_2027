// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Sim.Core;

namespace Rts.Llm;

/// <summary>M5-01: converts between the LLM's <see cref="DirectiveDraft"/> and the sim's <see cref="CommandKind.Directive"/>.
/// S24 (M5-12): also builds one <see cref="CommanderOrderCodec"/> command per <see cref="OrderDraft"/>.</summary>
public static class DirectiveCommands
{
    /// <summary>Game ticks per second (SimConstants): an order's minute becomes a due tick at 60 * 20 ticks per minute.</summary>
    public const int TicksPerSecond = 20;

    /// <summary>Command sequence numbers the order commands start at when a caller has no better range. The driver's own
    /// sequence counts up from 0, so an order never collides with a directive of the same player unless 32 768 commands pass first.</summary>
    public const ushort OrderSequenceBase = 0x8000;

    /// <summary>The command carrying <paramref name="draft"/> for <paramref name="player"/>. A draft without a focus cell
    /// (FocusX or FocusY negative) sends <see cref="Directive.NoCell"/>. The sim clamps values and drops an unexplored cell.
    /// S24: the strong-mix flag is packed with <see cref="DirectiveCodec.PackStrong"/>.</summary>
    public static Command ToCommand(this DirectiveDraft draft, int execTick, byte player, ushort sequence)
    {
        CellCoord cell = draft.FocusX < 0 || draft.FocusY < 0 ? Directive.NoCell : new CellCoord(draft.FocusX, draft.FocusY);
        Command c = DirectiveCodec.ToCommand(execTick, player, sequence, draft.Posture, draft.Focus, draft.TargetPlayer, draft.Mix, draft.Expand, cell);
        return c with { Arg = DirectiveCodec.PackStrong(c.Arg, draft.StrongMix) };
    }

    /// <summary>S24 (M5-12): appends one <see cref="CommanderOrderCodec"/> command per order of <paramref name="reply"/> to
    /// <paramref name="outCommands"/>, for <paramref name="player"/>, executing at <paramref name="execTick"/>. The sequence
    /// counter is advanced once per command. A slot the reply does not mention gets no command (its order stays as it is);
    /// kind None clears the slot. AttackAt gets the due tick of its minute (0 = at once).</summary>
    public static void AddOrderCommands(this CommanderReply reply, List<Command> outCommands, int execTick, byte player, ref ushort sequence)
    {
        ArgumentNullException.ThrowIfNull(outCommands);
        foreach (OrderDraft o in reply.OrderList)
        {
            CellCoord cell = o.CellX < 0 || o.CellY < 0 ? Directive.NoCell : new CellCoord(o.CellX, o.CellY);
            int due = o.Kind == CommanderOrderKind.AttackAt ? o.Minute * 60 * TicksPerSecond : 0;
            outCommands.Add(CommanderOrderCodec.ToCommand(execTick, player, sequence, o.Kind, o.Slot, cell, o.Amount, due));
            unchecked { sequence++; }
        }
    }

    /// <summary>The draft a stored directive came from (for "last 3 directives" in the prompt). <see cref="Directive.None"/> gives null.</summary>
    public static DirectiveDraft? ToDraft(Directive d) => !d.Set
        ? null
        : new DirectiveDraft(d.Posture, d.Focus, d.TargetPlayer, d.Mix, d.Expand,
            (short)(d.HasFocusCell ? d.FocusCell.X : -1), (short)(d.HasFocusCell ? d.FocusCell.Y : -1));
}
