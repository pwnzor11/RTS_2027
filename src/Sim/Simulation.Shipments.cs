// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Sim.Core;
using Rts.Sim.Missions;
using Rts.Sim.Rules;
using Rts.Sim.Shipments;

namespace Rts.Sim;

// M7-06 (docs/design/11_m7_plan.md §2.5 "Supply line", docs/design/07_expedition.md §2.2, D-126): the home supply line.
// Everything is behind [shipments] enabled plus a tender_type; with either missing RunShipments returns at once and the "shipments"
// part stays empty, so existing games hash exactly as before. State: the "shipments" part (ShipmentState). Hook: RunShipments is called
// from StepCore right after ApplyInput, so the Move orders it issues are resolved by RunMovement in the same tick (no pending state
// survives between ticks, which keeps save/load exact).
public sealed partial class Simulation
{
    /// <summary>Ticks between two checks of the charter (a refinery with a working harvester near it); progress grows by this much per check.</summary>
    private const int CharterCheckTicks = 10;

    /// <summary>Cells (Chebyshev, from the refinery's top-left cell) within which an own working harvester counts as holding a field for the charter.</summary>
    private const int CharterRange = 16;

    /// <summary>Ticks between two re-issues of a Move order to an idle tender that has not arrived yet.</summary>
    private const int TenderReorderTicks = 20;

    /// <summary>Index of the tender vehicle type, or -1 when shipments are off (disabled, or no <c>tender_type</c>).</summary>
    private int TenderTypeIndex()
    {
        ShipmentRules sr = Setup.Rules.Shipments;
        if (!sr.Enabled || sr.TenderType.Length == 0) return -1;
        return SimRules.IndexOf(Setup.Rules.Vehicles, sr.TenderType, v => v.Id);
    }

    /// <summary>Highest decay step worth keeping: the step at which the payload sits on its floor (decay steps beyond it change nothing).</summary>
    private int MaxDecayStep()
    {
        ShipmentRules sr = Setup.Rules.Shipments;
        if (sr.DecayPermille <= 0) return 0;
        int span = Math.Max(0, 1000 - sr.FloorPermille);
        return (span + sr.DecayPermille - 1) / sr.DecayPermille;
    }

    /// <summary>Credits the next shipment of <paramref name="player"/> would carry: <c>value x (1000 - decay) / 1000</c>, never below the floor.</summary>
    internal int ShipmentPayload(byte player)
    {
        ShipmentRules sr = Setup.Rules.Shipments;
        long decay = Math.Min(1000L, (long)_shipments.DecayStep[player] * sr.DecayPermille);
        long payload = sr.Value * (1000 - decay) / 1000;
        long floor = sr.Value * (long)sr.FloorPermille / 1000;
        return (int)Math.Max(payload, floor);
    }

    private static int PackVehicle(EntityId id) => unchecked((int)(((uint)id.Index << 16) | id.Generation));

    private static EntityId UnpackVehicle(int packed) => new((ushort)((uint)packed >> 16), (ushort)(packed & 0xFFFF));

    private bool IsDock(int b) =>
        _buildings.Slots.IsIndexAlive(b) && _buildings.Health[b] > 0 && RulesOf(_buildings.Player[b]).Buildings[_buildings.TypeIndex[b]].ShipmentDock;

    /// <summary>Home shipments, once per tick before missions: tenders arrive or sink, charters count, timers fire (see file header).</summary>
    private void RunShipments()
    {
        int tenderType = TenderTypeIndex();
        if (tenderType < 0) return;
        ShipmentRules sr = Setup.Rules.Shipments;
        int n = _players.Count;
        Span<int> docks = stackalloc int[n];
        for (int b = 1; b < _buildings.Capacity; b++)
            if (IsDock(b)) docks[_buildings.Player[b]]++;

        RunTenders();

        for (int p = 0; p < n; p++)
        {
            if (docks[p] == 0) { _shipments.NextShipmentTick[p] = 0; _shipments.CharterProgress[p] = 0; continue; }   // no dock: no schedule
            if (Tick % CharterCheckTicks == 0) RunCharter((byte)p, sr);
            if (_shipments.NextShipmentTick[p] == 0) { _shipments.NextShipmentTick[p] = Tick + RulesOf((byte)p).Shipments.IntervalTicks; continue; }
            if (Tick < _shipments.NextShipmentTick[p]) continue;
            _shipments.NextShipmentTick[p] = Tick + RulesOf((byte)p).Shipments.IntervalTicks;   // simfu-3: Fast Charging
            int inFlight = 0;
            foreach (Tender t in _shipments.Tenders) if (t.Player == p) inFlight++;
            if (inFlight >= sr.TendersMax) continue;   // sea lane full: this shipment is skipped
            LaunchTender((byte)p, tenderType);
        }
    }

