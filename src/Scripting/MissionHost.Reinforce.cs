// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using MoonSharp.Interpreter;
using Rts.Sim;
using Rts.Sim.Core;
using Rts.Sim.Map;
using Rts.Sim.Missions;
using Rts.Sim.Rules;
using Rts.Sim.World;

namespace Rts.Scripting;

// M8-04 owns this file: reinforcement variants and spawn groups (docs/missions/api.md).
// Reinforce.Edge (pilot) stays in MissionHost.Api.cs and must not change.
//
// Decisions (M8-04):
//  * Every spawn goes through the pilot path (SpawnActor -> Simulation.SpawnVehicle / SpawnInfantry) and the scripting orders the pilot
//    uses (AssignAttackMove / AssignMission). The few things the pilot path cannot reach (boarding, unloading, unpacking, rank) go through
//    the public wrappers of Simulation.MissionSpawns.cs, which run the rules a player command would run.
//  * Free cells: new spawns look for an empty cell of the right terrain, nearest first (rings, row-major inside a ring); nothing is placed
//    on an occupied cell. When no cell is free within the search radius the call is a script error.
//  * Delayed and Transport arrivals are queued state (PendingArrival) in the version-4 tail of the "mission" part.
public sealed partial class MissionHost
{
    private static readonly string[] Formations = { "line", "column", "box", "scatter" };

    /// <summary>Largest ring searched for a free cell around the wanted one.</summary>
    private const int FreeCellRing = 10;

    /// <summary>Ticks a transport may drive before it unloads wherever it is (it normally unloads on reaching the destination).</summary>
    private const int TransportDeadlineTicks = 3000;

    // Queued reinforcement work (M8-04), saved in the version-4 tail.
    private readonly List<PendingArrival> _arrivals = new();

    /// <summary>Number of queued arrivals (Reinforce.Delayed timers and transports still driving to their drop point); for tests and tools.</summary>
    internal int PendingArrivalCount => _arrivals.Count;

    private void RegisterReinforceApi(Script s)
    {
        NewApiTable(s, "Reinforce",
            ("Zone", a =>
            {
                int p = PlayerArg(a, 0);
                List<string> types = StrList(a, 1, "type ids", 32, types: true);
                string zone = ZoneName(a, 2);
                return TagTable(ReinforceZone(p, types, ZoneOf(zone), CellArg(a, 3)));
            }),
            ("Transport", a =>
            {
                int p = PlayerArg(a, 0);
                List<string> types = StrList(a, 1, "type ids", 32, types: true);
                CellCoord edge = EdgeCellArg(a, 2);
                return TagTable(ReinforceTransport(p, types, edge, CellArg(a, 3)));
            }),
            ("Delayed", a =>
            {
                int ticks = Int(a, 0, 1, 1_000_000_000);
                int p = PlayerArg(a, 1);
                List<string> types = StrList(a, 2, "type ids", 32, types: true);
                CellCoord edge = EdgeCellArg(a, 3);
                _arrivals.Add(new PendingArrival
                {
                    Kind = ArrivalKind.Delayed, Tick = (int)Math.Min((long)int.MaxValue, (long)_sim!.Tick + ticks), Player = p,
                    Edge = edge, Dest = CellArg(a, 4), Types = types,
                });
                return DynValue.Nil;
            }),
            ("Naval", a =>
            {
                int p = PlayerArg(a, 0);
                List<string> types = StrList(a, 1, "type ids", 32, types: true);
                CellCoord edge = EdgeCellArg(a, 2);
                return TagTable(ReinforceNaval(p, types, edge, CellArg(a, 3)));
            }),
            ("Rig", a =>
            {
                int p = PlayerArg(a, 0);
                CellCoord edge = EdgeCellArg(a, 1);
                return DynValue.NewString(ReinforceRig(p, edge, CellArg(a, 2)));
            }));

        NewApiTable(s, "Actor",
            ("CreateGroup", a =>
            {
                List<string> types = StrList(a, 0, "type ids", 32, types: true);
                int p = PlayerArg(a, 1);
                CellCoord cell = CellArg(a, 2);
                string formation = "box", prefix = "";
                int rank = 0;
                if (!Absent(a, 3))
                {
                    GroupOptions(a, 3);
                    Table o = a[3].Table;
                    if (o.Get("formation") is { Type: DataType.String } f) formation = f.String;
                    if (o.Get("rank") is { Type: DataType.Number } r) rank = (int)r.Number;
                    if (o.Get("tagPrefix") is { Type: DataType.String } t) prefix = t.String;
                }

                return TagTable(CreateGroup(types, p, cell, formation, rank, prefix));
            }));
    }

