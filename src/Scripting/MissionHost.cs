// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using System.IO.Hashing;
using System.Text;
using MoonSharp.Interpreter;
using Rts.Sim;
using Rts.Sim.Core;
using Rts.Sim.Rules;

namespace Rts.Scripting;

/// <summary>
/// Runs one mission's Lua script inside the simulation tick (D-015, D-061, architecture §10). Plugs into the Sim through
/// <see cref="SimSetup.Extensions"/>; the Sim never sees MoonSharp. One host serves one Simulation.
/// <para>
/// Lifecycle: the script file is loaded in <see cref="OnSetup"/> (it may only define functions; API calls there are errors). On the first
/// tick of a new game the global <c>OnInit()</c> runs once and registers triggers, objectives and actors. Triggers store the global
/// <em>name</em> of their callback, so a loaded game finds them again; all other script data lives in the global <c>state</c> table.
/// Everything mutable (triggers, tags, objectives, messages, tech sets, script RNG, the <c>state</c> table) is in the "mission" state part,
/// which is saved and hashed. Errors in callbacks are collected in <see cref="Errors"/>; they are deterministic and never stop the sim.
/// </para>
/// </summary>
public sealed partial class MissionHost : ISimExtension
{
    private const int MaxMessages = 32;
    private const int MaxErrors = 50;
    private const int FormatVersion = 1;

    private readonly string _source;
    private readonly Script _script;
    private readonly MissionStatePart _part;
    private Simulation? _sim;
    private int _callbackDepth;
    private int _callbacksThisTick;

    // ---- the "mission" state part (everything below is written by WriteState) ----
    private bool _initDone;
    private bool _halted;
    private MissionOutcome _outcome;
    private ulong _rng;
    private int _counter;
    private int _messageSeq;
    private readonly List<KeyValuePair<string, ActorRef>> _tags = new();
    private readonly List<TriggerRecord> _triggers = new();
    private readonly List<MissionObjective> _objectives = new();
    private readonly List<MissionMessage> _messages = new();
    private readonly bool[] _restricted;
    private readonly SortedSet<string>[] _granted;

    // Diagnostics, not state.
    private readonly List<string> _errors = new();
    private readonly List<string> _printLog = new();

    /// <summary>Creates the host. The script source is compiled lazily in <see cref="OnSetup"/>.</summary>
    /// <param name="def">The parsed mission.toml.</param>
    /// <param name="luaSource">The text of mission.lua.</param>
    public MissionHost(MissionDef def, string luaSource)
    {
        ArgumentNullException.ThrowIfNull(def);
        ArgumentNullException.ThrowIfNull(luaSource);
        Def = def;
        _source = luaSource;
        SourceHash = HashSource(luaSource);
        _part = new MissionStatePart(this);
        int n = def.Players.Count;
        _restricted = new bool[n];
        _granted = new SortedSet<string>[n];
        for (int p = 0; p < n; p++)
        {
            _granted[p] = new SortedSet<string>(StringComparer.Ordinal);
            if (def.Players[p].Tech is { } tech)
            {
                _restricted[p] = true;
                foreach (string id in tech) _granted[p].Add(id);
            }
        }

        _script = Sandbox.Create(this, _printLog);
    }

    /// <summary>The mission definition.</summary>
    public MissionDef Def { get; }

    /// <summary>xxHash64 of the script source, stored in the state part: a save made with another script refuses to load.</summary>
    public ulong SourceHash { get; }

    /// <inheritdoc />
    public string Name => "mission:" + Def.Id;

    /// <inheritdoc />
    public ISimStatePart State => _part;

    /// <summary>Objectives in the order the script added them.</summary>
    public IReadOnlyList<MissionObjective> Objectives => _objectives;

    /// <summary>The most recent Media.Message lines (oldest first, at most 32).</summary>
    public IReadOnlyList<MissionMessage> Messages => _messages;

    /// <summary>Whether the script has called Mission.Win or Mission.Lose.</summary>
    public MissionOutcome Outcome => _outcome;

    /// <summary>Script errors so far (tick and message). Empty means the script ran cleanly.</summary>
    public IReadOnlyList<string> Errors => _errors;

    /// <summary>Lines the script printed with print() (debug aid, not state).</summary>
    public IReadOnlyList<string> PrintLog => _printLog;

    /// <summary>True after a fatal script problem (the state table could not be saved); callbacks no longer run.</summary>
    public bool Halted => _halted;

    /// <summary>Number of live triggers (for tests and tools).</summary>
    public int TriggerCount => _triggers.Count;

