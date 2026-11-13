// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Sim.Core;
using Rts.Sim.Missions;
using Rts.Sim.Rules;
using Rts.Sim.View;

namespace Rts.AI.Recruit;

/// <summary>
/// M4-12 (flag ReplaceHarvesters; finishing games): a side whose harvesters are all dead and whose funds cannot pay for a new one (or for
/// the refinery that brings one) has no way back, and in M3 that froze the game: both sides broke, a few units left, the yard's free
/// repair out-healing them. This sells the least needed structures (never the yard, a refinery, a factory, the last power plant or a
/// structure that is still needed to build the harvester), dearest refund first, one per <see cref="GapTicks"/>, until the funds cover the
/// harvester; items in production are cancelled first for their refund, and nothing else is bought meanwhile (<see cref="Saving"/>). The production manager then buys it (the harvester comes first, see <see cref="TierPlanner.WantedHarvester"/>).
/// </summary>
internal sealed class EconomyRescue
{
    /// <summary>Ticks between two sales.</summary>
    public const int GapTicks = 60;
    private int _last = -GapTicks;
    private int _harv = -2, _refinery = -2;
    private bool _hadHarvester;
    private static readonly ProductionCategory[] Lanes =
        [ProductionCategory.Structure, ProductionCategory.Defence, ProductionCategory.Vehicle, ProductionCategory.Infantry, ProductionCategory.Aircraft];

    private static readonly ProductionCategory[] BuildLanes = [ProductionCategory.Structure, ProductionCategory.Defence];

    /// <summary>Production items cancelled for their refund.</summary>
    public int Cancelled { get; private set; }

    /// <summary>Structures sold so far.</summary>
    public int Sold { get; private set; }

    /// <summary>True while the rescue is saving up: the build order and the unit lanes hold back (only the harvester, or the refinery
    /// that brings one, is bought), so the refunds are not spent on anything else.</summary>
    public bool Saving { get; private set; }

    /// <summary>The refinery type that is still allowed while <see cref="Saving"/> (it brings a free harvester), or -1.</summary>
    public int RefineryType => _refinery < 0 ? -1 : _refinery;

    /// <summary>Ticks without a harvester unloading after which a side counts as out of income (the map is mined out, or the
    /// harvesters cannot reach a field).</summary>
    public const int StalledIncomeTicks = 2400;   // (tune)

    /// <summary>True while the side's funds have not risen for <see cref="StalledIncomeTicks"/> (no income: the fields are mined out or
    /// the harvesters cannot reach them): it stops building structures, sells spare ones for one last army and empties its garrisons
    /// (see <see cref="GarrisonManager"/>).</summary>
    public bool Broke { get; private set; }

    private int _lastUnload;
    private readonly Dictionary<ulong, MissionKind> _harvMission = new();

    public void Think(RecruitKnowledge k, ICommandSink sink, RecruitFaction faction)
    {
        int tick = k.View.Tick;
        // an unload shows as a harvester going from Return back to Harvest (own missions are in the view; funds are no signal because
        // production spends credits as they arrive)
        foreach (ViewActor h in k.Harvesters)
        {
            ulong key = RecruitKnowledge.KeyOf(h);
            if (_harvMission.TryGetValue(key, out MissionKind before) && before == MissionKind.Return && h.Mission != MissionKind.Return) _lastUnload = tick;
            _harvMission[key] = h.Mission;
        }
        if (!_hadHarvester) _lastUnload = tick;
        Broke = _hadHarvester && tick - _lastUnload >= StalledIncomeTicks;
        if (Broke && k.Harvesters.Count > 0)
        {
            // income has stalled: stop paying for structures (cancel them for the refund), sell what the army does not need, units only
            Saving = false;
            if (tick - _last < GapTicks) return;
            foreach (ProductionCategory cat in BuildLanes)
                if (k.Queue(cat).Items.Count > 0)
                {
                    _last = tick; Cancelled++;
                    sink.Issue(CommandKind.Cancel, EntityId.None, (ActorKind)(int)cat);
                    return;
                }
            if (tick - _last >= GapTicks * 5) SellOne(k, sink, faction, keepProduction: true);
            return;
        }
        if (k.Harvesters.Count > 0) _hadHarvester = true;
        // both factions start without a harvester (it comes with the first refinery): the rescue is armed only once one was owned
        if (k.Harvesters.Count > 0 || !k.HaveYard || !_hadHarvester) { Saving = false; return; }
        if (tick - _last < GapTicks) return;
        SimRules rules = k.Rules;
        if (_harv == -2) _harv = SimRules.IndexOf(rules.Vehicles, faction.HarvesterId, v => v.Id);
        if (_refinery == -2) _refinery = SimRules.IndexOf(rules.Buildings, faction.RefineryId, b => b.Id);
        if (_harv < 0 || _refinery < 0) return;
        int funds = k.Hud.Cash + k.Hud.Stored;
        int unpaid = -1;   // S15: credits still owed on a harvester that is in the queue but stalled for lack of money
        foreach (QueueItemView it in k.Queue(ProductionCategory.Vehicle).Items)
            if (it.TypeIndex == _harv)
            {
                if (!it.Stalled) return;   // already bought and being built
                unpaid = (int)((long)rules.Vehicles[_harv].Cost * (1000 - it.ProgressPermille) / 1000);
            }
        // a refinery brings a free harvester; with one standing a harvester is bought at the factory
        int need = unpaid >= 0 ? unpaid : k.BuildingCount[_refinery] > 0 ? rules.Vehicles[_harv].Cost : rules.Buildings[_refinery].Cost;
        Saving = true;
        if (funds >= need) return;
        // money already sunk into a structure or unit in production comes back when it is cancelled (front items only)
        foreach (ProductionCategory cat in Lanes)
        {
            QueueView q = k.Queue(cat);
            if (q.Items.Count == 0) continue;
            int t = q.Items[0].TypeIndex;
            if (cat == ProductionCategory.Structure && t == _refinery) continue;
            if (cat == ProductionCategory.Vehicle && t == _harv) continue;
            _last = tick;
            Cancelled++;
            sink.Issue(CommandKind.Cancel, EntityId.None, (ActorKind)(int)cat);   // Arg = category, Arg2 = 0 (front item)
            return;
        }

        if (!SellOne(k, sink, faction, keepProduction: false)) Saving = false;   // nothing left to sell: spend what there is (Recruit rule)
    }