    /// <summary>The optional options table of Actor.CreateGroup: {formation = "line"|"column"|"box"|"scatter", rank = 0..3, tagPrefix = "x"}.</summary>
    private void GroupOptions(CallbackArguments a, int i)
    {
        DynValue o = a[i];
        if (o.Type != DataType.Table) throw Err("argument " + (i + 1) + " (options) must be a table, got " + o.Type.ToLuaTypeString());
        DynValue f = o.Table.Get("formation");
        if (f.Type != DataType.Nil && (f.Type != DataType.String || Array.IndexOf(Formations, f.String) < 0))
            throw Err("options.formation must be one of " + string.Join(", ", Formations));
        DynValue r = o.Table.Get("rank");
        if (r.Type != DataType.Nil) IntOf(r, "options.rank", 0, Rts.Sim.Rules.VeterancyRules.Ranks);
        DynValue t = o.Table.Get("tagPrefix");
        if (t.Type != DataType.Nil && (t.Type != DataType.String || t.String.Length is 0 or > 32)) throw Err("options.tagPrefix must be a string of 1-32 characters");
    }

    private DynValue TagTable(List<string> tags)
    {
        var t = new Table(_script);
        for (int k = 0; k < tags.Count; k++) t.Set(k + 1, DynValue.NewString(tags[k]));
        return DynValue.NewTable(t);
    }

    // ---- cells ----

    private static int Cheb(CellCoord a, CellCoord b) => Math.Max(Math.Abs(a.X - b.X), Math.Abs(a.Y - b.Y));

    /// <summary>The cell nearest <paramref name="want"/> (rings of growing size, row-major inside a ring) where an actor of this locomotion can be
    /// placed now: right terrain, no building, no vehicle, no soldier. Null when none lies within <paramref name="maxRing"/>.</summary>
    private CellCoord? FreeCellNear(CellCoord want, Locomotion loco, int maxRing, Func<CellCoord, bool>? accept = null)
    {
        Simulation sim = _sim!;
        for (int r = 0; r <= maxRing; r++)
            for (int dy = -r; dy <= r; dy++)
                for (int dx = -r; dx <= r; dx++)
                {
                    if (Math.Max(Math.Abs(dx), Math.Abs(dy)) != r) continue;
                    var c = new CellCoord(want.X + dx, want.Y + dy);
                    if (!sim.Setup.Map.Contains(c) || (accept != null && !accept(c))) continue;
                    if (!sim.Map.IsPassableTerrain(c, loco, sim.Setup.Rules.Terrain) || !sim.Map.BuildingAt(c).IsNone || !sim.Map.VehicleAt(c).IsNone) continue;
                    bool soldier = false;
                    for (int s = 0; s < MapState.InfantrySlots && !soldier; s++) soldier = !sim.Map.InfantryAt(c, s).IsNone;
                    if (!soldier) return c;
                }

        return null;
    }

    /// <summary>
    /// S19 MIS-07: the legal building site nearest <paramref name="want"/> (rings of growing size, row-major inside a ring, up to <see cref="FreeCellRing"/>):
    /// the top-left cell where <see cref="Simulation.CanPlace"/> says Ok now. Null when none.
    /// </summary>
    private static CellCoord? FreeSiteNear(Simulation sim, byte player, int buildingType, CellCoord want)
    {
        for (int r = 1; r <= FreeCellRing; r++)
            for (int dy = -r; dy <= r; dy++)
                for (int dx = -r; dx <= r; dx++)
                {
                    if (Math.Max(Math.Abs(dx), Math.Abs(dy)) != r) continue;
                    var c = new CellCoord(want.X + dx, want.Y + dy);
                    if (c.X < 0 || c.Y < 0) continue;
                    if (sim.CanPlace(player, buildingType, c) == PlacementResult.Ok) return c;
                }

        return null;
    }

