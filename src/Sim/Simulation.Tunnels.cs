// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Sim.Core;
using Rts.Sim.Missions;
using Rts.Sim.Movement;
using Rts.Sim.Numerics;
using Rts.Sim.Rules;
using Rts.Sim.Tunnels;
using Rts.Sim.World;

namespace Rts.Sim;

// Tunnel network transit (design 05 §1, D-074). OWNER: M3-02. State: Tunnels (TunnelState.Queue / Transit / NextTicket,
// Vehicle/InfantryLayer). Constants: Setup.Rules.Tunnels.
//
// Life of a unit (its layer drives the phase; every phase is saved state, nothing lives outside the "tunnels" part):
//   Enter -> mission EnterTunnel: walk to the nearest own finished entrance that fits (not the destination).
//   Adjacent to it -> Queue entry (ticket = arrival order), layer Queued, mission Transit. It stands on the surface.
//   Head of its entrance's queue and room in the network -> a Transit entry is created while the unit is still Queued:
//     TicksLeft = points x the entrance's TicksPerPoint (the draw-in), its points count against the network cap from now.
//   Draw-in done -> layer Tunnel: off the map (occupancy released, position parked on the destination's centre),
//     TicksLeft = TransitTicks.
//   Transit done -> the exit cell (normal unit exit search around the destination's Exit) is claimed, layer Surfacing,
//     a Unit telegraph sits on it for TelegraphTicks; points are freed. Then layer Surface and mission Guard.
public sealed partial class Simulation
{
    /// <summary>Per tick (inside RunTunnels, before RunBurrowing): draw queued units in at each entrance's TicksPerPoint
    /// within the network cap, count transit down, surface arrivals at the destination exit.</summary>
    private void RunTunnelTransit()
    {
        if (_tunnels.Queue.Count == 0 && _tunnels.Transit.Count == 0) return;
        ValidateTunnelQueue();
        ValidateTunnelTransit();
        AdvanceTunnelTransit();
        StartTunnelDraws();
    }

    /// <summary>Enter with Target = own entrance building (the destination). <paramref name="unit"/> is an own live mobile actor.
    /// Refuse when the destination does not fit the unit's TunnelSize.</summary>
    private void ApplyEnterTunnel(Command c, ActorRef unit)
    {
        MobileStore store = StoreOf(unit.Kind)!;
        int i = unit.Id.Index;
        ActorLayer layer = _tunnels.LayerOf(unit.Kind, i);
        // ASSUMED (M3-02): a unit already underground (or in a transport) ignores Enter; a queued unit may be re-ordered.
        if (layer != ActorLayer.Surface && layer != ActorLayer.Queued) return;
        TunnelSize size = TunnelSizeOf(unit.Kind, i);
        int dest = c.Target.Index;
        if (!IsTunnelNode(dest, c.Player) || !FitsTunnel(size, BuildingTypeAt(dest).Entrance)) return;
        // ASSUMED (M3-02): the entry is never the destination itself; with no other own entrance that fits, the order is refused.
        if (NearestTunnelNode(c.Player, store.Position[i], size, c.Target).IsNone) return;
        if (layer == ActorLayer.Queued) LeaveTunnelQueue(unit);
        Queue(store, i, MissionKind.EnterTunnel, default, c.Target);
    }

