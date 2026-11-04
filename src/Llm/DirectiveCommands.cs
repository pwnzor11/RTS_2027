// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Sim.Core;

namespace Rts.Llm;

/// <summary>M5-01: converts between the LLM's <see cref="DirectiveDraft"/> and the sim's <see cref="CommandKind.Directive"/>.</summary>
public static class DirectiveCommands
{
    /// <summary>The command carrying <paramref name="draft"/> for <paramref name="player"/>. A draft without a focus cell
    /// (FocusX or FocusY negative) sends <see cref="Directive.NoCell"/>. The sim clamps values and drops an unexplored cell.</summary>
    public static Command ToCommand(this DirectiveDraft draft, int execTick, byte player, ushort sequence)
    {
        CellCoord cell = draft.FocusX < 0 || draft.FocusY < 0 ? Directive.NoCell : new CellCoord(draft.FocusX, draft.FocusY);
        return DirectiveCodec.ToCommand(execTick, player, sequence, draft.Posture, draft.Focus, draft.TargetPlayer, draft.Mix, draft.Expand, cell);
    }

    /// <summary>The draft a stored directive came from (for "last 3 directives" in the prompt). <see cref="Directive.None"/> gives null.</summary>
    public static DirectiveDraft? ToDraft(Directive d) => !d.Set
        ? null
        : new DirectiveDraft(d.Posture, d.Focus, d.TargetPlayer, d.Mix, d.Expand,
            (short)(d.HasFocusCell ? d.FocusCell.X : -1), (short)(d.HasFocusCell ? d.FocusCell.Y : -1));
}
