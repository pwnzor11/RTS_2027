// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Sim.Core;
using Rts.Sim.Rules;
using Rts.Sim.View;

namespace Rts.AI.Recruit;

/// <summary>
/// Unit production for the Recruit tier: one item at a time per lane, drawing from a seeded shuffled bag (infantry: riflemen with a
/// rocketeer now and then; vehicles: a tank first, then mostly tanks with a jeep now and then), only while cash covers the unit plus the credits the
/// build order still needs. Sets each new barracks/factory's rally point in front of the base, toward the map centre.
/// </summary>
internal sealed class ProductionManager
{
    // Each lane builds from a bag (the faction's, see RecruitFaction) that is reshuffled (seeded, per slot) whenever it runs out;
    // the vehicle lane opens with the faction's first vehicle.
    private readonly RecruitFaction _faction;
    private readonly IFactionTactics? _tactics;
    private readonly TierPlanner? _tier;
    private bool _noIncome;

    public ProductionManager(RecruitFaction faction, IFactionTactics? tactics, TierPlanner? tier = null)
    {
        _faction = faction; _tactics = tactics; _tier = tier;
    }

    private int[] _inf = Array.Empty<int>(), _veh = Array.Empty<int>();
    private int _infIdx, _vehIdx;
    private int _bagTier = 1;
    private AiRandom? _rng;
    private bool _first = true;
    private readonly HashSet<ulong> _rallied = new();

    /// <summary>M4-12: while true only a wanted harvester is bought (the no-income rescue is saving up).</summary>
    public bool Hold { get; set; }

    /// <summary>s23 D-235 (A): set by the AI each decision from <see cref="EconomyRescue.Broke"/> (income stalled, the map is mined out): the unit lanes
    /// keep no reserve, buy the dearest affordable unit and give up a stalled front item for its refund.</summary>
    public bool Broke { get; set; }

    /// <summary>S24 (D-241 Reserve): credits every unit purchase leaves untouched (the harvester replacement is exempt); 0 = none.</summary>
    public int OrderReserve { get; set; }

    /// <summary>M5-02: the commander's directive as knob values, set by the AI each decision (<see cref="DirectiveBias.Plain"/> = none).</summary>
    public DirectiveBias Bias { get; set; } = DirectiveBias.Plain;

    /// <summary>Units queued so far (infantry + vehicles).</summary>
    public int Queued { get; private set; }

    /// <summary>Rally cell in front of the base (set after the first Think with a yard).</summary>
    public CellCoord Rally { get; private set; }

    /// <summary>Takes the AI's seeded generator (variation in the unit mix).</summary>
    public void Start(AiRandom rng) => _rng = rng;