    /// <summary>An own entrance building (slot <paramref name="buildingIndex"/>) is about to be removed: drop its queue and
    /// re-route transit units headed for it (D2), or lose them.</summary>
    private void OnEntranceRemoving(int buildingIndex)
    {
        EntityId lost = _buildings.Slots.IdOf(buildingIndex);
        byte owner = _buildings.Player[buildingIndex];
        LeptonCoord from = BuildingCentre(buildingIndex);
        List<TunnelQueueEntry> queue = _tunnels.Queue;
        for (int q = 0; q < queue.Count; q++)
        {
            TunnelQueueEntry e = queue[q];
            if (e.Entrance == lost) { DropQueueEntry(q--); continue; }
            if (e.Destination != lost) continue;
            EntityId next = NearestTunnelNode(owner, from, TunnelSizeOf(e.Unit.Kind, e.Unit.Id.Index), lost);
            // ASSUMED (M3-02): a queued unit (still on the surface) whose destination is lost with no other fitting entrance just
            // leaves the queue; only units already underground are lost.
            if (next.IsNone) { DropQueueEntry(q--); continue; }
            e.Destination = next;
            queue[q] = e;
            SetDrawDestination(e.Unit, next);
        }
        List<TunnelTransit> transit = _tunnels.Transit;
        for (int k = 0; k < transit.Count; k++)
        {
            TunnelTransit t = transit[k];
            if (t.Destination != lost || _tunnels.LayerOf(t.Unit.Kind, t.Unit.Id.Index) != ActorLayer.Tunnel) continue;
            if (!RerouteTransit(k, from, lost)) k--;
        }
        // ASSUMED (M3-02): units still walking to an entrance with this destination are re-targeted the same way (D2), or stop.
        RetargetWalkers(_units, ActorKind.Vehicle, lost, owner, from);
        RetargetWalkers(_infantry, ActorKind.Infantry, lost, owner, from);
    }

    // ---- mission steps (called from Missions/TunnelMissions.cs) -------------------------------------

    /// <summary>EnterTunnel: walk to the nearest own entrance that fits (re-picked when it is lost) and join its queue once
    /// next to its footprint. Target = the destination entrance; Destination = the centre of the entry's top-left cell.</summary>
    internal int TunnelEnterStep(MobileStore store, ActorKind kind, int i)
    {
        if (store.Health[i] <= 0) return 1;
        byte owner = store.Player[i];
        EntityId dest = store.Target[i];
        TunnelSize size = TunnelSizeOf(kind, i);
        if (!_buildings.Slots.IsAlive(dest) || !IsTunnelNode(dest.Index, owner) || !FitsTunnel(size, BuildingTypeAt(dest.Index).Entrance))
            return StopTunnelWalk(store, i);
        EntityId entry = _map.BuildingAt(store.Destination[i].ToCell());
        bool entryOk = store.Status[i] != MissionStatus.Init && !entry.IsNone && entry != dest && _buildings.Slots.IsAlive(entry)
            && IsTunnelNode(entry.Index, owner) && FitsTunnel(size, BuildingTypeAt(entry.Index).Entrance);
        if (!entryOk)
        {
            // ASSUMED (M3-02): "nearest" entry = straight-line distance from the unit to the entrance's centre (ties: lower slot).
            entry = NearestTunnelNode(owner, store.Position[i], size, dest);
            if (entry.IsNone) return StopTunnelWalk(store, i);
            store.Destination[i] = LeptonCoord.CenterOf(_buildings.Cell[entry.Index]);
            store.Status[i] = MissionStatus.Waiting;
        }
        BuildingType ebt = BuildingTypeAt(entry.Index);
        CellCoord tl = _buildings.Cell[entry.Index];
        if (NextToFootprint(Occupancy.CurCell(kind, store, i), tl, ebt.FootprintW, ebt.FootprintH))
        {
            JoinTunnelQueue(store, kind, i, entry, dest);
            return MissionHandlers.GuardSleepTicks;
        }
        return MoveMission.StepToward(this, store, kind, i, new CellCoord(tl.X + ebt.Exit.X, tl.Y + ebt.Exit.Y));
    }

    /// <summary>Transit: everything is driven by <see cref="RunTunnelTransit"/>; a unit back on the surface goes to Guard.</summary>
    internal int TunnelTransitStep(MobileStore store, ActorKind kind, int i)
    {
        ActorLayer layer = _tunnels.LayerOf(kind, i);
        if (layer is ActorLayer.Queued or ActorLayer.Tunnel or ActorLayer.Surfacing) return MissionHandlers.GuardSleepTicks;
        if (store.NextMission[i] == MissionKind.None) Queue(store, i, MissionKind.Guard, default, default);
        return 1;
    }

