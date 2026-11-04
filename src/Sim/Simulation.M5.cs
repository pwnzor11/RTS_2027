// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Sim.Core;
using Rts.Sim.Directives;

namespace Rts.Sim;

// M5-01 (docs/design/09_m5_plan.md §2–§3): the commander directive. The sim stores the latest directive per player and never acts on it.
public sealed partial class Simulation
{
    /// <summary>The "directive" part (M5-01).</summary>
    public DirectiveState Directives => _directives;

    /// <summary>The latest directive of <paramref name="player"/>, or <see cref="Directive.None"/>.</summary>
    public Directive DirectiveOf(byte player) => _directives.Of(player);

    /// <summary>CommandKind.Directive: Arg = <see cref="DirectiveCodec.Pack"/>, Cell = focus cell or <see cref="Directive.NoCell"/>.
    /// Values are clamped (<see cref="DirectiveCodec.Clamp"/>); a focus cell off the map or still in shroud for the player is dropped,
    /// so a directive never carries information the player does not have. Neutral slots ignore directives.</summary>
    private void ApplyDirective(Command c)
    {
        if (c.Player >= Setup.PlayerCount || _players.IsNeutral(c.Player)) return;
        var (posture, focus, target, mix, expand) = DirectiveCodec.Unpack(c.Arg);
        (posture, focus, target, mix) = DirectiveCodec.Clamp(posture, focus, target, mix, c.Player, Setup.PlayerCount, _players.IsNeutral);
        CellCoord cell = c.Cell;
        if (!InMap(cell) || _vision.VisionAt(c.Player, cell) == 0) cell = Directive.NoCell;
        _directives.Store(c.Player, posture, focus, target, mix, expand, cell, Tick);
    }
}
