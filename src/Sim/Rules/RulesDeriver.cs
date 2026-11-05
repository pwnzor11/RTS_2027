// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using System.Runtime.CompilerServices;

namespace Rts.Sim.Rules;

/// <summary>
/// Per-player derived rules (design 08 §4, M4-04): the base <see cref="SimRules"/> with the effects of every researched upgrade
/// applied — upgrades in index order, each upgrade's <see cref="UpgradeType.Effects"/> in list order. Ops: <c>set</c>, <c>add</c>,
/// <c>mul</c> (per mille); <c>enable</c> is reserved and ignored. Targets: a vehicle/infantry/building type id, a provides name
/// (every building that provides it), <c>class:vehicle|infantry|building</c> (every type of that class, any faction),
/// <c>weapon:&lt;id&gt;</c>, <c>warhead:&lt;id&gt;</c>. A pure function of (base rules, upgrade mask): integer math only, so it is
/// deterministic and the result is never state. Unknown effect ids and attributes are ignored (the loader rejects them).
/// </summary>
public static class RulesDeriver
{
    private const string WeaponPrefix = "weapon:";
    private const string WarheadPrefix = "warhead:";
    private const int MaxMemoEntries = 64;

    /// <summary>The rules <paramref name="baseRules"/> become with the upgrades of <paramref name="upgradeMask"/> researched.
    /// Returns <paramref name="baseRules"/> itself when no researched upgrade has an effect. Always builds a fresh copy otherwise.</summary>
    public static SimRules Derive(SimRules baseRules, ulong upgradeMask)
    {
        ArgumentNullException.ThrowIfNull(baseRules);
        if (upgradeMask == 0 || baseRules.Effects == null || baseRules.Effects.Count == 0) return baseRules;
        IReadOnlyList<UpgradeType> upgrades = baseRules.Upgrades;
        Tables? t = null;
        for (int u = 0; u < upgrades.Count && u < RuleCounts.MaxUpgrades; u++)
        {
            if ((upgradeMask & (1UL << u)) == 0) continue;
            IReadOnlyList<string> ids = upgrades[u].Effects;
            for (int k = 0; k < ids.Count; k++)
            {
                int e = SimRules.IndexOf(baseRules.Effects, ids[k], x => x.Id);
                if (e < 0) continue;
                t ??= new Tables(baseRules);
                Apply(t, baseRules.Effects[e]);
            }
        }
        return t == null ? baseRules : t.Build(baseRules);
    }

    // ---- memo (not state) ----------------------------------------------------------------------------
    // Derived rules are a pure function of (base rules, mask), so they are memoised per base-rules instance. Races between sims on
    // different threads (tests) can only lose a memo entry, never change a result. Simulation.RulesOf reads this.

    private sealed class Memo
    {
        public (ulong Mask, SimRules Rules)[] Entries = Array.Empty<(ulong, SimRules)>();
    }

    private static readonly ConditionalWeakTable<SimRules, Memo> Memos = new();

    /// <summary><see cref="Derive"/>, memoised per base-rules instance (the cache <see cref="Simulation.RulesOf"/> uses).</summary>
    internal static SimRules Cached(SimRules baseRules, ulong upgradeMask)
    {
        if (upgradeMask == 0) return baseRules;
        Memo memo = Memos.GetValue(baseRules, _ => new Memo());
        (ulong Mask, SimRules Rules)[] entries = memo.Entries;
        for (int i = 0; i < entries.Length; i++)
            if (entries[i].Mask == upgradeMask) return entries[i].Rules;
        SimRules derived = Derive(baseRules, upgradeMask);
        var next = new (ulong, SimRules)[entries.Length >= MaxMemoEntries ? 1 : entries.Length + 1];
        if (next.Length > 1) Array.Copy(entries, next, entries.Length);
        next[^1] = (upgradeMask, derived);
        memo.Entries = next;
        return derived;
    }

    // ---- application -------------------------------------------------------------------------------

    private sealed class Tables
    {
        public readonly VehicleType[] Vehicles;
        public readonly InfantryType[] Infantry;
        public readonly BuildingType[] Buildings;
        public readonly WeaponType[] Weapons;
        public readonly WarheadType[] Warheads;
        public ShipmentRules Shipments;

        public Tables(SimRules r)
        {
            Vehicles = r.Vehicles.ToArray();
            Infantry = r.Infantry == null ? Array.Empty<InfantryType>() : r.Infantry.ToArray();
            Buildings = r.Buildings == null ? Array.Empty<BuildingType>() : r.Buildings.ToArray();
            Weapons = r.Weapons == null ? Array.Empty<WeaponType>() : r.Weapons.ToArray();
            Warheads = r.Warheads == null ? Array.Empty<WarheadType>() : r.Warheads.ToArray();
            Shipments = r.Shipments;
        }

        public SimRules Build(SimRules r) => r with
        {
            Vehicles = Vehicles,
            Infantry = r.Infantry == null ? null! : Infantry,
            Buildings = r.Buildings == null ? null! : Buildings,
            Weapons = r.Weapons == null ? null! : Weapons,
            Warheads = r.Warheads == null ? null! : Warheads,
            Shipments = Shipments,
        };
    }