    private int StopTunnelWalk(MobileStore store, int i)
    {
        Queue(store, i, MissionKind.Guard, default, default);
        return 1;
    }

    private void JoinTunnelQueue(MobileStore store, ActorKind kind, int i, EntityId entry, EntityId dest)
    {
        MoveMission.DropRequest(this, store, i);
        Occupancy.DropReservation(this, kind, store, i);
        ClearRoute(store, i);
        store.Destination[i] = store.Position[i];
        store.Mission[i] = MissionKind.Transit;   // set now (not queued) so this tick's RunTunnelTransit already sees it
        store.Status[i] = MissionStatus.Init;
        store.Target[i] = dest;
        SetLayer(kind, i, ActorLayer.Queued);
        _tunnels.Queue.Add(new TunnelQueueEntry
        {
            Entrance = entry, Unit = new ActorRef(kind, store.Slots.IdOf(i)), Destination = dest, Ticket = _tunnels.NextTicket++,
        });
    }

    // ---- per-tick phases ------------------------------------------------------------------------

    /// <summary>Drop queue entries whose unit is gone, was given another order, or whose entrance is no longer an own finished
    /// node that fits; re-route those whose destination stopped being one (e.g. captured).</summary>
    private void ValidateTunnelQueue()
    {
        List<TunnelQueueEntry> queue = _tunnels.Queue;
        for (int q = 0; q < queue.Count; q++)
        {
            TunnelQueueEntry e = queue[q];
            MobileStore? store = StoreOf(e.Unit.Kind);
            int i = e.Unit.Id.Index;
            if (store == null || !store.Slots.IsAlive(e.Unit.Id) || store.Health[i] <= 0
                || _tunnels.LayerOf(e.Unit.Kind, i) != ActorLayer.Queued || store.Mission[i] != MissionKind.Transit)
            {
                DropQueueEntry(q--);
                continue;
            }
            byte owner = store.Player[i];
            TunnelSize size = TunnelSizeOf(e.Unit.Kind, i);
            if (!ValidNode(e.Entrance, owner, size)) { DropQueueEntry(q--); continue; }
            if (ValidNode(e.Destination, owner, size)) continue;
            LeptonCoord from = _buildings.Slots.IsAlive(e.Destination) ? BuildingCentre(e.Destination.Index) : store.Position[i];
            EntityId next = NearestTunnelNode(owner, from, size, e.Destination);
            if (next.IsNone) { DropQueueEntry(q--); continue; }
            e.Destination = next;
            queue[q] = e;
            SetDrawDestination(e.Unit, next);
        }
    }

    /// <summary>Drop transit entries of dead units and stale draw-ins; keep underground units on their Transit mission; re-route
    /// units whose destination stopped being a valid node.</summary>
    private void ValidateTunnelTransit()
    {
        List<TunnelTransit> transit = _tunnels.Transit;
        for (int k = 0; k < transit.Count; k++)
        {
            TunnelTransit t = transit[k];
            MobileStore? store = StoreOf(t.Unit.Kind);
            int i = t.Unit.Id.Index;
            if (store == null || !store.Slots.IsAlive(t.Unit.Id))
            {
                RemoveTelegraphOf(t.Unit);
                transit.RemoveAt(k--);
                continue;
            }
            ActorLayer layer = _tunnels.LayerOf(t.Unit.Kind, i);
            if (layer == ActorLayer.Queued)
            {
                if (QueueIndexOf(t.Unit) < 0) transit.RemoveAt(k--);   // its queue entry was dropped
                continue;
            }
            if (layer != ActorLayer.Tunnel && layer != ActorLayer.Surfacing) { transit.RemoveAt(k--); continue; }
            if (store.Mission[i] != MissionKind.Transit || store.NextMission[i] != MissionKind.None) PinUnderground(t.Unit.Kind, store, i);
            if (layer == ActorLayer.Tunnel && !ValidNode(t.Destination, t.Owner, TunnelSizeOf(t.Unit.Kind, i)))
            {
                LeptonCoord from = _buildings.Slots.IsAlive(t.Destination) ? BuildingCentre(t.Destination.Index) : store.Position[i];
                if (!RerouteTransit(k, from, t.Destination)) { k--; continue; }
                TunnelTransit r = transit[k];
                r.TicksLeft++;   // AdvanceTunnelTransit counts this tick off right after: the full TransitTicks remain
                transit[k] = r;
            }
        }
    }

