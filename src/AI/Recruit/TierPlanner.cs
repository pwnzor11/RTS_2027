// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Data;
using Rts.Sim.Core;
using Rts.Sim.Rules;
using Rts.Sim.View;

namespace Rts.AI.Recruit;

/// <summary>
/// M3-11: the tier brain shared by the production, build-order and military managers. Every capability is gated on a
/// <see cref="DifficultyProfile"/> flag, never on the tier number. It remembers (fog-honest, from the view only) the armour classes the
/// enemy has been seen to field, the enemy defences it knows of and where enemy harvesters were last seen, and from that it
/// decides: which harvester / aircraft / unit to build next (<see cref="WantedHarvester"/>, <see cref="WantedAircraft"/>,
/// <see cref="PickUnit"/>) and which extra structures the build order grows (<see cref="ExtendBuild"/>).
/// </summary>
internal sealed class TierPlanner
{
    /// <summary>Remembered enemy harvester sightings kept.</summary>
    public const int MaxSightings = 8;
    /// <summary>Most aircraft wanted at once.</summary>
    public const int MaxAircraft = 2;
    /// <summary>Defences the enemy must be known to have before long range is favoured.</summary>
    public const int DefencesForSiege = 2;
    /// <summary>Credits above which a rich AI with AdaptBuild adds production structures.</summary>
    public const int FloatingCash = 1500;
    /// <summary>Army size before the superweapon / air tech is added to the build order.</summary>
    public const int ArmyBeforeLateTech = 8;

    private readonly DifficultyProfile _p;
    private readonly RecruitFaction _faction;
    private readonly int[] _armor = new int[RuleCounts.ArmorClasses];
    private readonly int[] _defArmor = new int[RuleCounts.ArmorClasses];
    private readonly int[] _cur = new int[RuleCounts.ArmorClasses];
    private readonly List<(ulong Key, CellCoord Cell, ActorRef Ref, int Tick)> _sightings = new();
    private int _harvester = -2, _aircraft = -2;
    private int _defRange;

    public TierPlanner(DifficultyProfile profile, RecruitFaction faction)
    {
        _p = profile; _faction = faction;
    }

    public DifficultyProfile Profile => _p;

    /// <summary>Enemy defensive structures known (live or last seen).</summary>
    public int DefenceCount { get; private set; }

    /// <summary>Percent (0..100) of the enemy units seen recently that are heavily armoured (0 until any were seen).</summary>
    public int HeavyShare { get; private set; }

    /// <summary>True once any enemy unit was seen.</summary>
    public bool SeenEnemy { get; private set; }

    /// <summary>Harvesters built to replace lost ones.</summary>
    public int HarvestersReplaced { get; private set; }

    /// <summary>Aircraft ordered.</summary>
    public int AircraftOrdered { get; private set; }

    /// <summary>Records what is in view this decision.</summary>
    public void Observe(RecruitKnowledge k, List<ViewActor> knownEnemyBuildings)
    {
        SimRules rules = k.Rules;
        Array.Clear(_cur);
        int seen = 0;
        foreach (ViewActor e in k.Visible)
        {
            if (e.Relation != Relation.Enemy || !e.IsMobile) continue;
            ArmorClass ac = e.Kind == ActorKind.Vehicle ? rules.Vehicles[e.TypeIndex].Armor : rules.Infantry[e.TypeIndex].Armor;
            _cur[(int)ac]++;
            seen++;
            if (e.Kind == ActorKind.Vehicle && rules.Vehicles[e.TypeIndex].HarvestCapacity > 0) Remember(e, k.View.Tick);
        }
        if (seen > 0)
        {
            SeenEnemy = true;
            int heavy = 0, all = 0;
            for (int a = 0; a < _armor.Length; a++)
            {
                _armor[a] = (_armor[a] * 7 + _cur[a] * 100) / 8;
                all += _armor[a];
                if (a == (int)ArmorClass.Heavy) heavy += _armor[a];
            }
            HeavyShare = all == 0 ? 0 : heavy * 100 / all;
        }

        Array.Clear(_defArmor);
        DefenceCount = 0; _defRange = 0;
        foreach (ViewActor b in knownEnemyBuildings)
        {
            BuildingType bt = rules.Buildings[b.TypeIndex];
            if (bt.PrimaryWeapon < 0) continue;
            DefenceCount++;
            _defArmor[(int)bt.Armor] += 100;
            _defRange = Math.Max(_defRange, rules.Weapons[bt.PrimaryWeapon].Range);
        }
    }

