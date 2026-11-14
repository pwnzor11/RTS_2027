// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Sim;
using Rts.Sim.Core;
using Rts.Sim.Rules;
using Rts.Sim.View;

namespace Rts.AI.Recruit;

/// <summary>
/// The fixed structure build order of the Recruit tier (data: the tier's build_repertoire, falling back to
/// the faction's default order, see <see cref="RecruitFaction"/>). Step n is satisfied when the AI owns (or has queued / ready) at least as many of that type
/// as the order lists up to and including step n, so a destroyed structure is simply built again. Places each Ready
/// structure via <see cref="PlacementSearch"/>. Inserts a power plant first whenever the next structure would push power use
/// above output.
/// </summary>
internal sealed class BuildOrderManager
{
    private readonly List<string> _order;
    private readonly string _powerId, _yardId;
    private readonly PlacementSearch _search = new();
    private readonly List<int> _orderTypes = new();
    private int _powerType = -1;
    private int[] _queued = Array.Empty<int>();
    private int[] _seen = Array.Empty<int>();

    public BuildOrderManager(string[] order, string powerId, string yardId)
    {
        _order = new List<string>(order);
        _powerId = powerId; _yardId = yardId;
    }

    /// <summary>M3-11: appends a structure to the end of the order (a tier capability: expansion refinery, research, production). Resolved on the next Think.</summary>
    public void Extend(string id) => _order.Add(id);

    /// <summary>Q-65: inserts a structure into the order directly after the first listed <paramref name="after"/> (at the end when that is not listed),
    /// so an economy structure is not queued behind the tech steps. Resolved on the next Think.</summary>
    public void InsertAfter(string id, string after)
    {
        int at = _order.IndexOf(after);
        _order.Insert(at < 0 ? _order.Count : at + 1, id);
        _orderTypes.Clear();
    }

    /// <summary>How many times the order lists <paramref name="id"/> (so a caller can append only the missing copies).</summary>
    public int StepsOf(string id)
    {
        int n = 0;
        foreach (string s in _order) if (s == id) n++;
        return n;
    }

    /// <summary>M4-12: while not <see cref="int.MinValue"/>, nothing is started except this structure type (when none is owned or queued);
    /// -1 holds everything. Set by the no-income rescue.</summary>
    public int HoldExcept { get; set; } = int.MinValue;

    /// <summary>s24 D-242: while the economy is broke (everything else held) the one structure still worth paying for: a lost barracks, else a lost factory,
    /// when it is buildable and the funds cover it (units are the only thing left to buy with them). The type index, or -1.</summary>
    internal static int LostProducer(RecruitKnowledge k, string barracksId, string factoryId, int funds)
    {
        foreach (string id in new[] { barracksId, factoryId })
        {
            int t = SimRules.IndexOf(k.Rules.Buildings, id, b => b.Id);
            if (t < 0 || k.BuildingCount[t] > 0) continue;
            if (k.Buildable(k.Rules.Buildings[t].Category, t) && funds >= k.Rules.Buildings[t].Cost) return t;
        }
        return -1;
    }

    /// <summary>M4-12: when true (tier 2+), <see cref="PendingCost"/> also counts what the structures in production still have to be paid,
    /// so units do not starve a 2500-credit tier gate that is already under way (it stalled for whole games).</summary>
    public bool ReserveInProgress { get; set; }

    /// <summary>M4-16: when set and true for a structure id, that order step is skipped for now (tech held while the enemy army is bigger).</summary>
    public Func<string, bool>? HoldStep { get; set; }

    /// <summary>M4-16: decisions on which a tech step was skipped.</summary>
    public int TechSkips { get; private set; }

    /// <summary>Credits the next structure (still to be started or being paid for) will need; units keep this in reserve.</summary>
    public int PendingCost { get; private set; }

    /// <summary>M7-10: a faction hook that may choose the cell of a ready structure itself (Expedition: beacons at the frontier); null or false = the shared search.</summary>
    public Func<RecruitKnowledge, int, (bool Ok, CellCoord Cell)>? Placer { get; set; }

    /// <summary>Number of structures placed so far by this AI.</summary>
    public int Placed { get; private set; }

    /// <summary>Ready-structure placements that failed before issue: the search found no site, or <c>CanPlace</c> refused the site found.</summary>
    public int Unplaceable { get; private set; }

