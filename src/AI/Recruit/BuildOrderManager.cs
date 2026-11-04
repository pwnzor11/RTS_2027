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
    private readonly string[] _order;
    private readonly string _powerId, _yardId;
    private readonly PlacementSearch _search = new();
    private int[] _orderTypes = Array.Empty<int>();
    private int _powerType = -1;
    private int[] _queued = Array.Empty<int>();
    private int[] _seen = Array.Empty<int>();

    public BuildOrderManager(string[] order, string powerId, string yardId)
    {
        _order = order;
        _powerId = powerId; _yardId = yardId;
    }

    /// <summary>Credits the next structure (still to be started or being paid for) will need; units keep this in reserve.</summary>
    public int PendingCost { get; private set; }

    /// <summary>Number of structures placed so far by this AI.</summary>
    public int Placed { get; private set; }

    /// <summary>The Place commands that were rejected by <c>CanPlace</c> before issue: should always stay 0.</summary>
    public int Unplaceable { get; private set; }

    public void Think(RecruitKnowledge k, ICommandSink sink)
    {
        SimRules rules = k.Rules;
        if (_orderTypes.Length == 0)
        {
            _orderTypes = new int[_order.Length];
            for (int i = 0; i < _order.Length; i++)
            {
                _orderTypes[i] = SimRules.IndexOf(rules.Buildings, _order[i], b => b.Id);
                if (_orderTypes[i] < 0) throw new InvalidOperationException("build order names unknown structure '" + _order[i] + "'");
            }
            _powerType = SimRules.IndexOf(rules.Buildings, _powerId, b => b.Id);
            _queued = new int[rules.Buildings.Count];
            _seen = new int[rules.Buildings.Count];
        }
        if (!k.HaveYard) { PendingCost = 0; return; }

        Array.Clear(_queued);
        bool laneBusy = false;
        foreach (ProductionCategory cat in new[] { ProductionCategory.Structure, ProductionCategory.Defence })
        {
            QueueView q = k.Queue(cat);
            foreach (QueueItemView it in q.Items) { _queued[it.TypeIndex]++; laneBusy = true; }
            if (q.ReadyTypeIndex >= 0)
            {
                _queued[q.ReadyTypeIndex]++;
                laneBusy = true;
                TryPlace(k, sink, q.ReadyTypeIndex);
            }
        }

        int next = NextType(k);
        PendingCost = next < 0 ? 0 : rules.Buildings[next].Cost;
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
        foreach (int t in _orderTypes)
        {
            seen[t]++;
            if (k.BuildingCount[t] + _queued[t] >= seen[t]) continue;
            BuildingType bt = rules.Buildings[t];
            if (!k.Buildable(bt.Category, t)) continue;
            target = t;
            break;
        }
        bool powerShort = EconomyManager.PowerShort(k, target < 0 ? 0 : -rules.Buildings[target].Power);
        bool powerInFlight = _queued[_powerType] > 0;
        if (powerShort && !powerInFlight && (target < 0 || rules.Buildings[target].Power <= 0) && k.Buildable(ProductionCategory.Structure, _powerType))
            return _powerType;
        return target;
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
}
