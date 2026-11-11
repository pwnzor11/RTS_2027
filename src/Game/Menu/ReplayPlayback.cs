// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Scripting;
using Rts.Sim;
using Rts.Sim.Core;
using Rts.Sim.Map;
using Rts.Sim.Replay;

namespace Rts.Game.Menu;

/// <summary>
/// A replay being played forward for viewing: a simulation stepped by the recorded commands (and the recorded setup spawns), with the
/// timing of <see cref="ReplayPlaybackClock"/>. Read-only: nothing here accepts input for the sim. It does what
/// <see cref="ReplayPlayer"/> does, but a step at a time; at every recorded hash tick it compares the state hash, so a replay that no
/// longer matches the game data shows <see cref="DesyncTick"/> instead of silently playing something else.
/// </summary>
public sealed class ReplayPlayback
{
    private readonly ReplayData _data;
    private readonly List<(int Tick, int Order, Command Command)> _commands = new();
    private readonly Dictionary<int, ulong> _hashes = new();
    private readonly List<Command> _slice = new();
    private int _nextSpawn;
    private int _nextCommand;
    private int _viewer = -1;
    private IReadOnlyList<LeptonCoord> _projectiles = Array.Empty<LeptonCoord>();

    /// <summary>
    /// Creates the playback. <paramref name="setupHook"/> (a mission replay's <c>PlaceInitialActors</c>) owns the tick-0 setup when given: the
    /// recorded tick-0 spawns are then skipped, as <see cref="ReplayPlayer.Run(ReplayData, SimSetup, Action{Simulation})"/> does.
    /// </summary>
    /// <param name="sim">A fresh simulation built from the replay's setup.</param>
    /// <param name="data">The parsed replay.</param>
    /// <param name="setupHook">Places the tick-0 actors, or null to apply the recorded spawns.</param>
    /// <param name="host">The mission script host that runs inside the sim, or null.</param>
    public ReplayPlayback(Simulation sim, ReplayData data, Action<Simulation>? setupHook = null, MissionHost? host = null)
    {
        Sim = sim;
        _data = data;
        Host = host;
        Clock = new ReplayPlaybackClock(data.FinalTick);
        foreach (ReplayRecord r in data.Records)
        {
            if (r.Type == ReplayWriter.TypeCommand) _commands.Add((r.Command.ExecTick, _commands.Count, r.Command));
            else if (r.Type == ReplayWriter.TypeHashCheck) _hashes[r.HashTick] = r.Hash;
        }
        _commands.Sort(static (a, b) => a.Tick != b.Tick ? a.Tick.CompareTo(b.Tick) : a.Order.CompareTo(b.Order));

        if (setupHook is null) ApplySpawns();
        else
        {
            setupHook(sim);
            while (_nextSpawn < data.Spawns.Count && data.Spawns[_nextSpawn].Tick <= 0) _nextSpawn++;
        }
        Current = sim.Snapshot(_viewer);
        Previous = Current;
        _projectiles = CaptureProjectiles();
    }

    /// <summary>The simulation being replayed.</summary>
    public Simulation Sim { get; }

    /// <summary>The map.</summary>
    public MapData Map => Sim.Setup.Map;

    /// <summary>The mission script host (mission replays), or null.</summary>
    public MissionHost? Host { get; }

    /// <summary>The timing model.</summary>
    public ReplayPlaybackClock Clock { get; }

    /// <summary>The replay's header and records.</summary>
    public ReplayData Data => _data;

    /// <summary>Snapshot at the current tick.</summary>
    public SimSnapshot Current { get; private set; }

    /// <summary>Snapshot one tick earlier (for drawing between ticks).</summary>
    public SimSnapshot Previous { get; private set; }

    /// <summary>Projectile positions at the current tick.</summary>
    public IReadOnlyList<LeptonCoord> Projectiles => _projectiles;

    /// <summary>Whose fog view is shown: -1 everything, else a player slot.</summary>
    public int Viewer => _viewer;

    /// <summary>First tick at which the state hash differed from the recorded one, or -1 while everything matched.</summary>
    public int DesyncTick { get; private set; } = -1;

    /// <summary>True for a replay of a campaign mission or the tutorial (it re-runs a mission script).</summary>
    public bool IsMissionReplay => Host is not null;

    /// <summary>
    /// S18 GS-03 (owner decision): whether the viewer shows the desync warning. Mission replays are watchable only (they carry no tier or
    /// campaign tech, see <see cref="MissionReplay.WatchableOnlyNote"/>), so only skirmish replays warn.
    /// </summary>
    public bool ShowDesyncWarning => DesyncTick >= 0 && !IsMissionReplay;

    /// <summary>Moves to the next view: everything, player 0, player 1, ... then everything again.</summary>
    public void CycleViewer()
    {
        _viewer = _viewer + 1 >= Sim.Setup.PlayerCount ? -1 : _viewer + 1;
        Current = Sim.Snapshot(_viewer);
        Previous = Current;
    }

    /// <summary>Adds elapsed wall time and runs the ticks that are due. Returns the number run.</summary>
    public int Advance(long elapsedMicroseconds) => Run(Clock.Advance(elapsedMicroseconds));

    /// <summary>Runs one tick (works while paused).</summary>
    public int StepOne() => Run(Clock.StepOne());

    /// <summary>Jumps forward by <see cref="ReplayPlaybackClock.ScrubSeconds"/> of game time.</summary>
    public int ScrubForward() => Run(Clock.ScrubForward());

    private int Run(int count)
    {
        if (count <= 0) return 0;
        for (int i = 0; i < count; i++)
        {
            if (i == count - 1) Previous = Sim.Snapshot(_viewer);
            StepCore();
        }
        Current = Sim.Snapshot(_viewer);
        _projectiles = CaptureProjectiles();
        return count;
    }

    private void StepCore()
    {
        ApplySpawns();   // a spawn stamped T happened after the hash check at T, before the Step out of T
        _slice.Clear();
        while (_nextCommand < _commands.Count && _commands[_nextCommand].Tick < Sim.Tick) _nextCommand++;   // late: Step would ignore them
        while (_nextCommand < _commands.Count && _commands[_nextCommand].Tick == Sim.Tick) _slice.Add(_commands[_nextCommand++].Command);
        Sim.Step(_slice);
        if (DesyncTick < 0 && _hashes.TryGetValue(Sim.Tick, out ulong expected) && Sim.ComputeStateHash() != expected) DesyncTick = Sim.Tick;
    }

    private void ApplySpawns()
    {
        while (_nextSpawn < _data.Spawns.Count && _data.Spawns[_nextSpawn].Tick <= Sim.Tick)
        {
            ReplaySpawn s = _data.Spawns[_nextSpawn++];
            if (s.Kind == ActorKind.Building) Sim.PlaceBuilding(s.Player, s.Type, s.Cell);
            else if (s.Kind == ActorKind.Infantry) Sim.SpawnInfantry(s.Player, s.Type, s.Cell, s.SubSlot);
            else Sim.SpawnVehicle(s.Player, s.Type, s.Cell);
        }
    }

    private List<LeptonCoord> CaptureProjectiles()
    {
        var list = new List<LeptonCoord>();
        var store = Sim.Projectiles;
        for (int i = 1; i < store.Capacity; i++)
            if (store.Slots.IsIndexAlive(i)) list.Add(store.Position[i]);
        return list;
    }
}
