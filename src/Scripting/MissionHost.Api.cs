// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using MoonSharp.Interpreter;
using Rts.Sim;
using Rts.Sim.Core;
using Rts.Sim.Rules;

namespace Rts.Scripting;

// The Lua API (architecture section 10), pilot subset. Rules for every function: only integers cross the boundary (a non-integral
// number is a script error), cells are tables {x=, y=} (or {x, y}), players are slot numbers, types are rules ids, and nothing may be
// called while the script file is loading (use OnInit or a callback).
public sealed partial class MissionHost
{
    private Table NewApiTable(Script s, string name, params (string Fn, Func<CallbackArguments, DynValue> Body)[] fns)
    {
        // Several partial files add functions to the same table (Trigger, Objective, Mission, ...): reuse it when it exists.
        Table t = s.Globals.Get(name) is { Type: DataType.Table } existing ? existing.Table : new Table(s);
        foreach ((string fn, Func<CallbackArguments, DynValue> body) in fns)
        {
            string full = name + "." + fn;
            t.Set(fn, DynValue.NewCallback((_, args) =>
            {
                if (_callbackDepth == 0 || _sim == null) throw new ScriptRuntimeException(full + ": the API cannot be used while the script loads; call it from OnInit or a callback");
                _fn = full;
                return body(args);
            }, full));
        }

        s.Globals.Set(name, DynValue.NewTable(t));
        return t;
    }

    private string _fn = "";

    internal void RegisterApi(Script s)
    {
        NewApiTable(s, "Trigger",
            ("AfterDelay", a => AddTrigger(TriggerKind.After, 0, "", a, 1, Int(a, 0, 0, 1_000_000_000))),
            ("Every", a => AddTrigger(TriggerKind.Every, Int(a, 0, 1, 1_000_000_000), "", a, 1, Int(a, 0, 1, 1_000_000_000))),
            ("OnEnter", a => AddTrigger(TriggerKind.Enter, PlayerArg(a, 1), ZoneName(a, 0), a, 2, 0)),
            ("OnDestroyed", a => AddTrigger(TriggerKind.Destroyed, 0, KnownTag(a, 0), a, 1, 0)),
            ("OnAllDestroyed", a => AddTrigger(TriggerKind.AllDestroyed, PlayerArg(a, 0), "", a, 1, 0)));

        NewApiTable(s, "Actor",
            ("Create", ActorCreate),
            ("Order", ActorOrder),
            ("Alive", a => DynValue.NewBoolean(TryTag(Str(a, 0), out ActorRef r) && _sim!.TryResolve(r))),
            ("Count", a => { var (k, ix) = TypeFilter(a, 1); return Num(CountAlive(_sim!, (byte)PlayerArg(a, 0), ix, k)); }),
            ("CountInZone", ActorCountInZone),
            ("Cell", ActorCellOf));

        NewApiTable(s, "Reinforce", ("Edge", ReinforceEdge));

        NewApiTable(s, "Objective",
            ("Add", ObjectiveAdd),
            ("Complete", a => SetObjective(Str(a, 0), ObjectiveStatus.Completed)),
            ("Fail", a => SetObjective(Str(a, 0), ObjectiveStatus.Failed)));

        NewApiTable(s, "Tech",
            ("Grant", a => TechSet(a, grant: true)),
            ("Revoke", a => TechSet(a, grant: false)),
            ("Has", a => DynValue.NewBoolean(_granted[PlayerArg(a, 0)].Contains(Str(a, 1)))));

        NewApiTable(s, "Media",
            ("Message", a =>
            {
                string text = Str(a, 0);
                _messages.Add(new MissionMessage(++_messageSeq, _sim!.Tick, text));
                if (_messages.Count > MaxMessages) _messages.RemoveAt(0);
                return DynValue.Nil;
            }));

        NewApiTable(s, "Player",
            ("Cash", a => Num(_sim!.Players.Cash[PlayerArg(a, 0)])),
            ("Funds", a => Num(_sim!.Funds((byte)PlayerArg(a, 0)))),   // M8-17: Cash + Stored, read-only
            ("AddCash", a =>
            {
                int p = PlayerArg(a, 0);
                _sim!.Players.Cash[p] = (int)Math.Clamp((long)_sim.Players.Cash[p] + Int(a, 1, -100_000_000, 100_000_000), 0, int.MaxValue);
                return DynValue.Nil;
            }));

        NewApiTable(s, "Map",
            ("Zone", a => ZoneTable(Str(a, 0))),
            ("Center", a =>
            {
                MissionZone z = ZoneOf(Str(a, 0));
                return CellTable(new CellCoord((z.X0 + z.X1) / 2, (z.Y0 + z.Y1) / 2));
            }),
            ("Width", _ => Num(_sim!.Setup.Map.Width)),
            ("Height", _ => Num(_sim!.Setup.Map.Height)));

        NewApiTable(s, "Mission",
            ("Win", a => EndMission(PlayerArg(a, 0), won: true)),
            ("Lose", a => EndMission(PlayerArg(a, 0), won: false)),
            ("Tick", _ => Num(_sim!.Tick)));

        NewApiTable(s, "Random",
            ("Next", a =>
            {
                int n = Int(a, 0, 1, 1_000_000_000);
                return Num((int)(NextRandom() >> 33) % n);
            }));

        // M8-02 contracts: one partial file per owning package.
        RegisterObjectivesApi(s);
        RegisterReinforceApi(s);
        RegisterMediaApi(s);
        RegisterAiApi(s);
    }