    private void Remember(ViewActor e, int tick)
    {
        ulong key = RecruitKnowledge.KeyOf(e);
        for (int i = 0; i < _sightings.Count; i++)
            if (_sightings[i].Key == key) { _sightings[i] = (key, e.Cell, e.Ref, tick); return; }
        if (_sightings.Count >= MaxSightings) _sightings.RemoveAt(0);
        _sightings.Add((key, e.Cell, e.Ref, tick));
    }

    /// <summary>The most recent enemy harvester sighting not older than <paramref name="maxAge"/> ticks. <paramref name="live"/> is true
    /// when it is in view right now (so it can be attacked by id).</summary>
    public bool TryHarvesterTarget(RecruitKnowledge k, int maxAge, out CellCoord cell, out ActorRef target, out bool live)
    {
        cell = default; target = default; live = false;
        int bestTick = int.MinValue, now = k.View.Tick;
        foreach (var s in _sightings)
        {
            if (now - s.Tick > maxAge || s.Tick <= bestTick) continue;
            bestTick = s.Tick; cell = s.Cell; target = s.Ref; live = s.Tick == now;
        }
        return bestTick != int.MinValue;
    }

    // ---- economy ---------------------------------------------------------------------------------

    /// <summary>Harvesters wanted alive now: the profile's target, plus one once the expansion refinery stands.</summary>
    public int HarvestersWanted(RecruitKnowledge k)
    {
        int refinery = SimRules.IndexOf(k.Rules.Buildings, _faction.RefineryId, b => b.Id);
        bool expanded = _p.ExpansionTick > 0 && refinery >= 0 && k.BuildingCount[refinery] >= 2;
        return _p.HarvesterTarget + (expanded ? 1 : 0);
    }

    /// <summary>The harvester vehicle type to build now, or -1 (flag off, enough alive / queued).</summary>
    public int WantedHarvester(RecruitKnowledge k)
    {
        if (!_p.ReplaceHarvesters) return -1;
        if (_harvester == -2) _harvester = SimRules.IndexOf(k.Rules.Vehicles, _faction.HarvesterId, v => v.Id);
        if (_harvester < 0) return -1;
        int have = k.Harvesters.Count;
        foreach (QueueItemView it in k.Queue(ProductionCategory.Vehicle).Items)
            if (it.TypeIndex == _harvester) have++;
        return have < HarvestersWanted(k) ? _harvester : -1;
    }

    /// <summary>Notes that the production manager ordered the wanted harvester.</summary>
    public void HarvesterOrdered() => HarvestersReplaced++;

    /// <summary>The aircraft (an air-locomotion vehicle type) to queue now in the vehicle lane (Air flag, unlocked, fewer than <see cref="MaxAircraft"/> flying or queued), or -1.</summary>
    public int WantedAircraft(RecruitKnowledge k)
    {
        if (!_p.Air || _faction.AirProducerId is null) return -1;
        SimRules rules = k.Rules;
        if (_aircraft == -2)
        {
            _aircraft = -1;
            for (int i = 0; i < rules.Vehicles.Count; i++)
                if (rules.Vehicles[i].Locomotion == Locomotion.Air && rules.Vehicles[i].Faction == _faction.Faction) { _aircraft = i; break; }
        }
        if (_aircraft < 0 || !k.Buildable(ProductionCategory.Vehicle, _aircraft)) return -1;
        int have = 0;
        foreach (QueueItemView it in k.Queue(ProductionCategory.Vehicle).Items) if (it.TypeIndex == _aircraft) have++;
        foreach (ViewActor a in k.Army) if (a.Kind == ActorKind.Vehicle && rules.Vehicles[a.TypeIndex].Locomotion == Locomotion.Air) have++;
        foreach (ViewActor a in k.Home) if (a.Kind == ActorKind.Vehicle && rules.Vehicles[a.TypeIndex].Locomotion == Locomotion.Air) have++;
        return have < MaxAircraft ? _aircraft : -1;
    }

    public void AircraftQueued() => AircraftOrdered++;

    // ---- unit choice ------------------------------------------------------------------------------