    /// <summary>
    /// Places the mission's pre-placed actors (mission.toml <c>[[actors]]</c>) and sets starting cash. Call once on a new game, after
    /// constructing the Simulation and before its first Step; the placements go into the sim's setup log. Not for a loaded game.
    /// </summary>
    public void PlaceInitialActors(Simulation sim)
    {
        ArgumentNullException.ThrowIfNull(sim);
        foreach (MissionPlayer p in Def.Players)
            if (!sim.Players.IsNeutral((byte)p.Slot)) sim.Players.Cash[p.Slot] = p.Cash;
        foreach (MissionActor a in Def.Actors)
            SpawnActor(sim, a.Type, a.Player, a.Cell, a.Tag.Length > 0 ? a.Tag : null);
    }

    /// <inheritdoc />
    public void OnSetup(Simulation sim)
    {
        _sim = sim;
        _rng = sim.Setup.Seed ^ 0xD1B54A32D192ED03UL;
        sim.UnlockFilter = IsTypeAllowed;
        _callbackDepth = 0;   // the main chunk runs with the API locked
        try { _script.DoString(_source, null, Def.Id + "/" + Def.ScriptFile); }
        catch (InterpreterException e) { throw new InvalidOperationException("mission script failed to load: " + (e.DecoratedMessage ?? e.Message), e); }
    }

    /// <inheritdoc />
    public void OnTick(Simulation sim)
    {
        if (_halted) return;
        _sim = sim;
        _callbacksThisTick = 0;
        int tick = sim.Tick;

        if (!_initDone)
        {
            _initDone = true;
            DynValue init = _script.Globals.Get("OnInit");
            if (init.Type == DataType.Function) Invoke("OnInit", init);
        }

        int count = _triggers.Count;   // triggers added by callbacks start next tick
        for (int i = 0; i < count && !_halted; i++)
        {
            TriggerRecord t = _triggers[i];
            if (t.Done || !ShouldFire(t, sim, tick)) continue;
            if (t.Kind == TriggerKind.Every) t.Next = tick + Math.Max(1, t.A);
            else t.Done = true;
            Invoke(t.Fn, _script.Globals.Get(t.Fn));
        }

        _triggers.RemoveAll(static t => t.Done);
        if (_callbacksThisTick > 0 && !_halted) ValidateState();
    }

    private bool ShouldFire(TriggerRecord t, Simulation sim, int tick)
    {
        switch (t.Kind)
        {
            case TriggerKind.After:
            case TriggerKind.Every:
                return tick >= t.Next;
            case TriggerKind.Enter:
                return Def.ZoneNamed(t.Name) is { } z && AnyMobileInZone(sim, (byte)t.A, z);
            case TriggerKind.Destroyed:
                return !(TryTag(t.Name, out ActorRef r) && sim.TryResolve(r));
            case TriggerKind.AllDestroyed:
                return CountAlive(sim, (byte)t.A, -1, null) == 0;
            default:
                return false;
        }
    }

    private void Invoke(string name, DynValue fn)
    {
        _callbackDepth++;
        _callbacksThisTick++;
        try
        {
            if (fn.Type != DataType.Function) throw new ScriptRuntimeException("callback '" + name + "' is not a global function");
            _script.Call(fn);
        }
        catch (InterpreterException e) { AddError(name + ": " + (e.DecoratedMessage ?? e.Message)); }
        catch (Exception e) when (e is not OutOfMemoryException) { AddError(name + ": " + e.Message); }
        finally { _callbackDepth--; }
    }

    private void AddError(string text)
    {
        if (_errors.Count < MaxErrors) _errors.Add("tick " + (_sim?.Tick ?? 0) + ": " + text);
    }

    private void ValidateState()
    {
        try
        {
            using var sink = new BinaryWriter(Stream.Null, Encoding.UTF8, leaveOpen: true);
            if (_script.Globals.Get("state").Type == DataType.Table) LuaStateCodec.Write(sink, _script.Globals.Get("state").Table, strict: true);
            else throw new ScriptRuntimeException("'state' must stay a table");
        }
        catch (InterpreterException e)
        {
            AddError((e.DecoratedMessage ?? e.Message) + " (script halted)");
            _halted = true;
        }
    }

    private static ulong HashSource(string s) => XxHash64.HashToUInt64(Encoding.UTF8.GetBytes(s));

    /// <summary>Next value of the script's own generator (SplitMix64, Random.Next); not the sim RNG.</summary>
    private ulong NextRandom()
    {
        _rng += 0x9E3779B97F4A7C15UL;
        ulong z = _rng;
        z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
        z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
        return z ^ (z >> 31);
    }

    // ---- unlock gate (Simulation.UnlockFilter) ----