    // ---- argument helpers ----

    private static DynValue Num(long v) => DynValue.NewNumber(v);

    private ScriptRuntimeException Err(string message) => new(_fn + ": " + message);

    private int Int(CallbackArguments a, int i, long min, long max) => (int)IntOf(a[i], "argument " + (i + 1), min, max);

    private long IntOf(DynValue v, string what, long min, long max)
    {
        if (v.Type != DataType.Number) throw Err(what + " must be an integer, got " + v.Type.ToLuaTypeString());
        if (!LuaStateCodec.IsInteger(v.Number)) throw Err(what + " must be an integer, got " + v.Number.ToString(System.Globalization.CultureInfo.InvariantCulture));
        long n = (long)v.Number;
        if (n < min || n > max) throw Err(what + " must be between " + min + " and " + max + ", got " + n);
        return n;
    }

    private string Str(CallbackArguments a, int i)
    {
        DynValue v = a[i];
        if (v.Type != DataType.String) throw Err("argument " + (i + 1) + " must be a string, got " + v.Type.ToLuaTypeString());
        return v.String;
    }

    private int PlayerArg(CallbackArguments a, int i) => Int(a, i, 0, _sim!.Setup.PlayerCount - 1);

    private CellCoord CellArg(CallbackArguments a, int i, bool inMap = true)
    {
        DynValue v = a[i];
        if (v.Type != DataType.Table) throw Err("argument " + (i + 1) + " must be a cell {x=, y=}, got " + v.Type.ToLuaTypeString());
        DynValue x = v.Table.Get("x"), y = v.Table.Get("y");
        if (x.Type == DataType.Nil) x = v.Table.Get(1);
        if (y.Type == DataType.Nil) y = v.Table.Get(2);
        var cell = new CellCoord((int)IntOf(x, "cell x", -100000, 100000), (int)IntOf(y, "cell y", -100000, 100000));
        if (inMap && !_sim!.Setup.Map.Contains(cell)) throw Err("cell (" + cell.X + "," + cell.Y + ") is outside the map");
        return cell;
    }

    private DynValue CellTable(CellCoord c)
    {
        var t = new Table(_script);
        t.Set("x", Num(c.X));
        t.Set("y", Num(c.Y));
        return DynValue.NewTable(t);
    }

    private MissionZone ZoneOf(string name) => Def.ZoneNamed(name) ?? throw Err("unknown zone '" + name + "' (define it in mission.toml)");

    private string ZoneName(CallbackArguments a, int i)
    {
        string name = Str(a, i);
        ZoneOf(name);
        return name;
    }

    private DynValue ZoneTable(string name)
    {
        MissionZone z = ZoneOf(name);
        var t = new Table(_script);
        t.Set("x0", Num(z.X0)); t.Set("y0", Num(z.Y0)); t.Set("x1", Num(z.X1)); t.Set("y1", Num(z.Y1));
        return DynValue.NewTable(t);
    }

    private string KnownTag(CallbackArguments a, int i)
    {
        string tag = Str(a, i);
        if (!TryTag(tag, out _)) throw Err("unknown actor tag '" + tag + "'");
        return tag;
    }

    private bool TryTag(string tag, out ActorRef actor)
    {
        for (int i = 0; i < _tags.Count; i++)
            if (_tags[i].Key == tag) { actor = _tags[i].Value; return true; }
        actor = default;
        return false;
    }

    // ---- triggers ----