    private Locomotion LocomotionOf(ActorKind kind, int index) =>
        kind == ActorKind.Vehicle ? _sim!.Setup.Rules.Vehicles[index].Locomotion : Locomotion.Foot;

    /// <summary>Spawns one unit of a type on the free cell nearest <paramref name="want"/>; a script error when none is free.</summary>
    private string SpawnNear(int player, string typeId, CellCoord want, int maxRing, Func<CellCoord, bool>? accept = null)
    {
        (ActorKind kind, int ix) = ResolveType(typeId);
        if (kind == ActorKind.Building) throw Err("'" + typeId + "' is a building; reinforcements are units");
        CellCoord cell = FreeCellNear(want, LocomotionOf(kind, ix), maxRing, accept) ?? throw Err("no free cell near (" + want.X + "," + want.Y + ") for " + typeId);
        return SpawnActor(_sim!, typeId, player, cell, null);
    }

    private ActorRef RefOf(string tag) { TryTag(tag, out ActorRef r); return r; }

    private void AttackMoveTo(string tag, CellCoord dest) => _sim!.AssignAttackMove(RefOf(tag), dest);

    // ---- Reinforce.Zone ----

    private List<string> ReinforceZone(int player, List<string> types, MissionZone zone, CellCoord dest)
    {
        foreach (string t in types) if (ResolveType(t).Kind == ActorKind.Building) throw Err("'" + t + "' is a building; reinforcements are units");
        var centre = new CellCoord((zone.X0 + zone.X1) / 2, (zone.Y0 + zone.Y1) / 2);
        int ring = Math.Max(zone.X1 - zone.X0, zone.Y1 - zone.Y0) + 1;
        var tags = new List<string>();
        foreach (string t in types)
        {
            // S18 MIS-07: when the zone is full (the player's army stands in it), overflow to the nearest free cell around it instead of failing
            // the callback halfway (flags set, timers never scheduled: lb_04 Landing, lb_03 waves, exp_04 Finale soft-locked).
            (ActorKind kind, int ix) = ResolveType(t);
            string tag = FreeCellNear(centre, LocomotionOf(kind, ix), ring, zone.Contains) is null
                ? SpawnNear(player, t, centre, ring + FreeCellRing)
                : SpawnNear(player, t, centre, ring, zone.Contains);
            AttackMoveTo(tag, dest);
            tags.Add(tag);
        }

        return tags;
    }

    // ---- Reinforce.Delayed (and the block layout of Reinforce.Edge) ----

    /// <summary>The same spawn as Reinforce.Edge (small block just inside the edge, then attack-move) for a queued arrival.</summary>
    private List<string> SpawnEdgeBlock(int player, List<string> types, CellCoord edge, CellCoord dest)
    {
        Simulation sim = _sim!;
        int w = sim.Setup.Map.Width, h = sim.Setup.Map.Height;
        (int ix, int iy) = (edge.X == 0 ? 1 : edge.X == w - 1 ? -1 : 0, edge.Y == 0 ? 1 : edge.Y == h - 1 ? -1 : 0);
        (int ax, int ay) = ix != 0 ? (0, 1) : (1, 0);
        var tags = new List<string>();
        for (int k = 0; k < types.Count; k++)
        {
            int depth = k / 3, along = k % 3 - 1;
            var cell = new CellCoord(Math.Clamp(edge.X + ix * depth + ax * along, 0, w - 1), Math.Clamp(edge.Y + iy * depth + ay * along, 0, h - 1));
            string tag = SpawnActor(sim, types[k], player, cell, null);
            if (RefOf(tag).Kind != ActorKind.Building) sim.AssignAttackMove(RefOf(tag), dest);
            tags.Add(tag);
        }

        return tags;
    }

    // ---- Reinforce.Transport ----

    private Faction FactionOfTypes(int player, List<string> types)
    {
        SimRules rules = _sim!.Setup.Rules;
        foreach (string t in types)
        {
            (ActorKind kind, int ix) = ResolveType(t);
            Faction f = kind == ActorKind.Vehicle ? rules.Vehicles[ix].Faction : kind == ActorKind.Infantry ? rules.Infantry![ix].Faction : Faction.Neutral;
            if (f != Faction.Neutral) return f;
        }

        return _sim.Players.Faction[player];
    }

