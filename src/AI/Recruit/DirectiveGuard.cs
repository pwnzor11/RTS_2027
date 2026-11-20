// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Sim.Core;

namespace Rts.AI.Recruit;

/// <summary>
/// S36 (M5-14, D-278): the commander's directives are followed only where they do not undercut the AI's own plan. The s36 paired ablation
/// (docs/balance/s36_balance.md) found that the parts which hold credits or the army back cost the most: a HoldCell order
/// (no waves), a Reserve order, a TechTo order and the focus values that keep a unit reserve and pull the tier gates forward. So until the AI
/// has the army its own plan wants before tech spending (<see cref="TierPlanner.CommandArmyReady"/>), or while its army is behind the enemy
/// army it has seen (<see cref="TierPlanner.CommandBehind"/>), HoldCell, Reserve, TechTo and the reserve/early-gate parts of the focus are
/// dropped for the decision and the AI plays its plain values; once they apply, a HoldCell order only moves the rally (the AI's own waves
/// still launch). Posture, mix, expand, target, focus cell, AttackAt, Raid and ExpandTo always stay. Stateless: a pure function of
/// this decision's directive, orders and armies, so nothing is saved.
/// </summary>
internal static class DirectiveGuard
{
    /// <summary>A HoldCell order stops the AI's own waves (S24 behaviour) when true; when false it only moves the rally and the idle army there.</summary>
    public const bool HoldBlocksWaves = false;   // s36: false measured better (G2: 29 better / 10 worse games than S24 on the paired hold row)

    /// <summary>The directive knobs and orders for a decision in which the AI is behind or has no army yet.</summary>
    public static (DirectiveBias Bias, IReadOnlyList<CommanderOrder> Orders) WhileBehind(DirectiveBias bias, IReadOnlyList<CommanderOrder> orders)
    {
        DirectiveBias plain = DirectiveBias.Plain;
        bias = bias with
        {
            UnitReserve = plain.UnitReserve, GatePermille = plain.GatePermille, Tier3ArmyPermille = plain.Tier3ArmyPermille, ForceTech = plain.ForceTech,
            EarlyLateTech = plain.EarlyLateTech, UpgradeMinArmy = plain.UpgradeMinArmy, UpgradeCushion = plain.UpgradeCushion,
        };
        bool drop = false;
        foreach (CommanderOrder o in orders) drop |= Dropped(o.Kind);
        if (!drop) return (bias, orders);
        var kept = new CommanderOrder[orders.Count];
        for (int i = 0; i < orders.Count; i++) kept[i] = Dropped(orders[i].Kind) ? CommanderOrder.Empty : orders[i];   // same slots, so slot memories stay aligned
        return (bias, kept);
    }

    /// <summary>True for the order kinds dropped while the guard holds.</summary>
    internal static bool IsHeldBack(CommanderOrderKind kind) => Dropped(kind);

    private static bool Dropped(CommanderOrderKind kind) =>
        kind is CommanderOrderKind.Reserve or CommanderOrderKind.TechTo or CommanderOrderKind.HoldCell;
}