    /// <summary>The global name of a callback: a string naming a global function, or a function value found among the globals.</summary>
    private string CallbackName(CallbackArguments a, int i)
    {
        DynValue v = a[i];
        if (v.Type == DataType.String)
        {
            if (_script.Globals.Get(v.String).Type != DataType.Function) throw Err("'" + v.String + "' is not a global function");
            return v.String;
        }

        if (v.Type != DataType.Function) throw Err("argument " + (i + 1) + " must be a function, got " + v.Type.ToLuaTypeString());
        if (LuaStateCodec.TryOrderedKeys(_script.Globals, out List<DynValue> keys))
        {
            foreach (DynValue k in keys)
                if (k.Type == DataType.String && _script.Globals.Get(k).Type == DataType.Function && ReferenceEquals(_script.Globals.Get(k).Function, v.Function))
                    return k.String;
        }

        throw Err("callbacks must be top-level named functions (function Name() ... end) so they survive save and load");
    }

    private DynValue AddTrigger(TriggerKind kind, int a, string name, CallbackArguments args, int fnIndex, int delayOrPeriod)
    {
        var t = new TriggerRecord { Kind = kind, A = a, Name = name, Fn = CallbackName(args, fnIndex) };
        if (kind is TriggerKind.After or TriggerKind.Every) t.Next = _sim!.Tick + delayOrPeriod;
        _triggers.Add(t);
        return DynValue.Nil;
    }

    // ---- actors ----

    private (ActorKind Kind, int Index) ResolveType(string id)
    {
        SimRules rules = _sim!.Setup.Rules;
        int i = SimRules.IndexOf(rules.Vehicles, id, v => v.Id);
        if (i >= 0) return (ActorKind.Vehicle, i);
        if (rules.Infantry != null && (i = SimRules.IndexOf(rules.Infantry, id, v => v.Id)) >= 0) return (ActorKind.Infantry, i);
        if (rules.Buildings != null && (i = SimRules.IndexOf(rules.Buildings, id, v => v.Id)) >= 0) return (ActorKind.Building, i);
        throw new ScriptRuntimeException(_fn + ": unknown type id '" + id + "'");
    }

    private (ActorKind? Kind, int Index) TypeFilter(CallbackArguments a, int i)
    {
        if (a[i].Type is DataType.Nil or DataType.Void) return (null, -1);
        (ActorKind k, int ix) = ResolveType(Str(a, i));
        return (k, ix);
    }

    private string NewTag()
    {
        string tag;
        do { tag = "a" + ++_counter; } while (TryTag(tag, out _));
        return tag;
    }

    private string SpawnActor(Simulation sim, string typeId, int player, CellCoord cell, string? tag)
    {
        if (player < 0 || player >= sim.Setup.PlayerCount) throw new InvalidOperationException("no such player " + player);
        if (!sim.Setup.Map.Contains(cell)) throw new InvalidOperationException("cell (" + cell.X + "," + cell.Y + ") is outside the map");
        if (tag != null && TryTag(tag, out _)) throw new InvalidOperationException("tag '" + tag + "' is already in use");
        Sim_ResolveForSpawn(sim, typeId, out ActorKind kind, out int index);
        EntityId id = kind switch
        {
            ActorKind.Vehicle => sim.SpawnVehicle((byte)player, index, cell),
            ActorKind.Infantry => sim.SpawnInfantry((byte)player, index, cell),
            _ => sim.PlaceBuilding((byte)player, index, cell),
        };
        if (id.IsNone) throw new InvalidOperationException(kind == ActorKind.Building ? "cannot place " + typeId + " at (" + cell.X + "," + cell.Y + ")" : "no room for another " + typeId);
        tag ??= NewTag();
        _tags.Add(new(tag, new ActorRef(kind, id)));
        return tag;
    }

    private static void Sim_ResolveForSpawn(Simulation sim, string id, out ActorKind kind, out int index)
    {
        SimRules rules = sim.Setup.Rules;
        index = SimRules.IndexOf(rules.Vehicles, id, v => v.Id);
        if (index >= 0) { kind = ActorKind.Vehicle; return; }
        if (rules.Infantry != null && (index = SimRules.IndexOf(rules.Infantry, id, v => v.Id)) >= 0) { kind = ActorKind.Infantry; return; }
        if (rules.Buildings != null && (index = SimRules.IndexOf(rules.Buildings, id, v => v.Id)) >= 0) { kind = ActorKind.Building; return; }
        throw new InvalidOperationException("unknown type id '" + id + "'");
    }

