// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Sim.Core;
using Rts.Sim.Rules;

namespace Rts.Game.Controls;

/// <summary>
/// A43-QOL-15: repair-all and sell-several. Both only choose which buildings get the ordinary per-building Repair or Sell command the
/// card button already sends, so the sim sees nothing new. The sim exposes no "is repairing" flag and Repair toggles, so this keeps the
/// set of buildings it switched on itself and never toggles one of them off again; a building drops out of the set as soon as it is full,
/// gone, or stopped healing (the sim stops a repair when the owner cannot pay).
/// </summary>
public sealed class BuildingBulk
{
    private readonly SortedDictionary<int, int> _repairing = new();   // building slot index -> health seen last time
    private int _lastTick = -1;

    /// <summary>True when this helper believes the building is being repaired (it started the repair).</summary>
    public bool IsRepairing(EntityId building) => _repairing.ContainsKey(building.Index);

    /// <summary>Notes a Repair the player started by hand (the card button), so a later repair-all does not toggle it off.</summary>
    public void NoteRepair(EntityId building, int health)
    {
        if (_repairing.ContainsKey(building.Index)) _repairing.Remove(building.Index);   // that click toggled it off
        else _repairing[building.Index] = health;
    }

    /// <summary>
    /// Updates the believed set from a fresh snapshot (it acts once per sim tick, so calling it every frame is fine): drops buildings that are gone, full, or
    /// whose health did not rise since the last tick.
    /// </summary>
    public void Observe(IReadOnlyList<BuildingView> buildings, SimRules rules, byte player, int tick)
    {
        if (tick == _lastTick) return;
        _lastTick = tick;
        if (_repairing.Count == 0) return;
        foreach (int index in _repairing.Keys.ToList())
        {
            bool keep = false;
            foreach (BuildingView b in buildings)
            {
                if (b.Id.Index != index || b.Ghost || b.Player != player) continue;
                int max = rules.Buildings[b.TypeIndex].MaxHealth;
                keep = b.Health < max && b.Health > _repairing[index];
                if (keep) _repairing[index] = b.Health;
                break;
            }

            if (!keep) _repairing.Remove(index);
        }
    }

    /// <summary>
    /// The own buildings a repair-all should toggle on now: damaged, not a ghost, not already believed to be repairing, in id order.
    /// The first tick after a Repair the health has not risen yet, so those buildings count as repairing through <see cref="Started"/>.
    /// </summary>
    public List<BuildingView> RepairTargets(IReadOnlyList<BuildingView> buildings, SimRules rules, byte player)
    {
        var list = new List<BuildingView>();
        foreach (BuildingView b in buildings)
        {
            if (b.Ghost || b.Player != player || IsRepairing(b.Id)) continue;
            if ((uint)b.TypeIndex >= (uint)rules.Buildings.Count) continue;
            int max = rules.Buildings[b.TypeIndex].MaxHealth;
            if (max > 0 && b.Health > 0 && b.Health < max) list.Add(b);
        }

        list.Sort((x, y) => x.Id.Index.CompareTo(y.Id.Index));
        return list;
    }

    /// <summary>Records that repair was switched on for these buildings (health at that moment as the starting point, minus one so the first rise counts).</summary>
    public void Started(IEnumerable<BuildingView> buildings)
    {
        foreach (BuildingView b in buildings) _repairing[b.Id.Index] = b.Health - 1;
    }

    /// <summary>Forgets everything (a new game or a load).</summary>
    public void Clear() => _repairing.Clear();
}