    public void Think(RecruitKnowledge k, ICommandSink sink, int reserve)
    {
        SimRules rules = k.Rules;
        if (_inf.Length == 0)
        {
            _inf = Resolve(_faction.InfantryBag, rules.Infantry, i => i.Id);
            _veh = Resolve(_faction.VehicleBag, rules.Vehicles, v => v.Id);
            Shuffle(_inf); Shuffle(_veh);
            _first = true;
        }
        // M4-12: tier-2/3 types join the bags once the player reaches that tech tier (tier 2+ profiles: the Recruit keeps its M3 mix)
        if (_tier != null && _tier.Profile.RespondToAttacks && k.View.Tier > _bagTier) GrowBags(k);
        if (!k.HaveYard) return;
        Rally = RallyCell(k);
        if (Bias.HasRallyCell && k.View.IsPassable(Bias.RallyCell, Locomotion.Tracked)) Rally = Bias.RallyCell;   // M5-02: a turtle defends where the commander says
        if (_tactics != null) Rally = _tactics.SafeCell(k, Rally);   // M3-10: not on an irradiated cell
        Rally = AshCells.OffField(k.View, Rally);   // S19 M13-12: soldiers do not wait on an ash field
        if (_faction.SkipUnbuildable)
        {
            SkipUnbuildable(k, ProductionCategory.Infantry, _inf, ref _infIdx);
            SkipUnbuildable(k, ProductionCategory.Vehicle, _veh, ref _vehIdx);
        }

        foreach (ViewActor b in k.Buildings)
        {
            if (rules.Buildings[b.TypeIndex].Produces == null) continue;
            ulong key = ((ulong)b.Id.Index << 16) | b.Id.Generation;
            if (!_rallied.Add(key)) continue;
            sink.Rally(b.Id, _tactics?.RallyFor(k, b) ?? Rally);
        }

        // M3-11: a lost harvester is replaced before anything else is bought (flag ReplaceHarvesters). While the money for it is short the
        // other lanes hold back; with no harvester and no money for a new one the AI falls back to the Recruit "spend what is left" rule.
        int harv = _tier?.WantedHarvester(k) ?? -1;
        int funds0 = k.Hud.Cash + k.Hud.Stored;
        bool holdForHarvester = harv >= 0 && (k.Harvesters.Count > 0 || funds0 >= rules.Vehicles[harv].Cost);
        if (holdForHarvester && k.Queue(ProductionCategory.Vehicle).Items.Count == 0 && k.Buildable(ProductionCategory.Vehicle, harv)
            && funds0 >= rules.Vehicles[harv].Cost)
        {
            sink.Build(ProductionCategory.Vehicle, harv);
            Queued++;
            _tier!.HarvesterOrdered();
        }
        _noIncome = k.Harvesters.Count == 0 && !holdForHarvester || Broke;   // s23 D-235 (A): a mined-out side spends down like one with no harvester
        if (Hold) return;   // M4-12: the no-income rescue is saving for a harvester
        if (Broke && CancelStalledUnit(k, sink)) return;

        // Vehicles first; infantry leaves the next vehicle's price in reserve so cheap riflemen cannot starve the factory.
        // No scraper left (it is never replaced): there is no income to wait for, so nothing is held back.
        if (_noIncome) reserve = OrderReserve;   // S24: a Reserve order holds even when broke (PendingCost already carries it otherwise)
        else reserve += Bias.UnitReserve + (_tier?.Profile.UnitReserve ?? 0);   // M5-02: economy / tech first (the rescue above is never held back); S15 lbknob: per-faction pacing
        if (holdForHarvester) reserve += rules.Vehicles[harv].Cost;
        else Lane(k, sink, reserve + (Bias.Mix == DirectiveCodec.MixInfantry ? MixLaneHold : 0), ProductionCategory.Vehicle, _veh, ref _vehIdx, t => rules.Vehicles[t].Cost);
        int vehicleReserve = holdForHarvester || _noIncome ? 0 : k.Queue(ProductionCategory.Vehicle).Items.Count == 0 && k.Buildable(ProductionCategory.Vehicle, PeekVehicle(rules))
            ? rules.Vehicles[PeekVehicle(rules)].Cost : 0;
        if (Bias.Mix == DirectiveCodec.MixInfantry) vehicleReserve = 0;   // M5-02: infantry first
        else if (Bias.Mix == DirectiveCodec.MixVehicles) vehicleReserve += MixLaneHold;
        Lane(k, sink, reserve + vehicleReserve, ProductionCategory.Infantry, _inf, ref _infIdx, t => rules.Infantry[t].Cost);
    }

    /// <summary>s23 D-235 (A): when Broke, the front item of a unit lane that is stalled for lack of money is cancelled (the refund buys a cheaper unit
    /// next decision). True when a Cancel was issued.</summary>
    private static bool CancelStalledUnit(RecruitKnowledge k, ICommandSink sink)
    {
        foreach (ProductionCategory cat in BrokeLanes)
        {
            QueueView q = k.Queue(cat);
            if (q.Items.Count == 0 || !IsMoneyStall(q.Items[0])) continue;
            sink.Issue(CommandKind.Cancel, EntityId.None, (ActorKind)(int)cat);   // Arg = category, front item
            return true;
        }
        return false;
    }

    /// <summary>s23 D-235 (A): a front item that is stalled and not yet paid in full (a finished item waiting for an exit cell, or one on hold, is not a money stall).</summary>
    internal static bool IsMoneyStall(QueueItemView it) => it.Stalled && !it.Held && it.ProgressPermille < 1000;

    private static readonly ProductionCategory[] BrokeLanes = [ProductionCategory.Vehicle, ProductionCategory.Infantry, ProductionCategory.Aircraft];