    /// <summary>s23 D-235 (B3): true while an own unit is on the EnterTunnel mission (walking to a hatch or tunnel entrance).</summary>
    internal static bool AnyEnteringTunnel(RecruitKnowledge k)
    {
        foreach (ViewActor a in k.Own) if (a.Mission == MissionKind.EnterTunnel) return true;
        return false;
    }

    /// <summary>Sells the structure with the dearest refund that is not needed (yard, refinery, factory, last power plant, an occupied
    /// garrison; with <paramref name="keepProduction"/> also the barracks and any producer). False when there is none.</summary>
    private bool SellOne(RecruitKnowledge k, ICommandSink sink, RecruitFaction faction, bool keepProduction)
    {
        SimRules rules = k.Rules;
        int powerPlants = 0;
        foreach (ViewActor b in k.Buildings) if (rules.Buildings[b.TypeIndex].Power > 0) powerPlants++;
        ViewActor? pick = null;
        int bestRefund = 0;
        bool entering = AnyEnteringTunnel(k);
        foreach (ViewActor b in k.Buildings)
        {
            BuildingType bt = rules.Buildings[b.TypeIndex];
            if (entering && bt.Entrance != TunnelSize.None) continue;   // s23 D-235 (B3): units still walk to a hatch; selling it strands them (d13: 10 conscripts, 10 000 ticks)
            if (bt.Id == faction.YardId || bt.Id == faction.RefineryId || bt.Id == faction.FactoryId || bt.Wall || bt.Gate) continue;
            if (keepProduction && (bt.Produces != null || bt.Id == faction.BarracksId || bt.PrimaryWeapon >= 0)) continue;
            if (bt.Power > 0 && powerPlants <= 1) continue;
            if (bt.Cost <= 0 || bt.Garrison > 0 && k.View.OccupantsOf(b.Id) > 0) continue;
            int refund = (int)((long)bt.Cost * rules.Economy.SellRefundPermille / 1000);
            if (refund > bestRefund) { bestRefund = refund; pick = b; }
        }
        if (pick is not ViewActor sell) return false;
        _last = k.View.Tick;
        Sold++;
        sink.Issue(CommandKind.Sell, sell.Id, ActorKind.Building);
        return true;
    }
    // ---- M3-21: AI save/restore. Appended at the end of the class; the format is private to the matching Restore. ----

    /// <summary>M3-21: writes the sale timer, the income watchdog and the counters.</summary>
    internal void Snapshot(BinaryWriter w)
    {
        w.Write(_last); w.Write(_harv); w.Write(_refinery); w.Write(_hadHarvester); w.Write(_lastUnload);
        AiSnapshot.WriteMap(w, _harvMission, (bw, m) => bw.Write((byte)m));
        w.Write(Cancelled); w.Write(Sold); w.Write(Saving); w.Write(Broke);
    }

    /// <summary>M3-21: reads what <see cref="Snapshot"/> wrote.</summary>
    internal void Restore(BinaryReader r)
    {
        _last = r.ReadInt32(); _harv = r.ReadInt32(); _refinery = r.ReadInt32(); _hadHarvester = r.ReadBoolean(); _lastUnload = r.ReadInt32();
        AiSnapshot.ReadMap(r, _harvMission, br => (MissionKind)br.ReadByte());
        Cancelled = r.ReadInt32(); Sold = r.ReadInt32(); Saving = r.ReadBoolean(); Broke = r.ReadBoolean();
    }
}
