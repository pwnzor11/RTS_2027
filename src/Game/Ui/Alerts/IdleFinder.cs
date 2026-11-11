// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Sim.Core;
using Rts.Sim.Rules;
using Rts.Sim.View;

namespace Rts.Game.Ui.Alerts;

/// <summary>
/// Finds the local player's idle harvesters and idle factories from the own-actor list (M13-15, D-205). A harvester is idle when the sim
/// says its mission is none, sleep, guard or stop with nothing queued (<see cref="ViewActor.Idle"/>). A factory is idle when it builds
/// infantry, vehicles or aircraft and the production lane of that category has nothing queued. Structures and defences are not counted (the
/// Construction Yard would always be "idle"). Reads views only.
/// </summary>
public static class IdleFinder
{
    /// <summary>The production lanes that count as factories.</summary>
    public static readonly IReadOnlyList<ProductionCategory> UnitLanes = new[] { ProductionCategory.Infantry, ProductionCategory.Vehicle, ProductionCategory.Aircraft };

    /// <summary>True for a living own vehicle whose type can harvest.</summary>
    public static bool IsHarvester(ViewActor a, SimRules rules) =>
        a.Kind == ActorKind.Vehicle && !a.Ghost && a.Health > 0 && a.TypeIndex >= 0 && a.TypeIndex < rules.Vehicles.Count
        && rules.Vehicles[a.TypeIndex].HarvestCapacity > 0;

    /// <summary>Appends the idle harvesters of <paramref name="own"/> to <paramref name="dest"/>, in list (slot) order. Carried, underground and docked ones are left out.</summary>
    public static void Harvesters(IReadOnlyList<ViewActor> own, SimRules rules, List<ViewActor> dest)
    {
        foreach (ViewActor a in own)
        {
            if (IsHarvester(a, rules) && a.Idle && !a.Underground) dest.Add(a);
        }
    }

    /// <summary>The production lane's queue, or null when the lane has none.</summary>
    public static QueueView? LaneOf(PlayerHud hud, ProductionCategory category)
    {
        foreach (QueueView q in hud.Queues) if (q.Category == category) return q;
        return null;
    }

    /// <summary>True when the lane of <paramref name="category"/> has nothing queued (and no finished item waiting).</summary>
    public static bool LaneIsEmpty(PlayerHud hud, ProductionCategory category) =>
        LaneOf(hud, category) is not { } q || q.Items.Count == 0;

    /// <summary>Appends the own factory buildings whose lane is empty to <paramref name="dest"/>, in list (slot) order.</summary>
    public static void Factories(IReadOnlyList<ViewActor> own, SimRules rules, PlayerHud hud, List<ViewActor> dest)
    {
        foreach (ViewActor a in own)
        {
            if (a.Kind != ActorKind.Building || a.Ghost || a.TypeIndex < 0 || a.TypeIndex >= rules.Buildings.Count) continue;
            ProductionCategory? makes = rules.Buildings[a.TypeIndex].Produces;
            if (makes is not { } c || !UnitLanes.Contains(c)) continue;
            if (LaneIsEmpty(hud, c)) dest.Add(a);
        }
    }

    /// <summary>The next entry after <paramref name="last"/> in slot order, wrapping round; the first when <paramref name="last"/> is not in the list. Null for an empty list.</summary>
    public static ViewActor? Next(IReadOnlyList<ViewActor> idle, EntityId last)
    {
        if (idle.Count == 0) return null;
        for (int i = 0; i < idle.Count; i++)
        {
            if (idle[i].Id == last) return idle[(i + 1) % idle.Count];
        }

        return idle[0];
    }
}
