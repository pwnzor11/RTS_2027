// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Sim.Core;
using Rts.Sim.Rules;

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

    /// <summary>M4-12: when true (tier 2+), <see cref="PendingCost"/> also counts what the structures in production still have to be paid,
    /// so units do not starve a 2500-credit tier gate that is already under way (it stalled for whole games).</summary>
    public bool ReserveInProgress { get; set; }

    /// <summary>M4-16: when set and true for a structure id, that order step is skipped for now (tech held while the enemy army is bigger).</summary>
    public Func<string, bool>? HoldStep { get; set; }

    /// <summary>M4-16: decisions on which a tech step was skipped.</summary>
    public int TechSkips { get; private set; }

    /// <summary>Credits the next structure (still to be started or being paid for) will need; units keep this in reserve.</summary>
    public int PendingCost { get; private set; }

    /// <summary>Number of structures placed so far by this AI.</summary>
    public int Placed { get; private set; }

    /// <summary>The Place commands that were rejected by <c>CanPlace</c> before issue: should always stay 0.</summary>
    public int Unplaceable { get; private set; }

    public void Think(RecruitKnowledge k, ICommandSink sink)
    {
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
            foreach (QueueItemView it in q.Items)
            {
                _queued[it.TypeIndex]++; laneBusy = true;
                if (ReserveInProgress) inProgress += (int)((long)rules.Buildings[it.TypeIndex].Cost * Math.Max(0, 1000 - it.ProgressPermille) / 1000);
            }
            if (q.ReadyTypeIndex >= 0)
            {
                _queued[q.ReadyTypeIndex]++;
                laneBusy = true;
                TryPlace(k, sink, q.ReadyTypeIndex);
            }
        }

        int next = NextType(k);
        if (HoldExcept != int.MinValue && next != HoldExcept) next = HoldExcept >= 0 && k.BuildingCount[HoldExcept] + _queued[HoldExcept] == 0 ? HoldExcept : -1;
        PendingCost = (next < 0 ? 0 : rules.Buildings[next].Cost) + inProgress;
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
        // SUP-5: a supply structure next when the cap is nearly reached (and the ceiling is not), power first if it would brown out.
        int supply = SupplyShort(k) ? SupplyType(k) : -1;
        if (supply >= 0 && _queued[supply] == 0)
        {
            if (!powerInFlight && EconomyManager.PowerShort(k, -rules.Buildings[supply].Power) && k.Buildable(ProductionCategory.Structure, _powerType))
                return _powerType;
            return supply;
        }
        return target;
    }

    /// <summary>SUP-5: free supply below which the AI starts a supply structure (units cost 1 to 3).</summary>
    internal const int SupplyHeadroom = 6;

    /// <summary>SUP-5: true when supply is on, the cap is below the ceiling and free supply is under <see cref="SupplyHeadroom"/>.</summary>
    internal static bool SupplyShort(RecruitKnowledge k)
    {
        SupplyView s = k.Hud.Supply;
        return s.Ceiling > 0 && s.Cap > 0 && s.Cap < s.Ceiling && s.Cap - s.Used < SupplyHeadroom;
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

    private void TryPlace(RecruitKnowledge k, ICommandSink sink, int type)
    {
        BuildingType bt = k.Rules.Buildings[type];
        bool front = bt.Category == ProductionCategory.Defence && bt.PrimaryWeapon >= 0;
        if (!_search.Find(k, type, front, out CellCoord cell)) return;
        if (!k.View.CanPlace(type, cell)) { Unplaceable++; return; }
        sink.Place(type, cell);
        Placed++;
    }
    // ---- M3-21: AI save/restore. Appended at the end of the class; the format is private to the matching Restore. ----

    /// <summary>M3-21: writes the build order (including what the tier planner appended) and the placement counters.</summary>
    internal void Snapshot(BinaryWriter w)
    {
        w.Write(_order.Count);
        foreach (string s in _order) w.Write(s);
        w.Write(Placed); w.Write(Unplaceable); w.Write(PendingCost);
    }

    /// <summary>M3-21: reads what <see cref="Snapshot"/> wrote. Types are resolved again on the next Think.</summary>
    internal void Restore(BinaryReader r)
    {
        int n = AiSnapshot.Count(r);
        _order.Clear(); _orderTypes.Clear();
        for (int i = 0; i < n; i++) _order.Add(r.ReadString());
        Placed = r.ReadInt32(); Unplaceable = r.ReadInt32(); PendingCost = r.ReadInt32();
    }
}