    private DynValue ActorCreate(CallbackArguments a)
    {
        string type = Str(a, 0);
        string? tag = a[3].Type is DataType.Nil or DataType.Void ? null : Str(a, 3);
        string made = SpawnActor(_sim!, type, PlayerArg(a, 1), CellArg(a, 2), tag);
        return DynValue.NewString(made);
    }

    private DynValue ActorOrder(CallbackArguments a)
    {
        string tag = KnownTag(a, 0);
        string order = Str(a, 1);
        CellCoord cell = CellArg(a, 2);
        TryTag(tag, out ActorRef actor);
        bool ok = order switch
        {
            "move" => _sim!.AssignMission(actor, Rts.Sim.Missions.MissionKind.Move, cell),
            "attackmove" => _sim!.AssignAttackMove(actor, cell),
            _ => throw Err("order must be \"move\" or \"attackmove\", got \"" + order + "\""),
        };
        return DynValue.NewBoolean(ok);
    }

    private DynValue ActorCellOf(CallbackArguments a)
    {
        if (!TryTag(Str(a, 0), out ActorRef r) || !_sim!.TryResolve(r)) return DynValue.Nil;
        return CellTable(CellOfActor(_sim, r));
    }

    private static CellCoord CellOfActor(Simulation sim, ActorRef r) => r.Kind switch
    {
        ActorKind.Vehicle => sim.Units.Position[r.Id.Index].ToCell(),
        ActorKind.Infantry => sim.Infantry.Position[r.Id.Index].ToCell(),
        _ => sim.Buildings.Cell[r.Id.Index],
    };

    private static int CountAlive(Simulation sim, byte player, int typeIndex, ActorKind? kind)
    {
        int n = 0;
        if (kind is null or ActorKind.Vehicle)
            for (int i = 1; i < sim.Units.Capacity; i++)
                if (sim.Units.Slots.IsIndexAlive(i) && sim.Units.Health[i] > 0 && sim.Units.Player[i] == player && (typeIndex < 0 || sim.Units.TypeIndex[i] == typeIndex)) n++;
        if (kind is null or ActorKind.Infantry)
            for (int i = 1; i < sim.Infantry.Capacity; i++)
                if (sim.Infantry.Slots.IsIndexAlive(i) && sim.Infantry.Health[i] > 0 && sim.Infantry.Player[i] == player && (typeIndex < 0 || sim.Infantry.TypeIndex[i] == typeIndex)) n++;
        if (kind is null or ActorKind.Building)
            for (int i = 1; i < sim.Buildings.Capacity; i++)
                if (sim.Buildings.Slots.IsIndexAlive(i) && sim.Buildings.Health[i] > 0 && sim.Buildings.Player[i] == player && (typeIndex < 0 || sim.Buildings.TypeIndex[i] == typeIndex)) n++;
        return n;
    }

    private DynValue ActorCountInZone(CallbackArguments a)
    {
        MissionZone z = ZoneOf(Str(a, 0));
        byte player = (byte)PlayerArg(a, 1);
        (ActorKind? kind, int index) = TypeFilter(a, 2);
        Simulation sim = _sim!;
        int n = 0;
        if (kind is null or ActorKind.Vehicle)
            for (int i = 1; i < sim.Units.Capacity; i++)
                if (sim.Units.Slots.IsIndexAlive(i) && sim.Units.Health[i] > 0 && sim.Units.Player[i] == player && (index < 0 || sim.Units.TypeIndex[i] == index) && z.Contains(sim.Units.Position[i].ToCell())) n++;
        if (kind is null or ActorKind.Infantry)
            for (int i = 1; i < sim.Infantry.Capacity; i++)
                if (sim.Infantry.Slots.IsIndexAlive(i) && sim.Infantry.Health[i] > 0 && sim.Infantry.Player[i] == player && (index < 0 || sim.Infantry.TypeIndex[i] == index) && z.Contains(sim.Infantry.Position[i].ToCell())) n++;
        if (kind is null or ActorKind.Building)
            for (int i = 1; i < sim.Buildings.Capacity; i++)
                if (sim.Buildings.Slots.IsIndexAlive(i) && sim.Buildings.Health[i] > 0 && sim.Buildings.Player[i] == player && (index < 0 || sim.Buildings.TypeIndex[i] == index) && z.Contains(sim.Buildings.Cell[i])) n++;
        return Num(n);
    }