    private static void Apply(Tables t, EffectDef e)
    {
        if (e.Op is not ("set" or "add" or "mul")) return;   // "enable" is reserved and unused (design 08 §4)
        string target = e.Target;
        if (target.StartsWith(WeaponPrefix, StringComparison.Ordinal))
        {
            int w = SimRules.IndexOf(t.Weapons, target[WeaponPrefix.Length..], x => x.Id);
            if (w >= 0) t.Weapons[w] = ApplyWeapon(t.Weapons[w], e);
            return;
        }
        if (target.StartsWith(WarheadPrefix, StringComparison.Ordinal))
        {
            int h = SimRules.IndexOf(t.Warheads, target[WarheadPrefix.Length..], x => x.Id);
            if (h >= 0) t.Warheads[h] = ApplyWarhead(t.Warheads[h], e);
            return;
        }
        switch (target)
        {
            case "class:vehicle":
                for (int i = 0; i < t.Vehicles.Length; i++) t.Vehicles[i] = ApplyVehicle(t.Vehicles[i], e);
                return;
            case "class:infantry":
                for (int i = 0; i < t.Infantry.Length; i++) t.Infantry[i] = ApplyInfantry(t.Infantry[i], e);
                return;
            case "class:player":   // simfu-3: player-level attributes
                if (e.Attr == "shipment_interval") t.Shipments = t.Shipments with { IntervalTicks = Op(e, t.Shipments.IntervalTicks, 1) };
                return;
            case "class:building":
                for (int i = 0; i < t.Buildings.Length; i++) t.Buildings[i] = ApplyBuilding(t.Buildings[i], e);
                return;
        }
        int v = SimRules.IndexOf(t.Vehicles, target, x => x.Id);
        if (v >= 0) t.Vehicles[v] = ApplyVehicle(t.Vehicles[v], e);
        int n = SimRules.IndexOf(t.Infantry, target, x => x.Id);
        if (n >= 0) t.Infantry[n] = ApplyInfantry(t.Infantry[n], e);
        for (int b = 0; b < t.Buildings.Length; b++)
            if (IsOrProvides(t.Buildings[b], target)) t.Buildings[b] = ApplyBuilding(t.Buildings[b], e);
    }

    private static bool IsOrProvides(BuildingType bt, string name)
    {
        if (string.Equals(bt.Id, name, StringComparison.Ordinal)) return true;
        for (int k = 0; k < bt.Provides.Count; k++)
            if (string.Equals(bt.Provides[k], name, StringComparison.Ordinal)) return true;
        return false;
    }

    /// <summary>One op on one value, in 64-bit, clamped to [<paramref name="min"/>, <paramref name="max"/>]. mul is per mille,
    /// truncated toward zero.</summary>
    private static int Op(EffectDef e, int current, int min = 0, int max = int.MaxValue)
    {
        long r = e.Op switch
        {
            "set" => e.Value,
            "add" => (long)current + e.Value,
            "mul" => (long)current * e.Value / 1000,
            _ => current,
        };
        return (int)Math.Clamp(r, min, max);
    }

    private static VehicleType ApplyVehicle(VehicleType x, EffectDef e) => e.Attr switch
    {
        "max_health" => x with { MaxHealth = Op(e, x.MaxHealth, 1) },
        "speed" => x with { Speed = Op(e, x.Speed) },
        "sight" => x with { Sight = Op(e, x.Sight) },
        "cost" => x with { Cost = Op(e, x.Cost) },
        "build_time" => x with { BuildTime = Op(e, x.BuildTime) },
        "rad_resist_permille" => x with { RadResistPermille = Op(e, x.RadResistPermille, 0, 1000) },
        "vet_threshold_permille" => x with { VetThresholdPermille = Op(e, x.VetThresholdPermille, 1, 100_000) },   // VET-1
        "harvest_capacity" => x with { HarvestCapacity = Op(e, x.HarvestCapacity, x.HarvestCapacity > 0 ? 1 : 0, 1000) },   // simfu-3: never turns a harvester off or a fighter into one
        _ => x,
    };

    private static InfantryType ApplyInfantry(InfantryType x, EffectDef e) => e.Attr switch
    {
        "max_health" => x with { MaxHealth = Op(e, x.MaxHealth, 1) },
        "speed" => x with { Speed = Op(e, x.Speed) },
        "sight" => x with { Sight = Op(e, x.Sight) },
        "cost" => x with { Cost = Op(e, x.Cost) },
        "build_time" => x with { BuildTime = Op(e, x.BuildTime) },
        "rad_resist_permille" => x with { RadResistPermille = Op(e, x.RadResistPermille, 0, 1000) },
        "vet_threshold_permille" => x with { VetThresholdPermille = Op(e, x.VetThresholdPermille, 1, 100_000) },   // VET-1
        _ => x,
    };

    private static BuildingType ApplyBuilding(BuildingType x, EffectDef e) => e.Attr switch
    {
        "max_health" => x with { MaxHealth = Op(e, x.MaxHealth, 1) },
        "sight" => x with { Sight = Op(e, x.Sight) },
        "power" => x with { Power = Op(e, x.Power, int.MinValue) },
        "cost" => x with { Cost = Op(e, x.Cost) },
        "ticks_per_point" => x with { TicksPerPoint = Op(e, x.TicksPerPoint, 1) },
        "superweapon_charge_ticks" => x with { SuperweaponChargeTicks = Op(e, x.SuperweaponChargeTicks, x.Superweapon.Length > 0 ? 1 : 0) },
        _ => x,
    };

    private static WeaponType ApplyWeapon(WeaponType x, EffectDef e) => e.Attr switch
    {
        "damage" => x with { Damage = Op(e, x.Damage) },
        "range" => x with { Range = Op(e, x.Range) },
        "rate_of_fire" => x with { RateOfFire = Op(e, x.RateOfFire, 1) },
        _ => x,
    };

    private static WarheadType ApplyWarhead(WarheadType x, EffectDef e) => e.Attr switch
    {
        "contaminate" => x with { Contaminate = Op(e, x.Contaminate, 0, 255) },
        "blackout_ticks" => x with { BlackoutTicks = Op(e, x.BlackoutTicks) },
        _ => x,
    };
}