    // Charter: the shipment decay is won back one step at a time while the player keeps a field worked. "Held" has no ownership in the
    // sim (pits and ash fields are neutral overlay cells), so a field counts as held while an own refinery has an own live harvester that
    // is on a Harvest or Return mission within CharterRange cells of it; leaving that state resets the progress ("continuously").
    private void RunCharter(byte p, ShipmentRules sr)
    {
        if (sr.CharterTicks <= 0 || _shipments.DecayStep[p] == 0) { _shipments.CharterProgress[p] = 0; return; }
        if (!FieldHeld(p)) { _shipments.CharterProgress[p] = 0; return; }
        _shipments.CharterProgress[p] += CharterCheckTicks;
        if (_shipments.CharterProgress[p] < sr.CharterTicks) return;
        _shipments.CharterProgress[p] = 0;
        _shipments.DecayStep[p]--;
    }

    private bool FieldHeld(byte p)
    {
        SimRules rules = RulesOf(p);
        for (int b = 1; b < _buildings.Capacity; b++)
        {
            if (!_buildings.Slots.IsIndexAlive(b) || _buildings.Player[b] != p || _buildings.Health[b] <= 0) continue;
            if (!rules.Buildings[_buildings.TypeIndex[b]].Refinery) continue;
            CellCoord at = _buildings.Cell[b];
            for (int v = 1; v < _units.Capacity; v++)
            {
                if (!_units.Slots.IsIndexAlive(v) || _units.Player[v] != p || _units.Health[v] <= 0) continue;
                if (rules.Vehicles[_units.TypeIndex[v]].HarvestCapacity <= 0) continue;
                MissionKind m = _units.Mission[v];
                if (m != MissionKind.Harvest && m != MissionKind.Return) continue;
                CellCoord c = _units.Position[v].ToCell();
                if (Math.Max(Math.Abs(c.X - at.X), Math.Abs(c.Y - at.Y)) <= CharterRange) return true;
            }
        }
        return false;
    }

    // Tenders in creation order: sunk ones are dropped (nothing is paid, D-126), arrivals pay and leave, the rest keep their cell current
    // and are re-ordered when idle.
    private void RunTenders()
    {
        List<Tender> list = _shipments.Tenders;
        for (int k = 0; k < list.Count; k++)
        {
            Tender t = list[k];
            EntityId id = UnpackVehicle(t.Vehicle);
            if (t.Vehicle == 0 || !_units.Slots.IsAlive(id) || _units.Health[id.Index] <= 0 || _units.Player[id.Index] != t.Player)
            {
                list.RemoveAt(k--);   // sunk at sea (or captured): the credits are lost
                continue;
            }
            int v = id.Index;
            CellCoord c = _units.Position[v].ToCell();
            if (c.X != t.CellX || c.Y != t.CellY) { t = t with { CellX = c.X, CellY = c.Y }; list[k] = t; }
            if (DockNextTo(t.Player, c, out _))
            {
                Refund(t.Player, t.Payload);
                OnStatsShipment(t.Player, t.Payload);
                OnActorRemoving(ActorKind.Vehicle, v);
                OnM3MobileRemoving(ActorKind.Vehicle, v);
                _units.Remove(id);
                list.RemoveAt(k--);
                continue;
            }
            if (_units.Mission[v] == MissionKind.None && _units.NextMission[v] == MissionKind.None && (Tick + t.Id) % TenderReorderTicks == 0)
                OrderToDock(t, id);
        }
    }