    /// <summary>
    /// The unit to build in a lane. Without EnemyAwareMix this is <paramref name="bagChoice"/> (no random numbers are drawn). With it,
    /// two builds in three take the buildable candidate with the best damage-times-health per squared credit against the armour mix the
    /// enemy has been seen with (Lanchester: for a fixed budget the strength grows with dps x hp / cost^2); the third keeps the bag so the
    /// army stays varied. With CounterDefences and enough enemy defences known, a unit that outranges them is worth double and one that
    /// does not is worth 70 percent.
    /// </summary>
    public int PickUnit(RecruitKnowledge k, ProductionCategory cat, int[] candidates, int bagChoice, AiRandom rng)
    {
        bool siege = _p.CounterDefences && DefenceCount >= DefencesForSiege;
        if (!_p.EnemyAwareMix || (!SeenEnemy && !siege)) return bagChoice;
        SimRules rules = k.Rules;
        long total = 0;
        Span<long> mix = stackalloc long[RuleCounts.ArmorClasses];
        for (int a = 0; a < mix.Length; a++)
        {
            mix[a] = _armor[a] + (siege ? _defArmor[a] : 0);
            total += mix[a];
        }
        if (total == 0) return bagChoice;
        int best = -1;
        long bestScore = -1;
        for (int i = 0; i < candidates.Length; i++)
        {
            int t = candidates[i];
            if (!k.Buildable(cat, t)) continue;
            long score = Score(rules, cat, t, mix, total, siege);
            if (score > bestScore) { bestScore = score; best = t; }
        }
        if (best < 0 || bestScore <= 0) return bagChoice;
        return rng.Next(3) < 2 ? best : bagChoice;
    }

    private long Score(SimRules rules, ProductionCategory cat, int type, ReadOnlySpan<long> mix, long total, bool siege)
    {
        int weaponIdx, hp, cost;
        if (cat == ProductionCategory.Infantry) { InfantryType u = rules.Infantry[type]; weaponIdx = u.PrimaryWeapon; hp = u.MaxHealth; cost = u.Cost; }
        else { VehicleType u = rules.Vehicles[type]; weaponIdx = u.PrimaryWeapon; hp = u.MaxHealth; cost = u.Cost; }
        if (weaponIdx < 0 || cost <= 0) return 0;
        WeaponType w = rules.Weapons[weaponIdx];
        WarheadType wh = rules.Warheads[w.Warhead];
        long dmg = 0;
        for (int a = 0; a < mix.Length; a++) dmg += mix[a] * w.Damage * wh.VersusPercent[a] / 100;
        dmg /= total;
        if (wh.Splash > 0) dmg = dmg * 120 / 100;
        long score = dmg * hp * 10_000_000L / ((long)Math.Max(1, w.RateOfFire) * cost * cost);
        if (siege) score = w.Range > _defRange ? score * 2 : score * 70 / 100;
        return score;
    }

    // ---- build order growth -------------------------------------------------------------------------

    /// <summary>Appends the structures this tier's capabilities need to the build order (each at most once per count wanted).</summary>
    public void ExtendBuild(RecruitKnowledge k, BuildOrderManager build)
    {
        int tick = k.View.Tick;
        int funds = k.Hud.Cash + k.Hud.Stored;
        if (_p.ExpansionTick > 0 && tick >= _p.ExpansionTick && build.StepsOf(_faction.RefineryId) < 2)
            build.Extend(_faction.RefineryId);
        if (_p.UseUpgrades && _faction.ResearchId is string research && build.StepsOf(research) < 1)
            build.Extend(research);
        bool lateOk = k.Army.Count >= ArmyBeforeLateTech;
        if (_p.Air && _faction.AirProducerId is string air && lateOk && build.StepsOf(air) < 1) build.Extend(air);
        if (_p.Superweapons && _faction.SuperweaponBuildingId is string sw && lateOk && build.StepsOf(sw) < 1) build.Extend(sw);
        if (!_p.AdaptBuild) return;
        if (funds >= FloatingCash)
        {
            if (build.StepsOf(_faction.BarracksId) < 2) build.Extend(_faction.BarracksId);
            else if (build.StepsOf(_faction.FactoryId) < 2) build.Extend(_faction.FactoryId);
        }
        if (SeenEnemy && HeavyShare >= 50 && build.StepsOf(_faction.DefenceId) < 3 && build.StepsOf(_faction.DefenceId) <= DefenceTotalBuilt(k))
            build.Extend(_faction.DefenceId);
    }

    private int DefenceTotalBuilt(RecruitKnowledge k)
    {
        int t = SimRules.IndexOf(k.Rules.Buildings, _faction.DefenceId, b => b.Id);
        return t < 0 ? 0 : k.BuildingCount[t];
    }
}