    /// <summary>The ground transport type of a faction: the smallest that seats <paramref name="soldiers"/>, else the largest (lowest rules index wins ties).</summary>
    private int PickGroundTransport(Faction faction, int soldiers)
    {
        IReadOnlyList<VehicleType> vs = _sim!.Setup.Rules.Vehicles;
        int best = -1;
        for (int i = 0; i < vs.Count; i++)
        {
            VehicleType v = vs[i];
            if (v.Faction != faction || v.Passengers <= 0 || v.UnloadTicks > 0 || v.Locomotion is not (Locomotion.Wheeled or Locomotion.Tracked or Locomotion.Hover)) continue;
            if (best < 0) { best = i; continue; }
            int cap = vs[best].Passengers;
            bool fits = v.Passengers >= soldiers, bestFits = cap >= soldiers;
            if ((fits && !bestFits) || (fits && bestFits && v.Passengers < cap) || (!fits && !bestFits && v.Passengers > cap)) best = i;
        }

        return best >= 0 ? best : throw Err("no ground transport type with passenger seats for faction " + faction);
    }

    private List<string> ReinforceTransport(int player, List<string> types, CellCoord edge, CellCoord dest)
    {
        Simulation sim = _sim!;
        var kinds = new ActorKind[types.Count];
        for (int k = 0; k < types.Count; k++)
        {
            kinds[k] = ResolveType(types[k]).Kind;
            if (kinds[k] == ActorKind.Building) throw Err("'" + types[k] + "' is a building; reinforcements are units");
        }

        var riders = new List<int>();
        for (int k = 0; k < types.Count; k++) if (kinds[k] == ActorKind.Infantry) riders.Add(k);
        var tags = new string[types.Count];
        int tIdx = riders.Count > 0 ? PickGroundTransport(FactionOfTypes(player, types), riders.Count) : -1;

        // Soldiers ride; every other type drives in by itself, as with Reinforce.Edge.
        for (int k = 0; k < types.Count; k++)
        {
            if (kinds[k] == ActorKind.Infantry) continue;
            tags[k] = SpawnNear(player, types[k], edge, FreeCellRing);
            AttackMoveTo(tags[k], dest);
        }

        int seats = tIdx >= 0 ? sim.Setup.Rules.Vehicles[tIdx].Passengers : 0;
        for (int from = 0; from < riders.Count; from += seats)
        {
            string transport = SpawnNear(player, sim.Setup.Rules.Vehicles[tIdx].Id, edge, FreeCellRing);
            ActorRef tr = RefOf(transport);
            for (int n = from; n < Math.Min(riders.Count, from + seats); n++)
            {
                int k = riders[n];
                tags[k] = SpawnActor(sim, types[k], player, CellOfActor(sim, tr), null);
                if (!sim.ScriptBoard(RefOf(tags[k]), tr.Id)) throw Err("a soldier could not board the transport");
            }

            sim.AssignMission(tr, MissionKind.Move, dest);
            _arrivals.Add(new PendingArrival { Kind = ArrivalKind.TransportDrop, Tick = sim.Tick + TransportDeadlineTicks, Player = player, Tag = transport, Dest = dest });
        }

        return new List<string>(tags);
    }

    // ---- Reinforce.Naval ----