    /// <summary>Count every transit entry down by one tick and move it to its next phase when it reaches zero.</summary>
    private void AdvanceTunnelTransit()
    {
        List<TunnelTransit> transit = _tunnels.Transit;
        TunnelRules rules = Setup.Rules.Tunnels;
        for (int k = 0; k < transit.Count; k++)
        {
            TunnelTransit t = transit[k];
            MobileStore store = StoreOf(t.Unit.Kind)!;
            int i = t.Unit.Id.Index;
            if (t.TicksLeft > 0) t.TicksLeft--;
            switch (_tunnels.LayerOf(t.Unit.Kind, i))
            {
                case ActorLayer.Queued:
                    if (t.TicksLeft > 0) break;
                    int q = QueueIndexOf(t.Unit);
                    t.Destination = _tunnels.Queue[q].Destination;
                    _tunnels.Queue.RemoveAt(q);
                    GoUnderground(t.Unit.Kind, store, i, t.Destination);
                    t.TicksLeft = rules.TransitTicks;
                    break;
                case ActorLayer.Tunnel:
                    // ASSUMED (M3-02): with no free exit cell (ring search of 8) the unit waits underground, still counting
                    // against the cap, and retries every tick.
                    if (t.TicksLeft == 0 && TrySurface(t, store, i)) t.TicksLeft = rules.TelegraphTicks;
                    break;
                case ActorLayer.Surfacing:
                    SyncTelegraph(t.Unit, t.TicksLeft);
                    if (t.TicksLeft > 0) break;
                    RemoveTelegraphOf(t.Unit);
                    SetLayer(t.Unit.Kind, i, ActorLayer.Surface);
                    Queue(store, i, MissionKind.Guard, default, default);
                    transit.RemoveAt(k--);
                    continue;
            }
            transit[k] = t;
        }
    }

    /// <summary>Each finished own entrance (slot order) whose queue head is not being drawn in starts drawing it in when the
    /// owner's network has room for its points.</summary>
    private void StartTunnelDraws()
    {
        List<TunnelQueueEntry> queue = _tunnels.Queue;
        if (queue.Count == 0) return;
        int cap = Setup.Rules.Tunnels.NetworkCapPoints;
        for (int b = 1; b < _buildings.Capacity; b++)
        {
            if (!_buildings.Slots.IsIndexAlive(b) || BuildingTypeAt(b).Entrance == TunnelSize.None) continue;
            EntityId id = _buildings.Slots.IdOf(b);
            int head = -1;
            for (int q = 0; q < queue.Count; q++)
                if (queue[q].Entrance == id && (head < 0 || queue[q].Ticket < queue[head].Ticket)) head = q;
            if (head < 0) continue;
            TunnelQueueEntry e = queue[head];
            if (TransitIndexOf(e.Unit) >= 0) continue;   // the head is being drawn in
            byte owner = _buildings.Player[b];
            int points = Setup.Rules.Tunnels.PointsOf(TunnelSizeOf(e.Unit.Kind, e.Unit.Id.Index));
            if (points <= 0 || NetworkPoints(owner) + points > cap) continue;   // a full network stalls every queue
            _tunnels.Transit.Add(new TunnelTransit
            {
                Unit = e.Unit, Owner = owner, Destination = e.Destination, Points = points,
                TicksLeft = points * Math.Max(1, BuildingTypeAt(b).TicksPerPoint),
            });
        }
    }

    // ---- phase changes ---------------------------------------------------------------------------

