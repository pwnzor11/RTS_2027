// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Sim.Core;
using Rts.Sim.Rules;
using Rts.Sim.View;

namespace Rts.Llm;

/// <summary>S47 (M16-A-08): where a standing commander order stands, as far as the commander can tell from its own view.</summary>
public enum OrderState : byte
{
    /// <summary>A timed attack whose minute has not come.</summary>
    Pending,
    /// <summary>The minute has come but the army is worth less than the order asked.</summary>
    Waiting,
    /// <summary>The army was strong enough at the due minute and no unit has reached the cell yet.</summary>
    Marching,
    /// <summary>A standing order (hold, raid, reserve) that is in force.</summary>
    Active,
    /// <summary>Reached: an army unit stands at the attack cell, the tier is reached, the base stands near the cell.</summary>
    Done,
    /// <summary>The attack lost most of its army, or no unit reached the cell in time.</summary>
    Failed,
}

/// <summary>S47: one line of the "YOUR ORDERS" block. <see cref="Detail"/> is short plain text with the numbers.</summary>
public readonly record struct OrderStatus(int Slot, CommanderOrderKind Kind, OrderState State, string Detail, CellCoord Cell);

/// <summary>Memory behind one slot's status; lives in <see cref="SummaryHistory"/> (LLM layer only, never saved).</summary>
internal sealed class OrderTrack
{
    public int IssuedTick;
    public CommanderOrderKind Kind;
    public bool Launched, Final;
    public int LaunchTick, LaunchValue;
    public OrderState FinalState;
    public string FinalDetail = "";
}

/// <summary>S47 (M16-A-08): works out <see cref="OrderStatus"/> for each standing order from the view. The sim reports no "done" or "failed", so
/// these are estimates: an attack counts as launched once the army is worth the order's amount at or after the due tick, as done when an own
/// army unit stands within <see cref="NearCells"/> of the cell, and as failed when the army has lost more than 60 % of its value since the
/// launch or no unit has arrived <see cref="GiveUpTicks"/> after it. Done and failed stay until the slot is re-issued or cleared.</summary>
internal static class OrderTracker
{
    public const int NearCells = 5;
    public const int RaidNearCells = 8;
    public const int GiveUpTicks = 5 * CounterHold.TicksPerMinute;
    public const int FailPercent = 40;   // army value left, percent of the value at launch, below which the attack counts as failed

    public static IReadOnlyList<OrderStatus> Update(PlayerView view, List<ViewActor> own, SortedDictionary<int, OrderTrack> tracks)
    {
        IReadOnlyList<CommanderOrder> orders = view.CommanderOrders;
        var result = new List<OrderStatus>();
        if (orders.Count == 0 && tracks.Count == 0) return result;
        int tick = view.Tick;
        SimRules rules = view.Rules;
        int value = 0, armyUnits = 0;
        var army = new List<ViewActor>();
        foreach (ViewActor a in own)
        {
            if (!a.IsMobile || !IsFighter(rules, a)) continue;
            army.Add(a);
            value += a.Kind == ActorKind.Vehicle ? rules.Vehicles[a.TypeIndex].Cost : rules.Infantry[a.TypeIndex].Cost;
            armyUnits++;
        }

        for (int slot = 0; slot < orders.Count && slot < CommanderOrderCodec.Slots; slot++)
        {
            CommanderOrder o = orders[slot];
            if (!o.IsSet) { tracks.Remove(slot); continue; }
            if (!tracks.TryGetValue(slot, out OrderTrack? t) || t.IssuedTick != o.IssuedTick || t.Kind != o.Kind)
                tracks[slot] = t = new OrderTrack { IssuedTick = o.IssuedTick, Kind = o.Kind };
            (OrderState state, string detail) = t.Final ? (t.FinalState, t.FinalDetail) : Evaluate(view, o, t, own, army, value, armyUnits, tick);
            if (!t.Final && state is OrderState.Done or OrderState.Failed) { t.Final = true; t.FinalState = state; t.FinalDetail = detail; }
            result.Add(new OrderStatus(slot, o.Kind, state, detail, o.Cell));
        }
        return result;
    }

