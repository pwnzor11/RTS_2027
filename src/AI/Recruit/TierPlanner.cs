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

    /// <summary>M4-16 (flag UseUpgrades): percent by which the known enemy army value must exceed ours before tech spending stops.</summary>
    public const int BehindMarginPct = 20;   // (tune) M4-16
    /// <summary>M4-16: percent by which our army value must exceed the known enemy army value to count as clearly ahead.</summary>
    public const int AheadMarginPct = 50;   // (tune) M4-16
    /// <summary>M4-16: fewest army units that can count as ahead.</summary>
    public const int AheadMinUnits = 8;   // (tune) M4-16

    /// <summary>M4-16: master switch of the tech hold (no research, tier gates, air tech or superweapon while behind).</summary>
    public const bool TechHoldEnabled = false;   // (tune) M4-16, off pending measurement

    /// <summary>M4-16: ticks a sighting of the enemy army is trusted for (peak value seen in this window).</summary>
    public const int EnemyValueMemoryTicks = 1200;   // (tune) M4-16

    /// <summary>M4-16 round 4: ticks the wave strength gate trusts the biggest enemy army seen at once.</summary>
    public const int WaveGateMemoryTicks = 3600;   // (tune) M4-16 r4

    private int _enemyPeak, _enemyPeakTick = int.MinValue, _longPeak, _longPeakTick = int.MinValue;

    /// <summary>M4-16 round 4: peak enemy army value seen at once in the last longer window (the wave strength gate).</summary>
    public int EnemyArmyLong { get; private set; }

    /// <summary>M4-16: credit value of our mobile army (units, not harvesters), this decision.</summary>
    public int OwnArmyValue { get; private set; }

    /// <summary>M4-16: credit value of the enemy army seen in the last <see cref="EnemyValueMemoryTicks"/> ticks (peak of what was in view at once).</summary>
    public int EnemyArmyValue { get; private set; }

    /// <summary>M4-16: our army is worth at least (100 + <see cref="AheadMarginPct"/>) percent of the enemy army seen lately and has <see cref="AheadMinUnits"/> units.</summary>
    public bool Ahead { get; private set; }

    /// <summary>M4-16: the known enemy army is worth more than ours by <see cref="BehindMarginPct"/>: tech, research and tier gates wait (flag UseUpgrades).</summary>
    public bool Behind { get; private set; }

    /// <summary>Records what is in view this decision.</summary>
    public void Observe(RecruitKnowledge k, List<ViewActor> knownEnemyBuildings)
    {
        SimRules rules = k.Rules;
        {
            int own = 0, enemy = 0;
            foreach (ViewActor a in k.Own)
                if (a.IsMobile && !IsHarvester(rules, a) && !IsUtility(rules, a)) own += UnitValue(rules, a);
            foreach (ViewActor e in k.Visible)
                if (e.Relation == Relation.Enemy && e.IsMobile && !IsHarvester(rules, e) && !IsUtility(rules, e)) enemy += UnitValue(rules, e);
            int now = k.View.Tick;
            if (enemy >= _enemyPeak || now - _enemyPeakTick > EnemyValueMemoryTicks) { _enemyPeak = enemy; _enemyPeakTick = now; }
            OwnArmyValue = own; EnemyArmyValue = _enemyPeak;
            if (enemy >= _longPeak || now - _longPeakTick > WaveGateMemoryTicks) { _longPeak = enemy; _longPeakTick = now; }
            EnemyArmyLong = _longPeak;
            Ahead = k.Army.Count >= AheadMinUnits && (long)own * 100 >= (long)_enemyPeak * (100 + AheadMarginPct);
            Behind = TechHoldEnabled && _p.UseUpgrades && (long)_enemyPeak * 100 > (long)own * (100 + BehindMarginPct);
        }
        Array.Clear(_cur);
        int seen = 0;
        foreach (ViewActor e in k.Visible)
        {
            if (e.Relation != Relation.Enemy || !e.IsMobile) continue;
            ArmorClass ac = e.Kind == ActorKind.Vehicle ? rules.Vehicles[e.TypeIndex].Armor : rules.Infantry[e.TypeIndex].Armor;
            _cur[(int)ac]++;
            seen++;
            if (e.Kind == ActorKind.Vehicle && rules.Vehicles[e.TypeIndex].Locomotion == Locomotion.Air) SeenEnemyAir = true;
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

    /// <summary>M4-16: true for the structures that are tech spending (research, tier gates, air tech, superweapon): the build order
    /// skips them while <see cref="Behind"/>. Economy, production and defence structures are never held.</summary>
    public bool IsTech(string id) =>
        id == _faction.ResearchId || id == _faction.Tier2GateId || id == _faction.Tier3GateId || id == _faction.AirProducerId
        || id == _faction.SuperweaponBuildingId;

    /// <summary>M7-10: an unarmed Expedition vehicle (Command Ship, Landing Craft, Supply Tender ...) is not army value.</summary>
    private static bool IsUtility(SimRules rules, ViewActor a) =>
        a.Kind == ActorKind.Vehicle && rules.Vehicles[a.TypeIndex] is { Faction: Faction.Expedition, PrimaryWeapon: < 0 };

    private static bool IsHarvester(SimRules rules, ViewActor a) => a.Kind == ActorKind.Vehicle && rules.Vehicles[a.TypeIndex].HarvestCapacity > 0;

    /// <summary>Army value of a unit: its cost with the veterancy rank counted (VET-6, same as MilitaryManager). Rank 0 is the plain cost.</summary>
    private static int UnitValue(SimRules rules, ViewActor a) => VeterancyValue.Of(rules, a);

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
        bool expanded = (_p.ExpansionTick > 0 || Bias.Expand) && refinery >= 0 && k.BuildingCount[refinery] >= 2;
        return _p.HarvesterTarget + _faction.ExtraHarvesters + (expanded ? 1 : 0);
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

    /// <summary>The aircraft (an air-locomotion vehicle type) to queue now in the Aircraft lane (M4-04) (Air flag, unlocked, fewer than <see cref="MaxAircraft"/> flying or queued), or -1.</summary>
    public int WantedAircraft(RecruitKnowledge k)
    {
        if (!_p.Air || _faction.AirProducerId is null) return -1;
        SimRules rules = k.Rules;
        // M4-12: the best buildable aircraft of the faction, not just the first: a ground attacker of the highest tier (the bomber, the
        // Great Dirigible); a pure anti-air fighter only once enemy aircraft were seen
        _aircraft = -1;
        int bestTier = -1;
        for (int i = 0; i < rules.Vehicles.Count; i++)
        {
            VehicleType v = rules.Vehicles[i];
            if (v.Locomotion != Locomotion.Air || !SameFaction(v.Faction) || !k.Buildable(ProductionCategory.Aircraft, i)) continue;
            int w = v.PrimaryWeapon;
            bool antiAirOnly = w >= 0 && rules.Weapons[w].Targets == WeaponTargets.Air;
            bool hurts = w >= 0 && rules.Weapons[w].Damage > 0;
            int rank = v.Tier * 4 + (hurts ? 2 : 0) + (antiAirOnly ? (SeenEnemyAir || Bias.WantsAntiAir ? 3 : -100) : 0)
                + (v.Id == _faction.PreferredAircraftId ? 1 : 0);   // M6-10: the faction's favourite wins a tie
            if (rank > bestTier) { bestTier = rank; _aircraft = i; }
        }
        if (_aircraft < 0) return -1;
        int have = 0;
        foreach (QueueItemView it in k.Queue(ProductionCategory.Aircraft).Items) have++;
        foreach (ViewActor a in k.Army) if (a.Kind == ActorKind.Vehicle && rules.Vehicles[a.TypeIndex].Locomotion == Locomotion.Air) have++;
        foreach (ViewActor a in k.Home) if (a.Kind == ActorKind.Vehicle && rules.Vehicles[a.TypeIndex].Locomotion == Locomotion.Air) have++;
        return have < Bias.AircraftCap ? _aircraft : -1;
    }

    public void AircraftQueued() => AircraftOrdered++;

    // Authority types carry no faction key in the data (Faction.Authority is the default value)
    private bool SameFaction(Faction f) => f == _faction.Faction;

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

    /// <summary>M4-12 (flag UseUpgrades): tick from which the tier-2 gate is added to the build order.</summary>
    public const int Tier2GateTick = 3600;   // (tune) 3 min
    /// <summary>M4-12 (flag UseUpgrades): tick from which the tier-3 gate is added (design 08 §8: tier 3 by 15 min).</summary>
    public const int Tier3GateTick = 8400;   // (tune) 7 min
    /// <summary>M4-12: army size that also brings the tier-3 gate forward (a rich side climbs early).</summary>
    public const int ArmyBeforeTier3 = 12;   // (tune)

    /// <summary>M4-16 s12 tier-5 fix (flag UseUpgrades): army size before the research structure and the tier-2 gate are added to the build order. The
    /// tier-5 trace showed the tech spending at 3 minutes leaving the army half the size of a recruit's, so the first wave and then the base were lost.</summary>
    public const int ArmyBeforeTech = 30;   // (tune) s12 tier 5

    /// <summary>M4-16 s12 tier-5 fix (flag AdaptBuild): army size before extra production or defence structures are added.</summary>
    public const int ArmyBeforeAdapt = 30;   // (tune) s12 tier 5

    /// <summary>M4-12: army size before the garrisonable defence is added to the build order.</summary>
    public const int ArmyBeforeGarrison = 16;   // (tune)

    /// <summary>M5-02: the commander's directive as knob values, set by the AI each decision (<see cref="DirectiveBias.Plain"/> = none).</summary>
    public DirectiveBias Bias { get; set; } = DirectiveBias.Plain;

    /// <summary>M4-12: an enemy aircraft has been seen (anti-air is then wanted).</summary>
    public bool SeenEnemyAir { get; private set; }

    /// <summary>Appends the structures this tier's capabilities need to the build order (each at most once per count wanted).</summary>
    public void ExtendBuild(RecruitKnowledge k, BuildOrderManager build)
    {
        int tick = k.View.Tick;
        int funds = k.Hud.Cash + k.Hud.Stored;
        bool up = _p.UseUpgrades || Bias.ForceTech;   // M5-02: a tech focus climbs even on a profile without research
        if (((_p.ExpansionTick > 0 && tick >= _p.ExpansionTick) || Bias.Expand) && build.StepsOf(_faction.RefineryId) < 2)
            build.Extend(_faction.RefineryId);
        bool techArmy = k.Army.Count >= ArmyBeforeTech || Bias.ForceTech;   // s12: no tech spending before there is an army
        if (up && techArmy && _faction.ResearchId is string research && build.StepsOf(research) < 1)
            build.Extend(research);
        // M4-12: tier climbing on a schedule (flag UseUpgrades): the tier-2 gate, then the tier-3 gate. A lost gate is rebuilt (the build
        // order counts owned structures), so the tier comes back.
        if (up && techArmy && _faction.Tier2GateId is string g2 && tick >= Tier2GateTick * Bias.GatePermille / 1000 && build.StepsOf(g2) < 1) build.Extend(g2);
        if (up && _faction.Tier3GateId is string g3 && k.View.Tier >= 2
            && tick >= Tier3GateTick * Bias.GatePermille / 1000 && k.Army.Count >= ArmyBeforeTier3 * Bias.Tier3ArmyPermille / 1000 && build.StepsOf(g3) < 1) build.Extend(g3);   // (tune) M4-12 round 2: time AND army
        // M4-12: a garrisonable defence near the base (flag RespondToAttacks: tier 2+); the garrison manager fills it with idle infantry
        if (_p.RespondToAttacks && _faction.GarrisonId is string gar && k.Army.Count >= ArmyBeforeGarrison && build.StepsOf(gar) < 1) build.Extend(gar);
        // M4-12: anti-air once enemy aircraft were seen
        if ((SeenEnemyAir || Bias.WantsAntiAir) && _faction.AntiAirId is string aa && build.StepsOf(aa) < 2) build.Extend(aa);
        bool lateOk = k.Army.Count >= ArmyBeforeLateTech || Bias.EarlyLateTech;
        if (_p.Air && _faction.AirProducerId is string air && lateOk && (k.View.Tier >= 2 || !up) && build.StepsOf(air) < 1)
            build.Extend(air);
        if (_p.Superweapons && _faction.SuperweaponBuildingId is string sw && SuperweaponUsable(k.Rules, sw) && lateOk && (k.View.Tier >= 3 || !up)
            && build.StepsOf(sw) < 1) build.Extend(sw);
        if (!_p.AdaptBuild || k.Army.Count < ArmyBeforeAdapt) return;
        if (funds >= FloatingCash)
        {
            if (build.StepsOf(_faction.BarracksId) < 2) build.Extend(_faction.BarracksId);
            else if (build.StepsOf(_faction.FactoryId) < 2) build.Extend(_faction.FactoryId);
        }
        if (SeenEnemy && HeavyShare >= 50 && build.StepsOf(_faction.DefenceId) < 3 && build.StepsOf(_faction.DefenceId) <= DefenceTotalBuilt(k))
            build.Extend(_faction.DefenceId);
    }

    /// <summary>M6-10: false when the superweapon needs a tunnel entrance in range and the faction has no entrance building at all (the
    /// Second Drift's data says so, see the M6-10 notes): the structure would never fire, so it is not built.</summary>
    private bool SuperweaponUsable(SimRules rules, string id)
    {
        int t = SimRules.IndexOf(rules.Buildings, id, b => b.Id);
        if (t < 0 || rules.Buildings[t].SuperweaponEntranceRange <= 0) return true;
        foreach (BuildingType b in rules.Buildings)
            if (b.Faction == _faction.Faction && b.Entrance != TunnelSize.None) return true;
        return false;
    }

    private int DefenceTotalBuilt(RecruitKnowledge k)
    {
        int t = SimRules.IndexOf(k.Rules.Buildings, _faction.DefenceId, b => b.Id);
        return t < 0 ? 0 : k.BuildingCount[t];
    }
    // ---- M3-21: AI save/restore. Appended at the end of the class; the format is private to the matching Restore. ----

    /// <summary>M3-21: writes what the planner remembers of the enemy (armour mix, defences, harvester sightings, flags) and its counters.</summary>
    internal void Snapshot(BinaryWriter w)
    {
        w.Write(_armor.Length); foreach (int v in _armor) w.Write(v);
        w.Write(_defArmor.Length); foreach (int v in _defArmor) w.Write(v);
        w.Write(_defRange); w.Write(DefenceCount); w.Write(HeavyShare);
        w.Write(SeenEnemy); w.Write(SeenEnemyAir); w.Write(HarvestersReplaced); w.Write(AircraftOrdered);
        w.Write(_sightings.Count);
        foreach (var s in _sightings)
        {
            w.Write(s.Key); AiSnapshot.WriteCell(w, s.Cell); w.Write((byte)s.Ref.Kind); AiSnapshot.WriteId(w, s.Ref.Id); w.Write(s.Tick);
        }
        w.Write(_enemyPeak); w.Write(_enemyPeakTick);   // M4-16
        w.Write(_longPeak); w.Write(_longPeakTick);   // M4-16 r4
    }

    /// <summary>M3-21: reads what <see cref="Snapshot"/> wrote.</summary>
    internal void Restore(BinaryReader r)
    {
        int n = AiSnapshot.Count(r);
        for (int i = 0; i < n; i++) { int v = r.ReadInt32(); if (i < _armor.Length) _armor[i] = v; }
        n = AiSnapshot.Count(r);
        for (int i = 0; i < n; i++) { int v = r.ReadInt32(); if (i < _defArmor.Length) _defArmor[i] = v; }
        _defRange = r.ReadInt32(); DefenceCount = r.ReadInt32(); HeavyShare = r.ReadInt32();
        SeenEnemy = r.ReadBoolean(); SeenEnemyAir = r.ReadBoolean(); HarvestersReplaced = r.ReadInt32(); AircraftOrdered = r.ReadInt32();
        n = AiSnapshot.Count(r);
        _sightings.Clear();
        for (int i = 0; i < n; i++)
            _sightings.Add((r.ReadUInt64(), AiSnapshot.ReadCell(r), new ActorRef((ActorKind)r.ReadByte(), AiSnapshot.ReadId(r)), r.ReadInt32()));
        _enemyPeak = r.ReadInt32(); _enemyPeakTick = r.ReadInt32();   // M4-16
        _longPeak = r.ReadInt32(); _longPeakTick = r.ReadInt32();   // M4-16 r4
    }
}