    /// <summary>M13-06: consecutive decisions in which a finished structure found no usable site before the AI cancels it (full refund).</summary>
    public const int CancelAfterFailedSearches = 3;

    /// <summary>M13-06: decisions during which a cancelled structure type is skipped in the build order, so another step can go first.</summary>
    public const int CancelledTypeCooldown = 20;

    /// <summary>M13-06: ready structures cancelled because no site was found.</summary>
    public int Cancelled { get; private set; }

    private readonly int[] _failedSearches = new int[2];   // M13-06: per lane (Structure, Defence)
    private int _banType = -1, _banLeft;                   // M13-06: the last cancelled type and its remaining cooldown

    public void Think(RecruitKnowledge k, ICommandSink sink)
    {
        if (_banLeft > 0 && --_banLeft == 0) _banType = -1;
        SimRules rules = k.Rules;
        if (_orderTypes.Count < _order.Count || _queued.Length == 0)
        {
            for (int i = _orderTypes.Count; i < _order.Count; i++)
            {
                int t = SimRules.IndexOf(rules.Buildings, _order[i], b => b.Id);
                if (t < 0) throw new InvalidOperationException("build order names unknown structure '" + _order[i] + "'");
                _orderTypes.Add(t);
            }
            _powerType = SimRules.IndexOf(rules.Buildings, _powerId, b => b.Id);
            _queued = new int[rules.Buildings.Count];
            _seen = new int[rules.Buildings.Count];
        }
        if (!k.HaveYard) { PendingCost = 0; return; }

        Array.Clear(_queued);
        bool laneBusy = false;
        int inProgress = 0;
        foreach (ProductionCategory cat in new[] { ProductionCategory.Structure, ProductionCategory.Defence })
        {
            QueueView q = k.Queue(cat);
            int lane = cat == ProductionCategory.Structure ? 0 : 1;
            foreach (QueueItemView it in q.Items)
            {
                _queued[it.TypeIndex]++; laneBusy = true;
                if (ReserveInProgress) inProgress += (int)((long)rules.Buildings[it.TypeIndex].Cost * Math.Max(0, 1000 - it.ProgressPermille) / 1000);
            }
            if (q.ReadyTypeIndex >= 0)
            {
                _queued[q.ReadyTypeIndex]++;
                laneBusy = true;
                if (TryPlace(k, sink, q.ReadyTypeIndex)) _failedSearches[lane] = 0;
                else if (++_failedSearches[lane] >= CancelAfterFailedSearches)
                {
                    // M13-06: nobody can place it; hand the credits back and free the lane instead of holding it for the match
                    sink.Issue(CommandKind.Cancel, EntityId.None, (ActorKind)(int)cat, default, default, Simulation.ReadyCancelPosition);
                    _failedSearches[lane] = 0; Cancelled++;
                    _banType = q.ReadyTypeIndex; _banLeft = CancelledTypeCooldown;
                    _queued[q.ReadyTypeIndex]--;
                }
            }
            else _failedSearches[lane] = 0;
        }

        int next = NextType(k);
        if (HoldExcept != int.MinValue && next != HoldExcept) next = HoldExcept >= 0 && k.BuildingCount[HoldExcept] + _queued[HoldExcept] == 0 ? HoldExcept : -1;
        // SUP-5: a supply structure that cannot start yet (the lane is busy) does not hold units back: it is cheap, and a tier gate under way is reserved already
        PendingCost = (next < 0 || (next == _supplyPick && laneBusy) ? 0 : rules.Buildings[next].Cost) + inProgress;
        if (laneBusy || next < 0) return;
        BuildingType bt = rules.Buildings[next];
        if (!k.Buildable(bt.Category, next)) return;
        sink.Build(bt.Category, next);
    }