    private static (OrderState, string) Evaluate(PlayerView view, CommanderOrder o, OrderTrack t, List<ViewActor> own, List<ViewActor> army,
        int value, int armyUnits, int tick)
    {
        switch (o.Kind)
        {
            case CommanderOrderKind.AttackAt:
            {
                int near = CountNear(army, o.Cell, NearCells);
                if (t.Launched && near > 0) return (OrderState.Done, $"{near} units reached the cell");
                if (tick < o.DueTick) return (OrderState.Pending, $"launches at {Clock(o.DueTick)} if the army is worth {o.Amount}cr (now {value}cr)");
                if (!t.Launched)
                {
                    if (value < o.Amount) return (OrderState.Waiting, $"due {Clock(o.DueTick)} passed, army {value}cr is short of {o.Amount}cr");
                    t.Launched = true; t.LaunchTick = tick; t.LaunchValue = value;
                    if (near > 0) return (OrderState.Done, $"{near} units are at the cell");
                }
                if (t.LaunchValue > 0 && (long)value * 100 < (long)t.LaunchValue * FailPercent)
                    return (OrderState.Failed, $"army down from {t.LaunchValue}cr to {value}cr, none reached the cell");
                if (tick - t.LaunchTick > GiveUpTicks) return (OrderState.Failed, "no unit reached the cell within 5 minutes of the launch");
                return (OrderState.Marching, $"launched at {Clock(t.LaunchTick)} with {t.LaunchValue}cr, army now {value}cr");
            }
            case CommanderOrderKind.HoldCell:
                return (OrderState.Active, $"{CountNear(army, o.Cell, OrderTrackerHold)} of {armyUnits} army units are in place");
            case CommanderOrderKind.Raid:
                return (OrderState.Active, $"raid of {o.Amount} units; {CountNear(army, o.Cell, RaidNearCells)} army units near the target");
            case CommanderOrderKind.ExpandTo:
            {
                foreach (ViewActor a in own)
                    if (a.Kind == ActorKind.Building && Cheb(a.Cell, o.Cell) <= RaidNearCells) return (OrderState.Done, "a structure of yours stands near the cell");
                return (OrderState.Active, "no structure of yours near the cell yet");
            }
            case CommanderOrderKind.TechTo:
                return view.Tier >= o.Amount ? (OrderState.Done, $"tier {view.Tier} reached") : (OrderState.Active, $"tier {view.Tier} of {o.Amount}");
            default:   // Reserve
                return (OrderState.Active, $"keeping {o.Amount}cr; cash now {view.Cash}cr");
        }
    }

    private const int OrderTrackerHold = 5;   // the AI's own HoldRadiusCells (MilitaryManager); read-only mirror, the AI is not referenced from here

    private static bool IsFighter(SimRules rules, ViewActor a) => a.Kind == ActorKind.Infantry
        ? !rules.Infantry[a.TypeIndex].Engineer && rules.Infantry[a.TypeIndex].PrimaryWeapon >= 0
        : rules.Vehicles[a.TypeIndex].HarvestCapacity == 0 && rules.Vehicles[a.TypeIndex].PrimaryWeapon >= 0;

    private static int CountNear(List<ViewActor> army, CellCoord cell, int radius)
    {
        int n = 0;
        foreach (ViewActor a in army) if (Cheb(a.Cell, cell) <= radius) n++;
        return n;
    }

    private static int Cheb(CellCoord a, CellCoord b) => Math.Max(Math.Abs(a.X - b.X), Math.Abs(a.Y - b.Y));

    private static string Clock(int tick) => $"{tick / 1200:00}:{tick / 20 % 60:00}";
}
