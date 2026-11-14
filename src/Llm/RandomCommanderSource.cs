// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.AI;
using Rts.Sim.Core;
using Rts.Sim.View;

namespace Rts.Llm;

/// <summary>
/// S24 (M5-12): a seeded random stand-in for <see cref="Commander"/>, for the 32-game swap test (model against random). It makes a
/// decision on the same call schedule as the commander (the first at tick 600, then every 600 ticks) and returns the same kinds of
/// values: a directive and up to four orders, already inside the parser's ranges. The sequence depends only on the seed and the
/// decision count, so one seed gives one game. It never calls a network and holds no key. Game thread only.
/// </summary>
public sealed class RandomCommanderSource : IDirectiveSource
{
    /// <summary>The stream of <see cref="SimRandom"/> this source draws from (separate from the match's own streams).</summary>
    private const ulong Stream = 0x5A24UL;

    private const int MaxOrderAmount = 3000;
    private const int MaxMinute = 10;

    private readonly SimRandom _rng;
    private readonly int _mapCells;
    private readonly List<OrderDraft> _orders = new();
    private int _nextTick = Commander.CadenceTicks;
    private CommanderReply? _decision;

    /// <summary>Creates the source for <paramref name="seed"/> on a map of <paramref name="mapCells"/> cells per side.</summary>
    public RandomCommanderSource(ulong seed, int mapCells)
    {
        if (mapCells < 1) throw new ArgumentOutOfRangeException(nameof(mapCells));
        _rng = new SimRandom(seed, Stream);
        _mapCells = mapCells;
    }

    /// <inheritdoc />
    public bool Poll(PlayerView view, out int packedArg, out CellCoord focusCell)
    {
        packedArg = 0;
        focusCell = Directive.NoCell;
        if (view.Tick < _nextTick) return false;
        _nextTick = view.Tick + Commander.CadenceTicks;

        CommanderReply reply = Next();
        _decision = reply;
        _orders.AddRange(reply.OrderList);
        DirectiveDraft d = reply.Directive!.Value;
        packedArg = DirectiveCodec.PackStrong(DirectiveCodec.Pack(d.Posture, d.Focus, d.TargetPlayer, d.Mix, d.Expand), d.StrongMix);
        focusCell = d.FocusX < 0 || d.FocusY < 0 ? Directive.NoCell : new CellCoord(d.FocusX, d.FocusY);
        return true;
    }

    /// <summary>The decision made by the last <see cref="Poll"/> that made one, once (null after it is taken).</summary>
    public CommanderReply? TakeDecision()
    {
        CommanderReply? r = _decision;
        _decision = null;
        return r;
    }

    /// <summary>The orders decided since the last call, oldest first, then cleared (same contract as <see cref="Commander.TakeOrders"/>).</summary>
    public IReadOnlyList<OrderDraft> TakeOrders()
    {
        OrderDraft[] taken = _orders.ToArray();
        _orders.Clear();
        return taken;
    }

    /// <summary>Draws one decision: a directive (every field inside its range) and zero to four orders. Advances the random stream.</summary>
    public CommanderReply Next()
    {
        bool hasCell = _rng.Next(2) == 1;
        var directive = new DirectiveDraft(
            Posture: (byte)_rng.Next(DirectiveCodec.PostureAllIn + 1),
            Focus: (byte)_rng.Next(DirectiveCodec.FocusSuperweapon + 1),
            TargetPlayer: DirectiveCodec.AnyTarget,
            Mix: (byte)_rng.Next(DirectiveCodec.MixAntiInfantry + 1),
            Expand: _rng.Next(2) == 1,
            FocusX: hasCell ? (short)_rng.Next(_mapCells) : (short)-1,
            FocusY: hasCell ? (short)_rng.Next(_mapCells) : (short)-1,
            StrongMix: _rng.Next(2) == 1);

        var orders = new List<OrderDraft>();
        int count = _rng.Next(CommanderOrderCodec.Slots + 1);
        for (int i = 0; i < count; i++) orders.Add(RandomOrder());
        return new CommanderReply(directive, null, orders);
    }

    private OrderDraft RandomOrder()
    {
        byte slot = (byte)_rng.Next(CommanderOrderCodec.Slots);
        var kind = (CommanderOrderKind)_rng.Next((int)CommanderOrderKind.Reserve + 1);
        bool needsCell = kind is CommanderOrderKind.AttackAt or CommanderOrderKind.HoldCell or CommanderOrderKind.Raid or CommanderOrderKind.ExpandTo;
        short x = -1, y = -1;
        if (needsCell)
        {
            x = (short)_rng.Next(_mapCells);
            y = (short)_rng.Next(_mapCells);
        }
        int amount = kind switch
        {
            CommanderOrderKind.AttackAt => 200 + _rng.Next(MaxOrderAmount),
            CommanderOrderKind.Raid => 1 + _rng.Next(8),
            CommanderOrderKind.TechTo => 2 + _rng.Next(4),
            CommanderOrderKind.Reserve => _rng.Next(1000),
            _ => 0,
        };
        int minute = kind == CommanderOrderKind.AttackAt ? _rng.Next(MaxMinute + 1) : 0;
        return new OrderDraft(slot, kind, x, y, amount, minute);
    }
}