    /// <summary>The structure to start next, or -1: the first unmet and buildable order step, with a power plant first when
    /// power would run short (or already is).</summary>
    private int NextType(RecruitKnowledge k)
    {
        SimRules rules = k.Rules;
        Array.Clear(_seen);
        int[] seen = _seen;
        int target = -1;
        for (int i = 0; i < _orderTypes.Count; i++)
        {
            int t = _orderTypes[i];
            seen[t]++;
            if (k.BuildingCount[t] + _queued[t] >= seen[t]) continue;
            if (t == _banType && _banLeft > 0) continue;   // M13-06: just cancelled for want of a site
            BuildingType bt = rules.Buildings[t];
            if (!k.Buildable(bt.Category, t)) continue;
            if (HoldStep != null && HoldStep(_order[i])) { TechSkips++; continue; }
            target = t;
            break;
        }
        bool powerShort = EconomyManager.PowerShort(k, target < 0 ? 0 : -rules.Buildings[target].Power);
        bool powerInFlight = _queued[_powerType] > 0;
        if (powerShort && !powerInFlight && (target < 0 || rules.Buildings[target].Power <= 0) && k.Buildable(ProductionCategory.Structure, _powerType))
            return _powerType;
        // SUP-5 (design 13 §6): a supply structure next when used + queued supply reaches cap - margin (and the ceiling is not reached), above
        // the build order. If its prerequisite is missing, that prerequisite comes first; power first if it would brown out.
        _supplyPick = -1;
        if (k.PackedRigs.Count == 0 && SupplyShort(k, SupplyMargin) && !SupplyInFlight(k))   // packed rigs: the cap is low only until they unpack
        {
            int supply = SupplyType(k);
            if (supply >= 0)
            {
                _supplyPick = supply;
                if (!powerInFlight && EconomyManager.PowerShort(k, -rules.Buildings[supply].Power) && k.Buildable(ProductionCategory.Structure, _powerType))
                    return _powerType;
                return supply;
            }
            int pre = SupplyPrerequisite(k);
            if (pre >= 0 && _queued[pre] == 0) { _supplyPick = pre; return pre; }
        }
        return target;
    }

    /// <summary>SUP-5: the supply margin of this AI (tiers 1..5 = 4, 5, 8, 10, 12); higher tiers build earlier. Set by the AI from its profile.</summary>
    public int SupplyMargin { get; set; } = 6;

    /// <summary>The supply margin for a difficulty tier 1..5.</summary>
    public static int MarginForTier(int tier) => Margins[Math.Clamp(tier, 1, 5) - 1];

    private static readonly int[] Margins = [4, 5, 8, 10, 12];   // (tune) design 13 §6 says 4, 6, 8, 10, 12; tier 2 is 5 so the garrison fill is not late

    private int _supplyPick = -1;

    /// <summary>SUP-5: supply of the units waiting in the unit queues (the front item included).</summary>
    internal static int QueuedSupply(RecruitKnowledge k)
    {
        int total = 0;
        foreach (QueueView q in k.Hud.Queues)
        {
            if (q.Category is not (ProductionCategory.Infantry or ProductionCategory.Vehicle or ProductionCategory.Aircraft)) continue;
            foreach (QueueItemView it in q.Items) total += k.Rules.Supply.SupplyOf(UnitCost(k, q.Category, it.TypeIndex), UnitSupply(k, q.Category, it.TypeIndex));
        }
        return total;
    }

    private static int UnitCost(RecruitKnowledge k, ProductionCategory c, int t) => c == ProductionCategory.Infantry ? k.Rules.Infantry[t].Cost : k.Rules.Vehicles[t].Cost;
    private static int UnitSupply(RecruitKnowledge k, ProductionCategory c, int t) => c == ProductionCategory.Infantry ? k.Rules.Infantry[t].Supply : k.Rules.Vehicles[t].Supply;

    /// <summary>SUP-5: true when supply is on, the cap is below the ceiling and used + queued supply is within <paramref name="margin"/> of the cap.</summary>
    internal static bool SupplyShort(RecruitKnowledge k, int margin)
    {
        SupplyView s = k.Hud.Supply;
        return s.Ceiling > 0 && s.Cap > 0 && s.Cap < s.Ceiling && s.Used + QueuedSupply(k) >= s.Cap - margin;
    }

    /// <summary>SUP-5: true when a structure that provides supply is already queued, ready to place or waiting in the lane.</summary>
    private bool SupplyInFlight(RecruitKnowledge k)
    {
        SimRules rules = k.Rules;
        for (int t = 0; t < _queued.Length; t++)
            if (_queued[t] > 0 && rules.Buildings[t].SupplyProvided > 0 && rules.Buildings[t].Id != _yardId) return true;
        return false;
    }