    private void Lane(RecruitKnowledge k, ICommandSink sink, int reserve, ProductionCategory cat, int[] mix, ref int idx,
        Func<int, int> cost)
    {
        if (k.Queue(cat).Items.Count > 0) return;
        if (_tier is { Profile.ArmyCap: > 0 } && (_tier.Profile.ArmyCapUntilTick == 0 || k.View.Tick < _tier.Profile.ArmyCapUntilTick) && k.Army.Count + k.Home.Count + k.Detached.Count >= _tier.Profile.ArmyCap) return;   // S15 lbknob: swarm cap
        if (cat == ProductionCategory.Infantry && _tactics != null && _tactics.HoldInfantry(k)) return;   // M7-10
        int type = cat == ProductionCategory.Vehicle ? PeekVehicle(k.Rules) : mix[idx % mix.Length];
        if (_tier != null && !(cat == ProductionCategory.Vehicle && _first)) type = _tier.PickUnit(k, cat, mix, type, _rng!);   // M3-11
        int wanted = cat == ProductionCategory.Vehicle && _tactics != null ? _tactics.WantedVehicle(k) : -1;
        // M3-11 (flag Air) / M4-04: aircraft are air-locomotion vehicle types built in the Aircraft lane; the next aircraft is a
        // special order like a Bore: it goes ahead of the vehicle bag and does not advance it
        bool aircraft = false;
        if (wanted < 0 && cat == ProductionCategory.Vehicle && _tier != null)
        {
            wanted = _tier.WantedAircraft(k);
            aircraft = wanted >= 0;
        }
        if (wanted >= 0)
        {
            // M3-10: a special order (a Bore for a new entrance, an Agitprop for the wave) goes ahead of the bag and does not advance it
            ProductionCategory lane = aircraft ? ProductionCategory.Aircraft : cat;   // M4-04: aircraft have their own lane
            if (aircraft && k.Queue(lane).Items.Count > 0) return;
            if (!k.Buildable(lane, wanted) || k.Hud.Cash + k.Hud.Stored < cost(wanted) + OrderReserve) return;
            sink.Build(lane, wanted);
            if (aircraft) _tier!.AircraftQueued();
            Queued++;
            return;
        }
        if (Bias.MixPermille > 0 && !(cat == ProductionCategory.Vehicle && _first)) type = PreferMix(k, cat, mix, type);   // M5-02
        if (!k.Buildable(cat, type)) return;
        int funds = k.Hud.Cash + k.Hud.Stored;
        if (_noIncome && funds < cost(type) + reserve)
        {
            // no income left: take the dearest bag item the money still covers instead of waiting for the planned one
            int best = -1;
            foreach (int t in mix)
                if (k.Buildable(cat, t) && cost(t) + OrderReserve <= funds && (best < 0 || cost(t) > cost(best))) best = t;
            if (best < 0) return;
            sink.Build(cat, best);
            Queued++;
            return;
        }
        if (funds < cost(type) + reserve) return;
        sink.Build(cat, type);
        if (cat == ProductionCategory.Vehicle && _first) _first = false;
        else
        {
            idx++;
            if (idx % mix.Length == 0) Shuffle(mix);
        }
        Queued++;
    }

    /// <summary>M5-02: credits the less wanted lane leaves in hand when the commander prefers infantry or vehicles.</summary>
    public const int MixLaneHold = 150;   // (tune)

    /// <summary>
    /// M5-02: with a unit-mix preference, a build takes the best buildable unit of the preferred kind (bag first, then every unit of the
    /// faction) in <see cref="DirectiveBias.MixPermille"/> of the cases, else the bag's choice. Anti-air: a gun that can hit aircraft;
    /// anti-structure: the most structure damage per squared credit. Infantry / vehicles act on the lane money instead.
    /// </summary>
    private int PreferMix(RecruitKnowledge k, ProductionCategory cat, int[] bag, int bagChoice)
    {
        if (Bias.Mix != DirectiveCodec.MixAntiAir && Bias.Mix != DirectiveCodec.MixAntiStructure) return bagChoice;
        if (_rng!.Next(1000) >= Bias.MixPermille) return bagChoice;
        SimRules rules = k.Rules;
        int best = -1;
        long bestScore = 0;
        int count = cat == ProductionCategory.Infantry ? rules.Infantry.Count : rules.Vehicles.Count;
        // the bag first (so a tie keeps the faction's own taste), then the rest of the roster in rule order
        for (int pass = 0; pass < 2; pass++)
            for (int i = 0; i < (pass == 0 ? bag.Length : count); i++)
            {
                int t = pass == 0 ? bag[i] : i;
                if (!k.Buildable(cat, t)) continue;
                long s = MixScore(rules, cat, t);
                if (s > bestScore) { bestScore = s; best = t; }
            }
        return best < 0 ? bagChoice : best;
    }

    private long MixScore(SimRules rules, ProductionCategory cat, int t)
    {
        int weaponIdx, cost;
        if (cat == ProductionCategory.Infantry) { InfantryType u = rules.Infantry[t]; weaponIdx = u.PrimaryWeapon; cost = u.Cost; if (u.Faction != _faction.Faction) return 0; }
        else { VehicleType u = rules.Vehicles[t]; weaponIdx = u.PrimaryWeapon; cost = u.Cost; if (u.Faction != _faction.Faction || u.Locomotion == Locomotion.Air || u.HarvestCapacity > 0) return 0; }
        if (weaponIdx < 0 || cost <= 0) return 0;
        WeaponType w = rules.Weapons[weaponIdx];
        if (w.Damage <= 0) return 0;
        if (Bias.Mix == DirectiveCodec.MixAntiAir) return w.Targets == WeaponTargets.Ground ? 0 : 1_000_000 / cost;
        WarheadType wh = rules.Warheads[w.Warhead];
        long dmg = (long)w.Damage * wh.VersusPercent[(int)ArmorClass.Structure] * (wh.Splash > 0 ? 12 : 10);
        return dmg * 1_000_000L / ((long)Math.Max(1, w.RateOfFire) * cost);
    }

