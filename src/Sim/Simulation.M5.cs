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
        _directives.Store(c.Player, posture, focus, target, mix, expand, cell, Tick, DirectiveCodec.IsStrong(c.Arg));
    }

    /// <summary>The "commander_orders" part (S24, M5-12).</summary>
    public CommanderOrderState CommanderOrders => _commanderOrders;

    /// <summary>The standing orders of <paramref name="player"/>, slot 0 first (empty slots included). Allocates.</summary>
    public CommanderOrder[] CommanderOrdersOf(byte player) => _commanderOrders.AllOf(player);

    /// <summary>CommandKind.CommanderOrder (S24, M5-12, D-241): Arg = <see cref="CommanderOrderCodec.Pack"/>, Cell = target cell (kinds with a cell),
    /// DueTick = Arg2. An unknown kind or slot is dropped; Kind None clears the slot; a cell off the map or in shroud for the player drops the
    /// order (as for the directive focus cell); TechTo amount is clamped to 2..5; IssuedTick = now. The sim never acts on the stored order.</summary>
    private void ApplyCommanderOrder(Command c)
    {
        if (c.Player >= Setup.PlayerCount || _players.IsNeutral(c.Player)) return;
        var (kind, slot, amount) = CommanderOrderCodec.Unpack(c.Arg);
        if (!CommanderOrderCodec.IsValid(kind, slot)) return;
        if (kind == CommanderOrderKind.None) { _commanderOrders.Store(c.Player, slot, CommanderOrder.Empty); return; }
        CellCoord cell = Directive.NoCell;
        if (kind is CommanderOrderKind.AttackAt or CommanderOrderKind.HoldCell or CommanderOrderKind.Raid or CommanderOrderKind.ExpandTo)
        {
            if (!InMap(c.Cell) || _vision.VisionAt(c.Player, c.Cell) == 0) return;
            cell = c.Cell;
        }
        if (kind == CommanderOrderKind.TechTo) amount = Math.Clamp(amount, 2, 5);
        int due = kind == CommanderOrderKind.AttackAt ? Math.Max(0, c.Arg2) : 0;
        _commanderOrders.Store(c.Player, slot, new CommanderOrder(kind, cell, amount, due, Tick));
    }
}
