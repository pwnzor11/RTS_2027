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
    private AiRandom? _rng;
    private bool _first = true;
    private readonly HashSet<ulong> _rallied = new();

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
        if (!k.HaveYard) return;
        Rally = RallyCell(k);
        if (_tactics != null) Rally = _tactics.SafeCell(k, Rally);   // M3-10: not on an irradiated cell
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
            sink.Rally(b.Id, Rally);
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
        _noIncome = k.Harvesters.Count == 0 && !holdForHarvester;

        // Vehicles first; infantry leaves the next vehicle's price in reserve so cheap riflemen cannot starve the factory.
        // No scraper left (it is never replaced): there is no income to wait for, so nothing is held back.
        if (_noIncome) reserve = 0;
        if (holdForHarvester) reserve += rules.Vehicles[harv].Cost;
        else Lane(k, sink, reserve, ProductionCategory.Vehicle, _veh, ref _vehIdx, t => rules.Vehicles[t].Cost);
        int vehicleReserve = holdForHarvester || _noIncome ? 0 : k.Queue(ProductionCategory.Vehicle).Items.Count == 0 && k.Buildable(ProductionCategory.Vehicle, PeekVehicle(rules))
            ? rules.Vehicles[PeekVehicle(rules)].Cost : 0;
        Lane(k, sink, reserve + vehicleReserve, ProductionCategory.Infantry, _inf, ref _infIdx, t => rules.Infantry[t].Cost);
    }

    private void Lane(RecruitKnowledge k, ICommandSink sink, int reserve, ProductionCategory cat, int[] mix, ref int idx,
        Func<int, int> cost)
    {
        if (k.Queue(cat).Items.Count > 0) return;
        int type = cat == ProductionCategory.Vehicle ? PeekVehicle(k.Rules) : mix[idx % mix.Length];
        if (_tier != null && !(cat == ProductionCategory.Vehicle && _first)) type = _tier.PickUnit(k, cat, mix, type, _rng!);   // M3-11
        int wanted = cat == ProductionCategory.Vehicle && _tactics != null ? _tactics.WantedVehicle(k) : -1;
        // M3-11 (flag Air): the sim has no aircraft lane (an aircraft is a vehicle type with air locomotion built in the vehicle lane), so
        // the next aircraft is a special order like a Bore: it goes ahead of the bag and does not advance it
        bool aircraft = false;
        if (wanted < 0 && cat == ProductionCategory.Vehicle && _tier != null)
        {
            wanted = _tier.WantedAircraft(k);
            aircraft = wanted >= 0;
        }
        if (wanted >= 0)
        {
            // M3-10: a special order (a Bore for a new entrance, an Agitprop for the wave) goes ahead of the bag and does not advance it
            if (!k.Buildable(cat, wanted) || k.Hud.Cash + k.Hud.Stored < cost(wanted)) return;
            sink.Build(cat, wanted);
            if (aircraft) _tier!.AircraftQueued();
            Queued++;
            return;
        }
        if (!k.Buildable(cat, type)) return;
        int funds = k.Hud.Cash + k.Hud.Stored;
        if (_noIncome && funds < cost(type) + reserve)
        {
            // no income left: take the dearest bag item the money still covers instead of waiting for the planned one
            int best = -1;
            foreach (int t in mix)
                if (k.Buildable(cat, t) && cost(t) <= funds && (best < 0 || cost(t) > cost(best))) best = t;
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
}
