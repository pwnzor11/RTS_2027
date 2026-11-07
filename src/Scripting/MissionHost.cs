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
    // ASSUMED (M3-12): one script callback (or the main chunk) may run at most this many Lua instructions; past that it is a script error.
    /// <summary>Lua instructions one script callback may run before it is stopped with an error (M3-12).</summary>
    public const int CallbackInstructionLimit = 200_000;
    private const int MaxMessages = 32;
    private const int MaxErrors = 50;
    /// <summary>
    /// Part format written when any M8-02 field (timers, hidden objectives, trigger extras, AI hook flags) is non-default. A game that
    /// uses none of them still writes <see cref="BaseFormatVersion"/> with the exact bytes of M3-M7, so schema-1 missions keep their
    /// hashes. Both versions are read. New fields are appended to the version-2 tail only.
    /// </summary>
    private const int FormatVersion = 2;
    private const int BaseFormatVersion = 1;
    /// <summary>M8-03: version 2 plus the difficulty tier after the AI hook flags. Written only when the tier is not the default.</summary>
    private const int TierFormatVersion = 3;
    /// <summary>M8-04: version 3 plus the queued reinforcement arrivals. Written only while an arrival is queued.</summary>
    private const int ArrivalsFormatVersion = 4;
    /// <summary>M11-05: version 4 plus the objective targets. Written only while some objective has a target.</summary>
    private const int TargetsFormatVersion = 5;

    private readonly string _source;
    private readonly Script _script;
    private readonly MissionStatePart _part;
    private readonly InstructionBudget _budget = new(CallbackInstructionLimit);
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
    // M8-02 additions (the version-2 tail of the part).
    private readonly List<MissionTimer> _timers = new();
    private readonly AiHookState[] _aiHooks;
    // M8-03 addition (version-3 tail): difficulty tier 1..5 for Mission.Difficulty().
    private int _difficulty = DefaultDifficulty;

    // Outputs for the game layer: not state, not hashed, not saved (plan 3.7).
    private readonly List<MissionDialogue> _dialogue = new();
    private MissionCameraHint _cameraHint = default;

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
        _aiHooks = new AiHookState[n];
        Array.Fill(_aiHooks, AiHookState.None);
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
        _script.AttachDebugger(_budget);
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

    /// <summary>Visible countdown timers (M8-03), in creation order.</summary>
    public IReadOnlyList<MissionTimer> Timers => _timers;

    /// <summary>Dialogue lines queued by the script, oldest first (output, M8-05).</summary>
    public IReadOnlyList<MissionDialogue> Dialogue => _dialogue;

    /// <summary>The latest camera hint (output, M8-05); <see cref="MissionCameraHint.Seq"/> 0 = none.</summary>
    public MissionCameraHint CameraHint => _cameraHint;

    /// <summary>What the script asked the built-in AI of this player to do (D-051: read by the AI on its next decision, never written by it).</summary>
    public AiHookState AiHookOf(int player) => player >= 0 && player < _aiHooks.Length ? _aiHooks[player] : AiHookState.None;

    /// <summary>Display text for an objective or timer label: the locale text when the label is a key in the mission's locale, else the label
    /// itself (s12). Display only; the hashed state keeps the label as written.</summary>
    public string Label(string text) => Def.Locale.Contains(text) ? Def.Locale.Resolve(text) : text;

    /// <summary>The objectives the HUD should list (hidden ones left out).</summary>
    public IEnumerable<MissionObjective> VisibleObjectives => _objectives.Where(static o => !o.Hidden);

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
        _budget.Reset();
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

        RunArrivals(sim);   // M8-04: queued Reinforce.Delayed / Reinforce.Transport work

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
        RunTimers(tick);   // M8-03
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
                return !(TryTag(t.Name, out ActorRef r) && IsStanding(sim, r));
            case TriggerKind.AllDestroyed:
                return CountAlive(sim, (byte)t.A, -1, null) == 0;
            default:
                return ShouldFireArea(t, sim);   // M8-03: Leave, Hold, Seen, Idle
        }
    }

    /// <summary>
    /// M8-17: an actor that is resolvable and still has health. The sim removes a dead actor (and may end the game) in RunCleanup, AFTER
    /// the trigger phase of the tick it died on; counting health 0 as gone lets OnDestroyed fire on that same tick, like OnAllDestroyed.
    /// </summary>
    private static bool IsStanding(Simulation sim, ActorRef r)
    {
        if (!sim.TryResolve(r)) return false;
        int i = r.Id.Index;
        return r.Kind switch
        {
            ActorKind.Vehicle => sim.Units.Health[i] > 0,
            ActorKind.Infantry => sim.Infantry.Health[i] > 0,
            ActorKind.Building => sim.Buildings.Health[i] > 0,
            _ => true,
        };
    }

    private void Invoke(string name, DynValue fn)
    {
        _callbackDepth++;
        _callbacksThisTick++;
        _budget.Reset();
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
        // Each version appends to the one before, and each is written only when its state is in use, so older layouts keep their bytes.
        bool v5 = HasObjectiveTarget();                         // M11-05 objective targets
        bool v4 = v5 || _arrivals.Count > 0;                    // M8-04 arrivals
        bool v3 = v4 || _difficulty != DefaultDifficulty;       // M8-03 difficulty tier
        bool v2 = v3 || HasVersion2State();
        w.Write(v5 ? TargetsFormatVersion : v4 ? ArrivalsFormatVersion : v3 ? TierFormatVersion : v2 ? FormatVersion : BaseFormatVersion);
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

        if (!v2) return;
        // ---- version 2 tail (M8-02); append only ----
        w.Write(_timers.Count);
        foreach (MissionTimer t in _timers) { w.Write(t.Id); w.Write(t.Text); w.Write(t.EndTick); w.Write(t.Fn); w.Write(t.Paused); }
        foreach (MissionObjective o in _objectives) w.Write(o.Hidden);          // count known from the objectives above
        foreach (TriggerRecord t in _triggers) { w.Write(t.B); w.Write(t.Counter); }   // count known from the triggers above
        foreach (AiHookState h in _aiHooks) { w.Write(h.AttackZone); w.Write(h.DefendZone); w.Write(h.Tier); w.Write(h.WaveSeq); w.Write(h.WaveId); w.Write(h.WaveZone); }
        if (v3) w.Write(_difficulty);   // ---- version 3 tail (M8-03) ----
        if (v4) WriteArrivals(w);       // ---- version 4 tail (M8-04) ----
        if (v5) foreach (MissionObjective o in _objectives) { w.Write((byte)o.Target.Kind); w.Write(o.Target.Name); w.Write(o.Target.X); w.Write(o.Target.Y); }   // ---- version 5 tail (M11-05) ----
    }

    private bool HasObjectiveTarget()
    {
        foreach (MissionObjective o in _objectives) if (o.Target.IsSet) return true;
        return false;
    }

    private bool HasVersion2State()
    {
        if (_timers.Count > 0) return true;
        foreach (MissionObjective o in _objectives) if (o.Hidden) return true;
        foreach (TriggerRecord t in _triggers) if (t.B != 0 || t.Counter != 0) return true;
        foreach (AiHookState h in _aiHooks) if (h != AiHookState.None) return true;
        return false;
    }

    internal void ReadState(BinaryReader r)
    {
        int version = r.ReadInt32();
        if (version is not (BaseFormatVersion or FormatVersion or TierFormatVersion or ArrivalsFormatVersion or TargetsFormatVersion)) throw new InvalidDataException("mission state: unsupported version");
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

        _timers.Clear();
        _arrivals.Clear();
        Array.Fill(_aiHooks, AiHookState.None);
        _difficulty = DefaultDifficulty;
        if (version < FormatVersion) return;
        for (int n = r.ReadInt32(), i = 0; i < n; i++) _timers.Add(new MissionTimer(r.ReadString(), r.ReadString(), r.ReadInt32(), r.ReadString(), r.ReadBoolean()));
        for (int i = 0; i < _objectives.Count; i++) _objectives[i] = _objectives[i] with { Hidden = r.ReadBoolean() };
        foreach (TriggerRecord t in _triggers) { t.B = r.ReadInt32(); t.Counter = r.ReadInt32(); }
        for (int p = 0; p < _aiHooks.Length; p++) _aiHooks[p] = new AiHookState(r.ReadString(), r.ReadString(), r.ReadInt32(), r.ReadInt32(), r.ReadString(), r.ReadString());
        if (version >= TierFormatVersion) _difficulty = r.ReadInt32();
        if (version >= ArrivalsFormatVersion) ReadArrivals(r);   // M8-04
        if (version >= TargetsFormatVersion)
            for (int i = 0; i < _objectives.Count; i++)
                _objectives[i] = _objectives[i] with { Target = new ObjectiveTarget((ObjectiveTargetKind)r.ReadByte(), r.ReadString(), r.ReadInt32(), r.ReadInt32()) };   // M11-05
    }
}