    /// <summary>M4-12: rebuilds both bags as the faction's base bag plus the tier lists up to the player's tier now, and reshuffles them.
    /// The bags only grow: a lost gate re-locks those types and <see cref="SkipUnbuildable"/> passes over them until it is rebuilt.</summary>
    private void GrowBags(RecruitKnowledge k)
    {
        _bagTier = Math.Min(3, k.View.Tier);
        var inf = new List<string>(_faction.InfantryBag);
        var veh = new List<string>(_faction.VehicleBag);
        if (_bagTier >= 2) { inf.AddRange(_faction.InfantryTier2); veh.AddRange(_faction.VehicleTier2); }
        if (_bagTier >= 3) veh.AddRange(_faction.VehicleTier3);
        _inf = Resolve(inf.ToArray(), k.Rules.Infantry, i => i.Id);
        _veh = Resolve(veh.ToArray(), k.Rules.Vehicles, v => v.Id);
        _infIdx = 0; _vehIdx = 0;
        Shuffle(_inf); Shuffle(_veh);
    }

    /// <summary>The next vehicle to build: a tank first, then the shuffled bag.</summary>
    private int PeekVehicle(SimRules rules) =>
        _first ? SimRules.IndexOf(rules.Vehicles, _faction.FirstVehicle, v => v.Id) : _veh[_vehIdx % _veh.Length];

    /// <summary>Moves a bag index on past items whose tech is not built yet (Directorate: the Reactor Crawler before the Isotope Works).</summary>
    private static void SkipUnbuildable(RecruitKnowledge k, ProductionCategory cat, int[] mix, ref int idx)
    {
        for (int n = 0; n < mix.Length && !k.Buildable(cat, mix[idx % mix.Length]); n++) idx++;
    }

    private void Shuffle(int[] a)
    {
        if (_rng is null) return;
        for (int i = a.Length - 1; i > 0; i--)
        {
            int j = _rng.Next(i + 1);
            (a[i], a[j]) = (a[j], a[i]);
        }
    }

    private static int[] Resolve<T>(string[] ids, IReadOnlyList<T> list, Func<T, string> idOf)
    {
        var r = new int[ids.Length];
        for (int i = 0; i < ids.Length; i++)
        {
            r[i] = SimRules.IndexOf(list, ids[i], idOf);
            if (r[i] < 0) throw new InvalidOperationException("unit mix names unknown type '" + ids[i] + "'");
        }
        return r;
    }

    /// <summary>A passable cell about 7 cells from the yard toward the map centre.</summary>
    private static CellCoord RallyCell(RecruitKnowledge k)
    {
        var view = k.View;
        int cx = k.Yard.X + 1, cy = k.Yard.Y + 1;
        int dx = Math.Sign(view.MapWidth / 2 - cx), dy = Math.Sign(view.MapHeight / 2 - cy);
        for (int d = 7; d >= 2; d--)
        {
            var c = new CellCoord(Math.Clamp(cx + dx * d, 1, view.MapWidth - 2), Math.Clamp(cy + dy * d, 1, view.MapHeight - 2));
            if (view.IsPassable(c, Locomotion.Tracked)) return c;
        }
        return new CellCoord(cx, cy);
    }
    // ---- M3-21: AI save/restore. Appended at the end of the class; the format is private to the matching Restore. ----

    /// <summary>M3-21: writes the unit bags (already shuffled) with their positions, the rally cell and the memory of rallied factories.</summary>
    internal void Snapshot(BinaryWriter w)
    {
        w.Write(_noIncome); w.Write(_infIdx); w.Write(_vehIdx); w.Write(_bagTier); w.Write(_first); w.Write(Queued);
        AiSnapshot.WriteCell(w, Rally);
        w.Write(_inf.Length); foreach (int t in _inf) w.Write(t);
        w.Write(_veh.Length); foreach (int t in _veh) w.Write(t);
        AiSnapshot.WriteKeys(w, _rallied);
    }

    /// <summary>M3-21: reads what <see cref="Snapshot"/> wrote.</summary>
    internal void Restore(BinaryReader r)
    {
        _noIncome = r.ReadBoolean(); _infIdx = r.ReadInt32(); _vehIdx = r.ReadInt32(); _bagTier = r.ReadInt32(); _first = r.ReadBoolean();
        Queued = r.ReadInt32();
        Rally = AiSnapshot.ReadCell(r);
        _inf = new int[AiSnapshot.Count(r)]; for (int i = 0; i < _inf.Length; i++) _inf[i] = r.ReadInt32();
        _veh = new int[AiSnapshot.Count(r)]; for (int i = 0; i < _veh.Length; i++) _veh[i] = r.ReadInt32();
        AiSnapshot.ReadKeys(r, _rallied);
    }
}