    private bool IsTypeAllowed(byte player, ProductionCategory category, int typeIndex)
    {
        if (player >= _restricted.Length || !_restricted[player]) return true;
        SimRules rules = _sim!.Setup.Rules;
        string? id = category switch
        {
            ProductionCategory.Infantry => typeIndex >= 0 && typeIndex < (rules.Infantry?.Count ?? 0) ? rules.Infantry![typeIndex].Id : null,
            ProductionCategory.Vehicle => typeIndex >= 0 && typeIndex < rules.Vehicles.Count ? rules.Vehicles[typeIndex].Id : null,
            ProductionCategory.Structure or ProductionCategory.Defence =>
                typeIndex >= 0 && typeIndex < (rules.Buildings?.Count ?? 0) ? rules.Buildings![typeIndex].Id : null,
            _ => null,
        };
        return id != null && _granted[player].Contains(id);
    }

    // ---- state part ----

    internal void WriteState(BinaryWriter w)
    {
        w.Write(FormatVersion);
        w.Write(SourceHash);
        w.Write(_initDone); w.Write(_halted); w.Write((byte)_outcome);
        w.Write(_rng); w.Write(_counter); w.Write(_messageSeq);

        w.Write(_tags.Count);
        foreach (var tag in _tags) { w.Write(tag.Key); w.Write((byte)tag.Value.Kind); w.Write(tag.Value.Id.Index); w.Write(tag.Value.Id.Generation); }

        w.Write(_triggers.Count);
        foreach (TriggerRecord t in _triggers) { w.Write((byte)t.Kind); w.Write(t.A); w.Write(t.Name); w.Write(t.Fn); w.Write(t.Next); w.Write(t.Done); }

        w.Write(_objectives.Count);
        foreach (MissionObjective o in _objectives) { w.Write(o.Id); w.Write(o.Text); w.Write(o.Primary); w.Write((byte)o.Status); }

        w.Write(_messages.Count);
        foreach (MissionMessage m in _messages) { w.Write(m.Seq); w.Write(m.Tick); w.Write(m.Text); }

        w.Write(_restricted.Length);
        for (int p = 0; p < _restricted.Length; p++)
        {
            w.Write(_restricted[p]);
            w.Write(_granted[p].Count);
            foreach (string id in _granted[p]) w.Write(id);
        }

        DynValue state = _script.Globals.Get("state");
        LuaStateCodec.Write(w, state.Type == DataType.Table ? state.Table : new Table(_script), strict: false);
    }

    internal void ReadState(BinaryReader r)
    {
        if (r.ReadInt32() != FormatVersion) throw new InvalidDataException("mission state: unsupported version");
        if (r.ReadUInt64() != SourceHash) throw new InvalidDataException("mission state: the save was made with a different mission script");
        _initDone = r.ReadBoolean(); _halted = r.ReadBoolean(); _outcome = (MissionOutcome)r.ReadByte();
        _rng = r.ReadUInt64(); _counter = r.ReadInt32(); _messageSeq = r.ReadInt32();

        _tags.Clear();
        for (int n = r.ReadInt32(), i = 0; i < n; i++)
        {
            string key = r.ReadString();
            var kind = (ActorKind)r.ReadByte();
            _tags.Add(new(key, new ActorRef(kind, new EntityId(r.ReadUInt16(), r.ReadUInt16()))));
        }

        _triggers.Clear();
        for (int n = r.ReadInt32(), i = 0; i < n; i++)
            _triggers.Add(new TriggerRecord { Kind = (TriggerKind)r.ReadByte(), A = r.ReadInt32(), Name = r.ReadString(), Fn = r.ReadString(), Next = r.ReadInt32(), Done = r.ReadBoolean() });

        _objectives.Clear();
        for (int n = r.ReadInt32(), i = 0; i < n; i++)
            _objectives.Add(new MissionObjective(r.ReadString(), r.ReadString(), r.ReadBoolean(), (ObjectiveStatus)r.ReadByte()));

        _messages.Clear();
        for (int n = r.ReadInt32(), i = 0; i < n; i++) _messages.Add(new MissionMessage(r.ReadInt32(), r.ReadInt32(), r.ReadString()));

        if (r.ReadInt32() != _restricted.Length) throw new InvalidDataException("mission state: player count mismatch");
        for (int p = 0; p < _restricted.Length; p++)
        {
            _restricted[p] = r.ReadBoolean();
            _granted[p].Clear();
            for (int n = r.ReadInt32(), i = 0; i < n; i++) _granted[p].Add(r.ReadString());
        }

        _script.Globals.Set("state", DynValue.NewTable(LuaStateCodec.Read(r, _script)));
    }
}