    /// <summary>Leave the surface: release the cell, forget the route, park the position on the destination's centre (inside
    /// its footprint, so it never blocks placement or exit searches).</summary>
    private void GoUnderground(ActorKind kind, MobileStore store, int i, EntityId dest)
    {
        MoveMission.DropRequest(this, store, i);
        Occupancy.Release(this, kind, store, i);
        ClearRoute(store, i);
        store.Position[i] = BuildingCentre(dest.Index);
        store.Destination[i] = store.Position[i];
        SetLayer(kind, i, ActorLayer.Tunnel);
    }

    /// <summary>Claim the exit cell of the destination (normal unit exit search) and start the Unit telegraph there.</summary>
    private bool TrySurface(TunnelTransit t, MobileStore store, int i)
    {
        int d = t.Destination.Index;
        BuildingType bt = BuildingTypeAt(d);
        CellCoord tl = _buildings.Cell[d];
        var exit = new CellCoord(tl.X + bt.Exit.X, tl.Y + bt.Exit.Y);
        Locomotion loco = t.Unit.Kind == ActorKind.Vehicle ? RulesOf(store.Player[i]).Vehicles[store.TypeIndex[i]].Locomotion : Locomotion.Foot;
        if (!FindFreeExitCell(exit, loco, out CellCoord cell)) return false;
        if (t.Unit.Kind == ActorKind.Infantry)
        {
            _infantry.SubSlot[i] = 0;
            store.Position[i] = InfantryStore.SlotPosition(cell, 0);
        }
        else store.Position[i] = LeptonCoord.CenterOf(cell);
        store.Destination[i] = store.Position[i];
        Occupancy.Claim(this, t.Unit.Kind, store, i);
        SetLayer(t.Unit.Kind, i, ActorLayer.Surfacing);
        _tunnels.Telegraphs.Add(new Telegraph
        {
            Cell = cell, Owner = t.Owner, TicksLeft = Setup.Rules.Tunnels.TelegraphTicks, Kind = TelegraphKind.Unit, Subject = t.Unit,
        });
        return true;
    }

    /// <summary>D2: send transit entry <paramref name="k"/> to the nearest surviving own entrance that fits (straight line from
    /// <paramref name="from"/>), restarting TransitTicks. With none, the unit is lost (killed, no refund) and the entry removed;
    /// returns false then.</summary>
    private bool RerouteTransit(int k, LeptonCoord from, EntityId lost)
    {
        TunnelTransit t = _tunnels.Transit[k];
        MobileStore store = StoreOf(t.Unit.Kind)!;
        int i = t.Unit.Id.Index;
        EntityId next = NearestTunnelNode(t.Owner, from, TunnelSizeOf(t.Unit.Kind, i), lost);
        if (next.IsNone)
        {
            store.Health[i] = 0;   // removed by RunCleanup
            _tunnels.Transit.RemoveAt(k);
            return false;
        }
        t.Destination = next;
        t.TicksLeft = Setup.Rules.Tunnels.TransitTicks;
        _tunnels.Transit[k] = t;
        store.Position[i] = BuildingCentre(next.Index);
        store.Destination[i] = store.Position[i];
        return true;
    }

    private void RetargetWalkers(MobileStore store, ActorKind kind, EntityId lost, byte owner, LeptonCoord from)
    {
        for (int i = 1; i < store.Capacity; i++)
        {
            if (!store.Slots.IsIndexAlive(i) || store.Mission[i] != MissionKind.EnterTunnel || store.Target[i] != lost) continue;
            EntityId next = NearestTunnelNode(owner, from, TunnelSizeOf(kind, i), lost);
            if (next.IsNone) { if (store.NextMission[i] == MissionKind.None) Queue(store, i, MissionKind.Guard, default, default); continue; }
            store.Target[i] = next;
            store.Status[i] = MissionStatus.Init;   // re-pick the entry (it must not be the new destination)
        }
    }