    /// <summary>True if cell <paramref name="c"/> touches (Chebyshev distance 1 or less from) the footprint of an own live dock.</summary>
    private bool DockNextTo(byte player, CellCoord c, out int dock)
    {
        for (int b = 1; b < _buildings.Capacity; b++)
        {
            dock = b;
            if (!IsDock(b) || _buildings.Player[b] != player) continue;
            BuildingType bt = RulesOf(player).Buildings[_buildings.TypeIndex[b]];
            CellCoord tl = _buildings.Cell[b];
            int dx = Math.Max(Math.Max(tl.X - c.X, c.X - (tl.X + bt.FootprintW - 1)), 0);
            int dy = Math.Max(Math.Max(tl.Y - c.Y, c.Y - (tl.Y + bt.FootprintH - 1)), 0);
            if (Math.Max(dx, dy) <= 1) return true;
        }
        dock = 0;
        return false;
    }

    // Sends the tender to the free water cell beside the nearest own dock (ties: lower slot, lower cell index) with the ordinary Move order.
    private void OrderToDock(Tender t, EntityId id)
    {
        CellCoord from = _units.Position[id.Index].ToCell();
        long bestDock = long.MaxValue;
        int dockSlot = 0;
        for (int b = 1; b < _buildings.Capacity; b++)
        {
            if (!IsDock(b) || _buildings.Player[b] != t.Player) continue;
            BuildingType bt = RulesOf(t.Player).Buildings[_buildings.TypeIndex[b]];
            CellCoord tl = _buildings.Cell[b];
            long dx = tl.X * 2L + bt.FootprintW - 1 - from.X * 2L, dy = tl.Y * 2L + bt.FootprintH - 1 - from.Y * 2L;
            long d = dx * dx + dy * dy;
            if (d < bestDock) { bestDock = d; dockSlot = b; }
        }
        if (dockSlot == 0) return;   // no dock: the tender waits at sea
        BuildingType dt = RulesOf(t.Player).Buildings[_buildings.TypeIndex[dockSlot]];
        CellCoord dtl = _buildings.Cell[dockSlot];
        long best = long.MaxValue;
        CellCoord goal = default;
        bool found = false;
        for (int y = dtl.Y - 1; y <= dtl.Y + dt.FootprintH; y++)
            for (int x = dtl.X - 1; x <= dtl.X + dt.FootprintW; x++)
            {
                var c = new CellCoord(x, y);
                if (!InMap(c) || !_map.IsPassableTerrain(c, Locomotion.Naval, Setup.Rules.Terrain) || !_map.BuildingAt(c).IsNone) continue;
                long dx = c.X - (long)from.X, dy = c.Y - (long)from.Y;
                long d = dx * dx + dy * dy;
                if (d < best) { best = d; goal = c; found = true; }   // scan order = cell-index order, so the first of equals wins
            }
        if (!found) return;
        ApplyMovementCommand(new Command(Tick, t.Player, 0, CommandKind.Move, id, EntityId.None, goal, (int)ActorKind.Vehicle));
    }

    // Spawns the tender on an ocean-edge cell: the edge water cells in cell-index order, starting at index (NextTenderId mod count) so
    // consecutive shipments rotate round the coast; the first free one from there on is used. No SimRandom.
    private void LaunchTender(byte p, int tenderType)
    {
        int payload = ShipmentPayload(p);
        if (payload <= 0) return;
        int w = Setup.Map.Width, h = Setup.Map.Height;
        var edge = new List<CellCoord>();
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                if (y != 0 && y != h - 1 && x != 0 && x != w - 1) { x = w - 2; continue; }   // interior row: jump to the last column
                var c = new CellCoord(x, y);
                if (_map.IsPassableTerrain(c, Locomotion.Naval, Setup.Rules.Terrain)) edge.Add(c);
            }
        if (edge.Count == 0) return;   // no edge water (Expedition cannot be picked there, D-124): no shipment
        int start = (int)((uint)_shipments.NextTenderId % (uint)edge.Count);
        for (int k = 0; k < edge.Count; k++)
        {
            CellCoord c = edge[(start + k) % edge.Count];
            if (!_map.BuildingAt(c).IsNone || !_map.VehicleAt(c).IsNone || ActorStandsOn(c)) continue;
            EntityId id = SpawnVehicle(p, tenderType, c);
            if (id.IsNone) return;   // vehicle store full: try again next interval
            var t = new Tender(_shipments.NextTenderId++, p, c.X, c.Y, payload, PackVehicle(id));
            _shipments.Tenders.Add(t);
            _shipments.DecayStep[p] = Math.Min(_shipments.DecayStep[p] + 1, MaxDecayStep());
            OrderToDock(t, id);
            return;
        }
    }
}