    private List<string> ReinforceNaval(int player, List<string> types, CellCoord edge, CellCoord dest)
    {
        Simulation sim = _sim!;
        SimRules rules = sim.Setup.Rules;
        var kinds = new ActorKind[types.Count];
        var ixs = new int[types.Count];
        for (int k = 0; k < types.Count; k++)
        {
            (kinds[k], ixs[k]) = ResolveType(types[k]);
            if (kinds[k] == ActorKind.Building) throw Err("'" + types[k] + "' is a building; reinforcements are units");
            if (kinds[k] == ActorKind.Vehicle && rules.Vehicles[ixs[k]].Locomotion is Locomotion.Air or Locomotion.Burrow)
                throw Err("'" + types[k] + "' cannot arrive by sea");
        }

        // Assign every passenger to a ship of the list with room (soldiers: passengers, ground vehicles: vehicle_slots) before spawning anything.
        bool IsShip(int k) => kinds[k] == ActorKind.Vehicle && rules.Vehicles[ixs[k]].Locomotion == Locomotion.Naval;
        var carrier = new int[types.Count];
        Array.Fill(carrier, -1);
        var soldierLoad = new int[types.Count];
        var vehicleLoad = new int[types.Count];
        for (int k = 0; k < types.Count; k++)
        {
            if (IsShip(k)) continue;
            bool soldier = kinds[k] == ActorKind.Infantry;
            for (int s = 0; s < types.Count && carrier[k] < 0; s++)
            {
                if (!IsShip(s)) continue;
                VehicleType sv = rules.Vehicles[ixs[s]];
                if (soldier ? soldierLoad[s] < sv.Passengers : vehicleLoad[s] < sv.VehicleSlots) carrier[k] = s;
            }

            if (carrier[k] < 0) throw Err("no ship in the list has room for '" + types[k] + "' (entry " + (k + 1) + ")");
            if (soldier) soldierLoad[carrier[k]]++; else vehicleLoad[carrier[k]]++;
        }

        var tags = new string[types.Count];
        for (int k = 0; k < types.Count; k++)
            if (IsShip(k)) tags[k] = SpawnNear(player, types[k], edge, FreeCellRing);

        for (int k = 0; k < types.Count; k++)
        {
            if (IsShip(k)) continue;
            ActorRef ship = RefOf(tags[carrier[k]]);
            tags[k] = SpawnActor(sim, types[k], player, CellOfActor(sim, ship), null);
            if (!sim.ScriptBoard(RefOf(tags[k]), ship.Id)) throw Err("'" + types[k] + "' could not board its ship");
        }

        // Nearest sea cell to the destination, for ships that do not unload (cell-index order breaks ties).
        CellCoord? goal = null;
        long bestD = long.MaxValue;
        for (int y = 0; y < sim.Setup.Map.Height; y++)
            for (int x = 0; x < sim.Setup.Map.Width; x++)
            {
                var c = new CellCoord(x, y);
                if (!sim.Map.IsPassableTerrain(c, Locomotion.Naval, rules.Terrain)) continue;
                long d = (long)(x - dest.X) * (x - dest.X) + (long)(y - dest.Y) * (y - dest.Y);
                if (d < bestD) { bestD = d; goal = c; }
            }

        for (int k = 0; k < types.Count; k++)
        {
            if (!IsShip(k)) continue;
            ActorRef ship = RefOf(tags[k]);
            if (soldierLoad[k] + vehicleLoad[k] > 0 && sim.ScriptLandingUnload(ship.Id, dest)) continue;
            if (goal is { } g) sim.AssignAttackMove(ship, g);
        }

        return new List<string>(tags);
    }

    // ---- Reinforce.Rig ----

    private string ReinforceRig(int player, CellCoord edge, CellCoord dest)
    {
        Simulation sim = _sim!;
        SimRules rules = sim.Setup.Rules;
        int rig = -1;
        if (rules.StartKits.Count > (int)Faction.LeftBehind)
            foreach (string id in rules.StartKits[(int)Faction.LeftBehind].Units)
            {
                int i = SimRules.IndexOf(rules.Vehicles, id, v => v.Id);
                if (i >= 0 && rules.Vehicles[i].UnpacksInto >= 0) { rig = i; break; }
            }

        for (int i = 0; i < rules.Vehicles.Count && rig < 0; i++)
            if (rules.Vehicles[i].Faction == Faction.LeftBehind && rules.Vehicles[i].UnpacksInto >= 0) rig = i;
        if (rig < 0) throw Err("the rules have no packed rig vehicle for Left Behind");

        string tag = SpawnNear(player, rules.Vehicles[rig].Id, edge, FreeCellRing);
        ActorRef r = RefOf(tag);
        // Drives to dest and unpacks there when the footprint is a legal site now; otherwise it just parks packed beside dest.
        if (!sim.ScriptUnpackRig(r.Id, dest)) sim.AssignMission(r, MissionKind.Move, dest);
        return tag;
    }