    /// <summary>Remove queue entry <paramref name="q"/> (and its draw-in, if any); the unit stays where it stands, back on the
    /// surface, and goes to Guard unless it was already given another order.</summary>
    private void DropQueueEntry(int q)
    {
        ActorRef unit = _tunnels.Queue[q].Unit;
        _tunnels.Queue.RemoveAt(q);
        int k = TransitIndexOf(unit);
        if (k >= 0) _tunnels.Transit.RemoveAt(k);
        MobileStore? store = StoreOf(unit.Kind);
        if (store == null || !store.Slots.IsAlive(unit.Id)) return;
        int i = unit.Id.Index;
        if (_tunnels.LayerOf(unit.Kind, i) == ActorLayer.Queued) SetLayer(unit.Kind, i, ActorLayer.Surface);
        if (store.Mission[i] == MissionKind.Transit && store.NextMission[i] == MissionKind.None) Queue(store, i, MissionKind.Guard, default, default);
    }

    /// <summary>A queued unit re-ordered with Enter: leave the queue without touching its mission.</summary>
    private void LeaveTunnelQueue(ActorRef unit)
    {
        int q = QueueIndexOf(unit);
        if (q >= 0) _tunnels.Queue.RemoveAt(q);
        int k = TransitIndexOf(unit);
        if (k >= 0) _tunnels.Transit.RemoveAt(k);
        SetLayer(unit.Kind, unit.Id.Index, ActorLayer.Surface);
    }

    /// <summary>An underground unit was given an order this tick (orders are not filtered by layer in ApplyInput): undo it.</summary>
    // ASSUMED (M3-02): orders to units in a tunnel or surfacing are ignored; the sim-level fix (skip them in ApplyInput) is
    // outside this package.
    private void PinUnderground(ActorKind kind, MobileStore store, int i)
    {
        MoveMission.DropRequest(this, store, i);
        Occupancy.DropReservation(this, kind, store, i);
        ClearRoute(store, i);
        store.Mission[i] = MissionKind.Transit;
        store.Status[i] = MissionStatus.Init;
        store.NextMission[i] = MissionKind.None;
        store.NextCell[i] = default;
        store.NextTarget[i] = default;
        store.Destination[i] = store.Position[i];
    }

    private static void ClearRoute(MobileStore store, int i)
    {
        store.PathActive[i] = false;
        store.PathCells[i] = null;
        store.PathPos[i] = 0;
        store.BlockedTicks[i] = 0;
        store.StuckCount[i] = 0;
    }

    // ---- queries ------------------------------------------------------------------------------------

    /// <summary>An own, live, finished (no Entrance telegraph on it) building with an entrance.</summary>
    private bool IsTunnelNode(int b, byte owner)
    {
        if (!_buildings.Slots.IsIndexAlive(b) || _buildings.Player[b] != owner || _buildings.Health[b] <= 0) return false;
        if (BuildingTypeAt(b).Entrance == TunnelSize.None) return false;
        EntityId id = _buildings.Slots.IdOf(b);
        foreach (Telegraph g in _tunnels.Telegraphs)
            if (g.Kind == TelegraphKind.Entrance && g.Subject.Kind == ActorKind.Building && g.Subject.Id == id) return false;
        return true;
    }

    private bool ValidNode(EntityId id, byte owner, TunnelSize size) =>
        _buildings.Slots.IsAlive(id) && IsTunnelNode(id.Index, owner) && FitsTunnel(size, BuildingTypeAt(id.Index).Entrance);

    /// <summary>The unit size fits the entrance: it uses the network at all (S or M) and is not larger than the entrance.</summary>
    private bool FitsTunnel(TunnelSize unit, TunnelSize entrance) =>
        Setup.Rules.Tunnels.PointsOf(unit) > 0 && entrance != TunnelSize.None && unit <= entrance;

    private TunnelSize TunnelSizeOf(ActorKind kind, int i)
    {
        SimRules r = RulesOf(PlayerAt(kind, i));
        return kind switch
        {
            ActorKind.Vehicle => r.Vehicles[_units.TypeIndex[i]].TunnelSize,
            ActorKind.Infantry => r.Infantry[_infantry.TypeIndex[i]].TunnelSize,
            _ => TunnelSize.None,
        };
    }

