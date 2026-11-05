// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Sim.Core;
using Rts.Sim.Production;
using Rts.Sim.Rules;

namespace Rts.Sim;

// Supply cap (SUP-1, docs/design/13_supply_cap.md, D-125/D-129). No state part: supply used and the cap are pure functions of the
// hashed units, infantry, buildings and rigs parts, derived by an integer scan in slot order. The result is cached per tick in scratch
// fields (not state, not saved); the cache is also dropped by every lifecycle hook that can change it (spawn, removal, placement,
// capture, pack start/finish), so a query in the middle of a tick never sees a stale value.
public sealed partial class Simulation
{
    // Scratch cache, per player. Rewritten by EnsureSupply; never read without it.
    private int[] _supplyUsed = Array.Empty<int>();
    private int[] _supplyCap = Array.Empty<int>();
    private int _supplyStamp = -1;
    private bool _supplyDirty = true;

    /// <summary>The [supply] constants (game.toml). A global block, read from the base rules.</summary>
    internal SupplyRules SupplyRulesOf => Setup.Rules.Supply;   // the only read of Setup.Rules.Supply (RulesOfSweepGrepTests)

    /// <summary>True when the supply cap is switched on for this game (game.toml <c>[supply] enabled</c>).</summary>
    public bool SupplyEnabled => SupplyRulesOf.Enabled;

    /// <summary>Drops the per-tick supply cache. Called by the lifecycle hooks that change who owns what.</summary>
    private void SupplyChanged() => _supplyDirty = true;

    /// <summary>
    /// Supply the player's live infantry and vehicles use: every alive soldier and vehicle slot of the owner counts, including
    /// passengers, garrisoned and underground units and packed rigs. 0 while supply is disabled.
    /// </summary>
    public int SupplyUsed(byte player)
    {
        if (!SupplyEnabled || player >= _players.Count) return 0;
        EnsureSupply();
        return _supplyUsed[player];
    }

    /// <summary>
    /// The player's supply cap: the sum of <c>supply_provided</c> over its completed, unpacked, alive buildings, at most the ceiling.
    /// 0 while supply is disabled.
    /// </summary>
    public int SupplyCap(byte player)
    {
        if (!SupplyEnabled || player >= _players.Count) return 0;
        EnsureSupply();
        return _supplyCap[player];
    }

    /// <summary>The HUD view of the player's supply; all zero while supply is disabled.</summary>
    public SupplyView SupplyViewFor(byte player) =>
        SupplyEnabled ? new SupplyView(SupplyUsed(player), SupplyCap(player), SupplyRulesOf.Ceiling) : default;

    /// <summary>Supply one unit of the category and type index costs (0 for structures, or while supply is disabled).</summary>
    public int SupplyOfType(ProductionCategory category, int typeIndex)
    {
        SupplyRules sr = SupplyRulesOf;
        if (!sr.Enabled) return 0;
        SimRules rules = Setup.Rules;
        switch (category)
        {
            case ProductionCategory.Infantry:
                if (rules.Infantry == null || typeIndex < 0 || typeIndex >= rules.Infantry.Count) return 0;
                return sr.SupplyOf(rules.Infantry[typeIndex].Cost, rules.Infantry[typeIndex].Supply);
            case ProductionCategory.Vehicle:
            case ProductionCategory.Aircraft:
                if (typeIndex < 0 || typeIndex >= rules.Vehicles.Count) return 0;
                return sr.SupplyOf(rules.Vehicles[typeIndex].Cost, rules.Vehicles[typeIndex].Supply);
            default:
                return 0;
        }
    }

    private void EnsureSupply()
    {
        if (!_supplyDirty && _supplyStamp == Tick) return;
        int n = _players.Count;
        if (_supplyUsed.Length != n) { _supplyUsed = new int[n]; _supplyCap = new int[n]; }
        Array.Clear(_supplyUsed); Array.Clear(_supplyCap);
        SimRules rules = Setup.Rules;
        SupplyRules sr = SupplyRulesOf;
        for (int i = 1; i < _units.Capacity; i++)
        {
            if (!_units.Slots.IsIndexAlive(i)) continue;
            byte p = _units.Player[i];
            if (p >= n) continue;
            VehicleType vt = rules.Vehicles[_units.TypeIndex[i]];
            _supplyUsed[p] += sr.SupplyOf(vt.Cost, vt.Supply);
        }
        for (int i = 1; i < _infantry.Capacity; i++)
        {
            if (!_infantry.Slots.IsIndexAlive(i)) continue;
            byte p = _infantry.Player[i];
            if (p >= n) continue;
            InfantryType it = rules.Infantry[_infantry.TypeIndex[i]];
            _supplyUsed[p] += sr.SupplyOf(it.Cost, it.Supply);
        }
        for (int i = 1; i < _buildings.Capacity; i++)
        {
            if (!_buildings.Slots.IsIndexAlive(i) || _rigs.PackTicksLeft[i] != 0) continue;   // a packing rig stops counting at once
            byte p = _buildings.Player[i];
            if (p >= n) continue;
            _supplyCap[p] += rules.Buildings[_buildings.TypeIndex[i]].SupplyProvided;
        }
        for (int p = 0; p < n; p++) _supplyCap[p] = Math.Min(_supplyCap[p], sr.Ceiling);
        _supplyStamp = Tick;
        _supplyDirty = false;
    }

    /// <summary>Supply of every unit waiting in the player's three unit queues (the front item included).</summary>
    private int QueuedSupply(byte player)
    {
        int total = 0;
        for (int k = 0; k <= (int)ProductionCategory.Defence; k++)
        {
            var category = (ProductionCategory)k;
            if (!IsUnitCategory(category)) continue;
            int lane = ProductionState.Lane(player, category);
            for (int q = 0; q < _production.QueueCount[lane]; q++)
                total += SupplyOfType(category, _production.QueueType[lane * _production.MaxQueue + q]);
        }
        return total;
    }

    /// <summary>Enqueue test: a unit of this supply on top of what is used and already queued would exceed the cap.</summary>
    private bool SupplyBlocksEnqueue(byte player, ProductionCategory category, int typeIndex)
    {
        if (!SupplyEnabled || !IsUnitCategory(category)) return false;
        int s = SupplyOfType(category, typeIndex);
        if (s <= 0) return false;   // free units are never blocked
        EnsureSupply();
        return _supplyUsed[player] + QueuedSupply(player) + s > _supplyCap[player];
    }

    /// <summary>Queue-head test: the unit fits when used + its supply is within the cap (queued items do not count against it).</summary>
    private bool SupplyFits(byte player, int supply)
    {
        if (supply <= 0) return true;
        EnsureSupply();
        return _supplyUsed[player] + supply <= _supplyCap[player];
    }
}