    // ---- Actor.CreateGroup ----

    private List<string> CreateGroup(List<string> types, int player, CellCoord centre, string formation, int rank, string prefix)
    {
        Simulation sim = _sim!;
        int n = types.Count;
        var names = new string?[n];
        if (prefix.Length > 0)
            for (int k = 0; k < n; k++)
            {
                names[k] = prefix + (k + 1);
                if (TryTag(names[k]!, out _)) throw Err("tag '" + names[k] + "' is already in use");
            }

        int side = 1;
        while (side * side < n) side++;
        var tags = new List<string>();
        for (int k = 0; k < n; k++)
        {
            int off = k == 0 ? 0 : (k % 2 == 1 ? (k + 1) / 2 : -(k / 2));
            (int dx, int dy) = formation switch
            {
                "line" => (off, 0),
                "column" => (0, off),
                "box" => (k % side - (side - 1) / 2, k / side - (side - 1) / 2),
                _ => ((int)((NextRandom() >> 33) % 7) - 3, (int)((NextRandom() >> 33) % 7) - 3),
            };
            var want = new CellCoord(Math.Clamp(centre.X + dx, 0, sim.Setup.Map.Width - 1), Math.Clamp(centre.Y + dy, 0, sim.Setup.Map.Height - 1));
            (ActorKind kind, int ix) = ResolveType(types[k]);
            CellCoord cell = want;
            if (kind != ActorKind.Building)
                cell = FreeCellNear(want, LocomotionOf(kind, ix), FreeCellRing) ?? throw Err("no free cell near (" + want.X + "," + want.Y + ") for " + types[k]);
            string tag = SpawnActor(sim, types[k], player, cell, names[k]);
            if (rank > 0 && kind != ActorKind.Building) sim.ScriptSetRank(kind, RefOf(tag).Id, rank);
            tags.Add(tag);
        }

        return tags;
    }

    // ---- queued arrivals (run at the start of every tick) ----

    private void RunArrivals(Simulation sim)
    {
        int tick = sim.Tick;
        for (int i = 0; i < _arrivals.Count; i++)
        {
            PendingArrival a = _arrivals[i];
            if (a.Kind == ArrivalKind.Delayed)
            {
                if (tick < a.Tick) continue;
                _arrivals.RemoveAt(i--);
                _fn = "Reinforce.Delayed";
                try { SpawnEdgeBlock(a.Player, a.Types, a.Edge, a.Dest); }
                catch (Exception e) when (e is not OutOfMemoryException) { AddError("Reinforce.Delayed: " + e.Message); }
                continue;
            }

            if (!TryTag(a.Tag, out ActorRef tr) || !sim.TryResolve(tr)) { _arrivals.RemoveAt(i--); continue; }   // the transport (and its load) died
            if (Cheb(CellOfActor(sim, tr), a.Dest) > 1 && tick < a.Tick) continue;
            _arrivals.RemoveAt(i--);
            sim.ScriptUnload(tr.Id);
        }
    }

    private void WriteArrivals(BinaryWriter w)
    {
        w.Write(_arrivals.Count);
        foreach (PendingArrival a in _arrivals)
        {
            w.Write((byte)a.Kind); w.Write(a.Tick); w.Write(a.Player); w.Write(a.Tag);
            w.Write(a.Edge.X); w.Write(a.Edge.Y); w.Write(a.Dest.X); w.Write(a.Dest.Y);
            w.Write(a.Types.Count);
            foreach (string t in a.Types) w.Write(t);
        }
    }

    private void ReadArrivals(BinaryReader r)
    {
        for (int n = r.ReadInt32(), i = 0; i < n; i++)
        {
            var a = new PendingArrival { Kind = (ArrivalKind)r.ReadByte(), Tick = r.ReadInt32(), Player = r.ReadInt32(), Tag = r.ReadString() };
            a.Edge = new CellCoord(r.ReadInt32(), r.ReadInt32());
            a.Dest = new CellCoord(r.ReadInt32(), r.ReadInt32());
            for (int m = r.ReadInt32(), j = 0; j < m; j++) a.Types.Add(r.ReadString());
            _arrivals.Add(a);
        }
    }
}