    /// <summary>Nearest own finished entrance that fits <paramref name="size"/>, by straight-line distance from
    /// <paramref name="from"/> to its centre (ties: lower slot), skipping <paramref name="exclude"/>; none if there is none.</summary>
    private EntityId NearestTunnelNode(byte owner, LeptonCoord from, TunnelSize size, EntityId exclude)
    {
        EntityId best = EntityId.None;
        long bestD = long.MaxValue;
        for (int b = 1; b < _buildings.Capacity; b++)
        {
            if (!IsTunnelNode(b, owner) || !FitsTunnel(size, BuildingTypeAt(b).Entrance)) continue;
            EntityId id = _buildings.Slots.IdOf(b);
            if (id == exclude) continue;
            long d = LeptonMath.DistanceSquared(from, BuildingCentre(b));
            if (d < bestD) { bestD = d; best = id; }
        }
        return best;
    }

    /// <summary>Points in the owner's network: draw-ins in progress and units underground (surfacing units no longer count).</summary>
    private int NetworkPoints(byte owner)
    {
        int sum = 0;
        foreach (TunnelTransit t in _tunnels.Transit)
        {
            if (t.Owner != owner) continue;
            ActorLayer layer = _tunnels.LayerOf(t.Unit.Kind, t.Unit.Id.Index);
            if (layer is ActorLayer.Queued or ActorLayer.Tunnel) sum += t.Points;
        }
        return sum;
    }

    private static bool NextToFootprint(CellCoord c, CellCoord tl, int w, int h)
    {
        int dx = c.X < tl.X ? tl.X - c.X : c.X >= tl.X + w ? c.X - (tl.X + w - 1) : 0;
        int dy = c.Y < tl.Y ? tl.Y - c.Y : c.Y >= tl.Y + h ? c.Y - (tl.Y + h - 1) : 0;
        return Math.Max(dx, dy) <= 1;
    }

    private int QueueIndexOf(ActorRef unit)
    {
        for (int q = 0; q < _tunnels.Queue.Count; q++) if (_tunnels.Queue[q].Unit == unit) return q;
        return -1;
    }

    private int TransitIndexOf(ActorRef unit)
    {
        for (int k = 0; k < _tunnels.Transit.Count; k++) if (_tunnels.Transit[k].Unit == unit) return k;
        return -1;
    }

    private void SetDrawDestination(ActorRef unit, EntityId dest)
    {
        int k = TransitIndexOf(unit);
        if (k < 0) return;
        TunnelTransit t = _tunnels.Transit[k];
        t.Destination = dest;
        _tunnels.Transit[k] = t;
    }

    private void SetLayer(ActorKind kind, int i, ActorLayer layer)
    {
        if (kind == ActorKind.Vehicle) _tunnels.VehicleLayer[i] = layer;
        else if (kind == ActorKind.Infantry) _tunnels.InfantryLayer[i] = layer;
    }

    // ASSUMED (M3-02): the Unit telegraph of a network arrival is owned by the transit entry (its TicksLeft is copied here every
    // tick); RunBurrowing (M3-03) should leave Unit telegraphs whose subject is in Tunnels.Transit alone.
    private void SyncTelegraph(ActorRef unit, int ticksLeft)
    {
        List<Telegraph> list = _tunnels.Telegraphs;
        for (int g = 0; g < list.Count; g++)
        {
            if (list[g].Kind != TelegraphKind.Unit || list[g].Subject != unit) continue;
            Telegraph tg = list[g];
            tg.TicksLeft = ticksLeft;
            list[g] = tg;
            return;
        }
    }

    private void RemoveTelegraphOf(ActorRef unit)
    {
        List<Telegraph> list = _tunnels.Telegraphs;
        for (int g = 0; g < list.Count; g++)
            if (list[g].Kind == TelegraphKind.Unit && list[g].Subject == unit) { list.RemoveAt(g); return; }
    }
}