    private static bool AnyMobileInZone(Simulation sim, byte player, MissionZone z)
    {
        for (int i = 1; i < sim.Units.Capacity; i++)
            if (sim.Units.Slots.IsIndexAlive(i) && sim.Units.Health[i] > 0 && sim.Units.Player[i] == player && z.Contains(sim.Units.Position[i].ToCell())) return true;
        for (int i = 1; i < sim.Infantry.Capacity; i++)
            if (sim.Infantry.Slots.IsIndexAlive(i) && sim.Infantry.Health[i] > 0 && sim.Infantry.Player[i] == player && z.Contains(sim.Infantry.Position[i].ToCell())) return true;
        return false;
    }

    // ---- reinforcements ----

    /// <summary>Reinforce.Edge(player, {types}, edgeCell, destCell): spawns the units in a small block just inside the map edge
    /// and sends them attack-moving to destCell. Returns the list of their tags.</summary>
    private DynValue ReinforceEdge(CallbackArguments a)
    {
        int player = PlayerArg(a, 0);
        DynValue list = a[1];
        if (list.Type != DataType.Table || list.Table.Length < 1) throw Err("argument 2 must be a non-empty list of type ids");
        CellCoord edge = CellArg(a, 2), dest = CellArg(a, 3);
        Simulation sim = _sim!;
        int w = sim.Setup.Map.Width, h = sim.Setup.Map.Height;
        if (edge.X != 0 && edge.Y != 0 && edge.X != w - 1 && edge.Y != h - 1) throw Err("edge cell (" + edge.X + "," + edge.Y + ") is not on the map edge");

        (int ix, int iy) = (edge.X == 0 ? 1 : edge.X == w - 1 ? -1 : 0, edge.Y == 0 ? 1 : edge.Y == h - 1 ? -1 : 0);
        (int ax, int ay) = ix != 0 ? (0, 1) : (1, 0);
        var tags = new Table(_script);
        int count = list.Table.Length;
        for (int k = 0; k < count; k++)
        {
            DynValue t = list.Table.Get(k + 1);
            if (t.Type != DataType.String) throw Err("type list entries must be strings");
            int depth = k / 3, along = k % 3 - 1;
            var cell = new CellCoord(Math.Clamp(edge.X + ix * depth + ax * along, 0, w - 1), Math.Clamp(edge.Y + iy * depth + ay * along, 0, h - 1));
            string tag = SpawnActor(sim, t.String, player, cell, null);
            TryTag(tag, out ActorRef r);
            if (r.Kind != ActorKind.Building) sim.AssignAttackMove(r, dest);
            tags.Set(k + 1, DynValue.NewString(tag));
        }

        return DynValue.NewTable(tags);
    }

    // ---- objectives, tech, mission end ----

    private DynValue ObjectiveAdd(CallbackArguments a)
    {
        string id = Str(a, 0), text = Str(a, 1);
        bool primary = a[2].Type == DataType.Boolean ? a[2].Boolean : throw Err("argument 3 (primary) must be true or false");
        if (_objectives.Exists(o => o.Id == id)) throw Err("objective '" + id + "' already exists");
        _objectives.Add(new MissionObjective(id, text, primary, ObjectiveStatus.Active));
        return DynValue.Nil;
    }

    private DynValue SetObjective(string id, ObjectiveStatus status)
    {
        int i = _objectives.FindIndex(o => o.Id == id);
        if (i < 0) throw Err("unknown objective '" + id + "'");
        _objectives[i] = _objectives[i] with { Status = status };
        return DynValue.Nil;
    }

    private DynValue TechSet(CallbackArguments a, bool grant)
    {
        int player = PlayerArg(a, 0);
        string id = Str(a, 1);
        ResolveType(id);   // errors for an unknown id
        if (grant) _granted[player].Add(id); else _granted[player].Remove(id);
        return DynValue.Nil;
    }

    private DynValue EndMission(int player, bool won)
    {
        if (_outcome != MissionOutcome.None) return DynValue.Nil;
        Simulation sim = _sim!;
        _outcome = won ? MissionOutcome.Won : MissionOutcome.Lost;
        if (won)
        {
            byte team = sim.Players.Team[player];
            for (int p = 0; p < sim.Players.Count; p++)
                if (!sim.Players.IsNeutral((byte)p) && sim.Players.Team[p] != team) sim.Players.Defeated[p] = true;
        }
        else
        {
            sim.Players.Defeated[player] = true;
        }

        return DynValue.Nil;
    }
}
