// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Game.Sandbox;
using Rts.Sim;
using Rts.Sim.Core;

namespace Rts.Game.Host;

/// <summary>
/// The only thing that calls <see cref="Simulation.Step"/> (review C-2). Owns the simulation, the pending command list,
/// the per-player command sequence counters, the replay hook and the advance policy. It has no clock: the caller passes
/// elapsed time to <see cref="Advance"/> (MonoGame's game time today, message arrival for a future LAN host).
/// Renderers read only <see cref="Current"/>, <see cref="Previous"/> and <see cref="Alpha"/>.
/// </summary>
public sealed class SimHost
{
    /// <summary>Ticks between a command being issued and running (D-014, single player).</summary>
    public const int CommandDelayTicks = 1;

    /// <summary>Length of one tick in microseconds (50 ms at 20 Hz).</summary>
    public const long TickMicroseconds = 1_000_000 / SimConstants.TicksPerSecond;

    /// <summary>Most elapsed time <see cref="Advance"/> keeps, so a stall does not cause a burst of ticks.</summary>
    public const long MaxAccumulatedMicroseconds = 250_000;

    private readonly Simulation _sim;
    private readonly ReplayRecording? _recording;
    private readonly List<Command> _pending = new();
    private readonly ushort[] _sequence = new ushort[256]; // one counter per player (Command.Player is a byte)
    private long _accumulatedUs;
    private bool _paused;
    private int _speedMultiplier = 1;
    private int _viewer;
    private SimSnapshot _previous;
    private SimSnapshot _current;
    private IReadOnlyList<LeptonCoord> _projectiles = Array.Empty<LeptonCoord>();

    /// <summary>Wraps a simulation. Call <see cref="RefreshSnapshots"/> after spawning units into it.</summary>
    /// <param name="sim">The simulation to drive.</param>
    /// <param name="recording">Receives every command and periodic hash; null for no replay.</param>
    public SimHost(Simulation sim, ReplayRecording? recording = null)
    {
        _sim = sim;
        _recording = recording;
        _current = sim.Snapshot(_viewer);
        _previous = _current;
        _projectiles = CaptureProjectiles();
    }

    /// <summary>The simulation. Read it between steps only; commands go through <see cref="Issue"/>.</summary>
    public Simulation Sim => _sim;

    /// <summary>Current simulation tick.</summary>
    public int Tick => _sim.Tick;

    /// <summary>Latest snapshot.</summary>
    public SimSnapshot Current => _current;

    /// <summary>The snapshot one tick before <see cref="Current"/> (equal to it before the second tick).</summary>
    public SimSnapshot Previous => _previous;

    /// <summary>
    /// 0..1 from <see cref="Previous"/> to <see cref="Current"/>. While paused it is 1, so a single step shows its
    /// result at once.
    /// </summary>
    public float Alpha => _paused ? 1f : _accumulatedUs / (float)TickMicroseconds;

    /// <summary>Projectile positions in leptons at the current tick (not interpolated).</summary>
    public IReadOnlyList<LeptonCoord> Projectiles => _projectiles;

    /// <summary>Commands queued and not yet run, in issue order (the sim sorts them per tick).</summary>
    public IReadOnlyList<Command> Pending => _pending;

    /// <summary>Which player's view the snapshot shows: 0, 1, or -1 for the omniscient view.</summary>
    public int Viewer => _viewer;

    /// <summary>True while paused. Pausing drops any partly accumulated time.</summary>
    public bool Paused
    {
        get => _paused;
        set
        {
            _paused = value;
            if (value) _accumulatedUs = 0;
        }
    }

    /// <summary>Ticks run per tick interval while running (1 = normal speed). Front end only.</summary>
    public int SpeedMultiplier
    {
        get => _speedMultiplier;
        set => _speedMultiplier = Math.Max(1, value);
    }

    /// <summary>
    /// Adds <paramref name="elapsedMicroseconds"/> and runs every whole tick that is due, <see cref="SpeedMultiplier"/>
    /// steps per interval. Does nothing while paused. Accumulated time is capped at
    /// <see cref="MaxAccumulatedMicroseconds"/> (catch-up cap).
    /// </summary>
    /// <returns>The number of ticks stepped.</returns>
    public int Advance(long elapsedMicroseconds)
    {
        if (_paused)
        {
            _accumulatedUs = 0;
            return 0;
        }

        _accumulatedUs = Math.Min(_accumulatedUs + Math.Max(0, elapsedMicroseconds), MaxAccumulatedMicroseconds);
        int stepped = 0;
        while (_accumulatedUs >= TickMicroseconds)
        {
            _accumulatedUs -= TickMicroseconds;
            for (int i = 0; i < _speedMultiplier; i++)
            {
                StepCore();
                stepped++;
            }
        }
        return stepped;
    }

    /// <summary>Runs exactly one tick now, whatever the pause state or the clock.</summary>
    public void StepOnce() => StepCore();

    /// <summary>Runs exactly <paramref name="count"/> ticks now, ignoring the clock (headless runs).</summary>
    public void StepTicks(int count)
    {
        for (int i = 0; i < count; i++) StepCore();
    }

    /// <summary>
    /// Queues a command and records it in the replay. Stamps the next sequence number of <c>command.Player</c>, and
    /// moves ExecTick up to <c>Tick + CommandDelayTicks</c> if it is earlier (so a default ExecTick of 0 means "as soon as allowed").
    /// </summary>
    /// <returns>The command as queued.</returns>
    public Command Issue(Command command)
    {
        int exec = Math.Max(command.ExecTick, _sim.Tick + CommandDelayTicks);
        Command stamped = command with { ExecTick = exec, Sequence = _sequence[command.Player]++ };
        _pending.Add(stamped);
        _recording?.RecordCommand(stamped);
        return stamped;
    }

    /// <summary>
    /// Builds an order with <see cref="OrderBuilder"/> and issues it. <paramref name="execTick"/> defaults to
    /// <c>Tick + CommandDelayTicks</c>.
    /// </summary>
    public Command IssueOrder(byte player, CommandKind kind, UnitKey subject, CellCoord cell = default,
        ActorRef target = default, int? execTick = null) =>
        Issue(OrderBuilder.Build(execTick ?? _sim.Tick + CommandDelayTicks, player, 0, kind, subject, cell, target));

    /// <summary>Moves to the next fog view: player 0, player 1, omniscient, then player 0 again.</summary>
    public void CycleViewer()
    {
        _viewer = _viewer switch { 0 => 1, 1 => -1, _ => 0 };
        RefreshSnapshots();
    }

    /// <summary>Rebuilds the snapshots from the sim as it is now (after setup spawns or a viewer change).</summary>
    public void RefreshSnapshots()
    {
        _current = _sim.Snapshot(_viewer);
        _previous = _current;
        _projectiles = CaptureProjectiles();
    }

    private void StepCore()
    {
        _previous = _current;
        int ran = _sim.Tick;
        _sim.Step(_pending);
        // Commands for the tick that just ran have been applied (or were never due); drop them.
        _pending.RemoveAll(c => c.ExecTick <= ran);
        if (_sim.Tick % SimConstants.HashIntervalTicks == 0) _recording?.RecordHash(_sim);
        _current = _sim.Snapshot(_viewer);
        _projectiles = CaptureProjectiles();
    }

    private List<LeptonCoord> CaptureProjectiles()
    {
        var list = new List<LeptonCoord>();
        var store = _sim.Projectiles;
        for (int i = 1; i < store.Capacity; i++)
            if (store.Slots.IsIndexAlive(i)) list.Add(store.Position[i]);
        return list;
    }
}
