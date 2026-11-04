// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Sim.Core;
using Rts.Sim.View;

namespace Rts.Llm;

/// <summary>
/// What the commander loop remembers between decisions so <see cref="BattlefieldSummary"/> can report change: the own
/// actors of the previous decision (to detect losses), the last tick each enemy building was seen alive (for "last seen"
/// ages), own buildings that lost health, and the last three directives. Fed from a <see cref="PlayerView"/> only.
/// Call <see cref="Observe"/> once per decision before building the summary; the "recent events" lists then describe the
/// time since the previous <see cref="Observe"/>. Deterministic: ordered collections only.
/// </summary>
public sealed class SummaryHistory
{
    /// <summary>Most directives remembered.</summary>
    public const int MaxDirectives = 3;

    private readonly SortedDictionary<long, (ActorKind Kind, int Type)> _prevOwn = new();
    private readonly SortedDictionary<long, int> _prevBuildingHealth = new();
    private readonly SortedDictionary<long, int> _lastSeen = new();
    private readonly SortedDictionary<long, int> _prevVisibleEnemyBuildings = new();
    private readonly List<(DirectiveDraft Draft, int Tick)> _directives = new();
    private bool _observed;

    /// <summary>Tick of the previous <see cref="Observe"/>, or -1 before the first one.</summary>
    public int PreviousTick { get; private set; } = -1;

    /// <summary>Own units and structures that vanished since the previous observation, as (kind, type index, count), ordered.</summary>
    public IReadOnlyList<(ActorKind Kind, int Type, int Count)> Losses { get; private set; } = Array.Empty<(ActorKind, int, int)>();

    /// <summary>Own structures that lost health since the previous observation, as (type index, count), ordered.</summary>
    public IReadOnlyList<(int Type, int Count)> Attacked { get; private set; } = Array.Empty<(int, int)>();

    /// <summary>Enemy structures that were in view at the previous observation and whose ground is in view now but are gone
    /// (destroyed), as (type index, count), ordered.</summary>
    public IReadOnlyList<(int Type, int Count)> EnemyBuildingsDestroyed { get; private set; } = Array.Empty<(int, int)>();

    /// <summary>The last directives issued (oldest first), at most <see cref="MaxDirectives"/>.</summary>
    public IReadOnlyList<(DirectiveDraft Draft, int Tick)> Directives => _directives;

    /// <summary>Records a directive issued at <paramref name="tick"/>.</summary>
    public void RecordDirective(DirectiveDraft d, int tick)
    {
        _directives.Add((d, tick));
        while (_directives.Count > MaxDirectives) _directives.RemoveAt(0);
    }

    /// <summary>Tick at which the enemy building (owner, type, top-left cell) was last seen alive, or -1 if never observed.</summary>
    public int LastSeenTick(byte owner, int type, CellCoord cell) =>
        _lastSeen.TryGetValue(BuildingKey(owner, type, cell), out int t) ? t : -1;

    /// <summary>Updates the memory from the current view and computes the events since the previous call.</summary>
    public void Observe(PlayerView view)
    {
        var own = new List<ViewActor>();
        view.CopyOwn(own);
        var cur = new SortedDictionary<long, (ActorKind, int)>();
        var curHealth = new SortedDictionary<long, int>();
        foreach (ViewActor a in own)
        {
            long k = OwnKey(a);
            cur[k] = (a.Kind, a.TypeIndex);
            if (a.Kind == ActorKind.Building) curHealth[k] = a.Health;
        }

        var loss = new SortedDictionary<(int, int), int>();
        var att = new SortedDictionary<int, int>();
        var killed = new SortedDictionary<int, int>();
        if (_observed)
        {
            foreach (KeyValuePair<long, (ActorKind Kind, int Type)> p in _prevOwn)
                if (!cur.ContainsKey(p.Key))
                {
                    (int, int) key = ((int)p.Value.Kind, p.Value.Type);
                    loss[key] = loss.GetValueOrDefault(key) + 1;
                }
            foreach (KeyValuePair<long, int> p in curHealth)
                if (_prevBuildingHealth.TryGetValue(p.Key, out int before) && p.Value < before && cur.TryGetValue(p.Key, out var info))
                    att[info.Item2] = att.GetValueOrDefault(info.Item2) + 1;
        }

        // enemy buildings: live ones refresh the last-seen tick; a building seen alive last time that is not listed now while
        // it was in view before and the view lists no ghost of it is gone
        var known = new List<ViewActor>();
        view.CopyKnownEnemyBuildings(known);
        var nowLive = new SortedDictionary<long, int>();
        var nowAny = new SortedDictionary<long, int>();
        foreach (ViewActor b in known)
        {
            long k = BuildingKey(b.Owner, b.TypeIndex, b.Cell);
            nowAny[k] = b.TypeIndex;
            if (b.Ghost) continue;
            _lastSeen[k] = view.Tick;
            nowLive[k] = b.TypeIndex;
        }
        if (_observed)
            foreach (KeyValuePair<long, int> p in _prevVisibleEnemyBuildings)
                if (!nowAny.ContainsKey(p.Key)) killed[p.Value] = killed.GetValueOrDefault(p.Value) + 1;
        foreach (long gone in _lastSeen.Keys.Where(k => !nowAny.ContainsKey(k)).ToList()) _lastSeen.Remove(gone);

        Losses = loss.Select(p => ((ActorKind)p.Key.Item1, p.Key.Item2, p.Value)).ToList();
        Attacked = att.Select(p => (p.Key, p.Value)).ToList();
        EnemyBuildingsDestroyed = killed.Select(p => (p.Key, p.Value)).ToList();

        _prevOwn.Clear();
        foreach (var p in cur) _prevOwn[p.Key] = p.Value;
        _prevBuildingHealth.Clear();
        foreach (var p in curHealth) _prevBuildingHealth[p.Key] = p.Value;
        _prevVisibleEnemyBuildings.Clear();
        foreach (var p in nowLive) _prevVisibleEnemyBuildings[p.Key] = p.Value;
        PreviousTick = view.Tick;
        _observed = true;
    }

    private static long OwnKey(ViewActor a) => ((long)(int)a.Kind << 40) | ((long)a.Id.Index << 16) | a.Id.Generation;

    internal static long BuildingKey(byte owner, int type, CellCoord cell) =>
        ((long)owner << 48) | ((long)(type & 0xFFFF) << 32) | ((long)(cell.X & 0xFFFF) << 16) | (long)(cell.Y & 0xFFFF);
}