    /// <summary>SUP-5: the cheapest buildable structure that provides supply, other than the yard; -1 when none. Data-driven, so every
    /// faction (Authority Ration Hall, Directorate Bunk Gallery, Left Behind Still Rig) is covered without a per-faction id.</summary>
    private int SupplyType(RecruitKnowledge k)
    {
        SimRules rules = k.Rules;
        int best = -1;
        for (int t = 0; t < rules.Buildings.Count; t++)
        {
            BuildingType bt = rules.Buildings[t];
            if (bt.SupplyProvided <= 0 || bt.Id == _yardId || bt.Category != ProductionCategory.Structure) continue;
            if (!k.Buildable(ProductionCategory.Structure, t)) continue;
            if (best < 0 || bt.Cost < rules.Buildings[best].Cost) best = t;
        }
        return best;
    }

    /// <summary>SUP-5: when no supply structure is buildable yet, the first missing prerequisite (buildable now, not owned) of the cheapest
    /// own-faction supply structure; -1 when there is none.</summary>
    private int SupplyPrerequisite(RecruitKnowledge k)
    {
        SimRules rules = k.Rules;
        int yard = SimRules.IndexOf(rules.Buildings, _yardId, b => b.Id);
        if (yard < 0) return -1;
        int target = -1;
        for (int t = 0; t < rules.Buildings.Count; t++)
        {
            BuildingType bt = rules.Buildings[t];
            if (bt.SupplyProvided <= 0 || bt.Id == _yardId || bt.Category != ProductionCategory.Structure || bt.Faction != rules.Buildings[yard].Faction) continue;
            if (target < 0 || bt.Cost < rules.Buildings[target].Cost) target = t;
        }
        if (target < 0) return -1;
        foreach (string id in rules.Buildings[target].Prerequisites)
        {
            int p = SimRules.IndexOf(rules.Buildings, id, b => b.Id);
            if (p < 0 || k.BuildingCount[p] > 0) continue;
            if (k.Buildable(rules.Buildings[p].Category, p)) return p;
        }
        return -1;
    }

    // M13-18: ClearBuildArea (idle army ordered off the build area after a failed search) was removed: measured over 15 minutes for all four factions
    // at tier 1 and 5 (also with the supply-structure cap lifted) it was never reached, because the 24-cell wide search always found a site.

    /// <summary>Issues the Place for a ready structure; false when the search found no site or <c>CanPlace</c> refused it (both count as Unplaceable).</summary>
    private bool TryPlace(RecruitKnowledge k, ICommandSink sink, int type)
    {
        BuildingType bt = k.Rules.Buildings[type];
        bool front = bt.Category == ProductionCategory.Defence && bt.PrimaryWeapon >= 0;
        CellCoord cell;
        if (Placer is not null && Placer(k, type) is { Ok: true } chosen) cell = chosen.Cell;
        else if (!_search.Find(k, type, front, out cell)) { Unplaceable++; return false; }   // M13-06: S18-AI-NEW-2, no site at all counts too
        if (!k.View.CanPlace(type, cell)) { Unplaceable++; return false; }
        sink.Place(type, cell);
        Placed++;
        return true;
    }
    // ---- M3-21: AI save/restore. Appended at the end of the class; the format is private to the matching Restore. ----

    /// <summary>M3-21: writes the build order (including what the tier planner appended) and the placement counters.</summary>
    internal void Snapshot(BinaryWriter w)
    {
        w.Write(_order.Count);
        foreach (string s in _order) w.Write(s);
        w.Write(Placed); w.Write(Unplaceable); w.Write(PendingCost);
        w.Write(Cancelled); w.Write(_failedSearches[0]); w.Write(_failedSearches[1]); w.Write(_banType); w.Write(_banLeft);   // M13-06
    }

    /// <summary>M3-21: reads what <see cref="Snapshot"/> wrote. Types are resolved again on the next Think.</summary>
    internal void Restore(BinaryReader r, bool legacy = false)
    {
        int n = AiSnapshot.Count(r);
        _order.Clear(); _orderTypes.Clear();
        for (int i = 0; i < n; i++) _order.Add(r.ReadString());
        Placed = r.ReadInt32(); Unplaceable = r.ReadInt32(); PendingCost = r.ReadInt32();
        if (legacy) { Cancelled = 0; _failedSearches[0] = _failedSearches[1] = 0; _banType = -1; _banLeft = 0; return; }   // session 18 layout: no M13-06 fields
        Cancelled = r.ReadInt32(); _failedSearches[0] = r.ReadInt32(); _failedSearches[1] = r.ReadInt32(); _banType = r.ReadInt32(); _banLeft = r.ReadInt32();   // M13-06
    }
}
