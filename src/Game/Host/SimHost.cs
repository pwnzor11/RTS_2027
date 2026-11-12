// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using System.Diagnostics;
using Rts.AI;
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
    private readonly List<Command> _aiScratch = new();
    private readonly ushort[] _sequence = new ushort[256]; // one counter per player (Command.Player is a byte)
    private long _accumulatedUs;
    private bool _paused;
    private int _speedMultiplier = 1;
    private int _speedPercent = GameSpeed.Normal;
    private long _speedCarry;   // leftover hundredths of a microsecond from scaling the elapsed time
    private int _viewer;
    private bool _spectator;
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

    /// <summary>
    /// AI players hosted outside the sim (D-051). When set, <see cref="AiDriver.Collect"/> runs before every Step and its
    /// commands join the pending list and the replay recording, exactly as if a player had issued them.
    /// </summary>
    public AiDriver? Ai { get; set; }

    /// <summary>
    /// LAN lockstep (M9, design 20 §9). When set, <see cref="Issue"/> and <see cref="IssueOrder"/> hand the command to this sink
    /// instead of queuing it: the host's turn scheduler stamps ExecTick and Sequence and the command comes back in a sealed turn
    /// bundle through <see cref="StepWithBundle"/>. Null (the default) keeps single-player behaviour unchanged.
    /// </summary>
    public Action<Command>? Outbox { get; set; }

    /// <summary>Optional timing sink for the perf benchmark (M4-16); null in the game, so the clock is never read.</summary>
    public HostProfile? Profile { get; set; }

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

    /// <summary>Next command sequence number of each player, trimmed after the last player that issued anything (saved with a game).</summary>
    public ushort[] Sequences
    {
        get
        {
            int n = _sequence.Length;
            while (n > 0 && _sequence[n - 1] == 0) n--;
            return _sequence[..n];
        }
    }

    /// <summary>
    /// Puts back the command queue of a saved game: the commands queued and not yet run, and the per-player sequence counters. Call
    /// right after loading, before the first step. The commands go in as they are (nothing is stamped or recorded again).
    /// </summary>
    public void RestoreQueue(IEnumerable<Command> pending, IReadOnlyList<ushort> sequences)
    {
        _pending.Clear();
        _pending.AddRange(pending);
        Array.Clear(_sequence);
        for (int i = 0; i < sequences.Count && i < _sequence.Length; i++) _sequence[i] = sequences[i];
    }

    /// <summary>Which player's view the snapshot shows: -1 for the omniscient view (full map vision), else the player whose fog applies.</summary>
    public int Viewer => _viewer;

    /// <summary>The speeds a spectator can pick (M4-08): 1x, 2x, 4x, 8x. Keys 1..4 select by index.</summary>
    public static IReadOnlyList<int> SpeedSteps { get; } = new[] { 1, 2, 4, 8 };

    /// <summary>
    /// True for an AI-vs-AI game watched from outside (M4-08): <see cref="Issue"/> and <see cref="IssueOrder"/> then refuse every
    /// command, so no input can reach the sim. AI commands do not pass through them (see <see cref="StepCore"/>), so the AIs
    /// keep playing and the replay is unchanged.
    /// </summary>
    public bool Spectator
    {
        get => _spectator;
        set => _spectator = value;
    }

    /// <summary>Number of commands <see cref="Issue"/> or <see cref="IssueOrder"/> refused while <see cref="Spectator"/> was set.</summary>
    public int RefusedCommands { get; private set; }

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
    /// Single-player game speed in percent of normal (<see cref="GameSpeed.Percents"/>, S19 D-205). It scales the elapsed time that
    /// <see cref="Advance"/> adds, so the sim step is unchanged and only the steps per real second differ. Front end only; it is not saved.
    /// </summary>
    public int SpeedPercent
    {
        get => _speedPercent;
        set => _speedPercent = GameSpeed.Snap(value);
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

        long scaled = Math.Max(0, elapsedMicroseconds);
        if (_speedPercent != GameSpeed.Normal)
        {
            long hundredths = scaled * _speedPercent + _speedCarry;
            scaled = hundredths / 100;
            _speedCarry = hundredths % 100;
        }

        _accumulatedUs = Math.Min(_accumulatedUs + scaled, MaxAccumulatedMicroseconds);
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

    /// <summary>
    /// LAN lockstep (M9): queues the commands of the sealed turn bundle for the tick about to run (already stamped by the host, so
    /// nothing is re-stamped), records them in the replay, then runs exactly one tick. <see cref="Advance"/> is not used in a LAN
    /// game; the lockstep driver calls this once per tick it holds a bundle for. Set <see cref="Ai"/> to null on LAN peers: the host
    /// runs the AIs itself and their commands arrive in bundles like everyone else's.
    /// </summary>
    public void StepWithBundle(IReadOnlyList<Command> bundle)
    {
        foreach (Command command in bundle)
        {
            _pending.Add(command);
            _recording?.RecordCommand(command);
        }
        StepCore();
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
    /// <returns>The command as queued (unchanged and not queued when <see cref="Spectator"/> is set).</returns>
    public Command Issue(Command command)
    {
        if (_spectator)
        {
            RefusedCommands++;
            return command;   // not queued, not recorded
        }

        if (Outbox is { } outbox)
        {
            outbox(command);   // LAN: stamped and recorded when it comes back in a turn bundle
            return command;
        }

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

    /// <summary>
    /// Moves to the next view: player 0, 1, … N-1, then omniscient (-1), then player 0 again (generalises the old 0, 1, -1).
    /// Neutral slots are skipped. The sim is not touched, so the state hash is unchanged.
    /// </summary>
    public void CycleViewer()
    {
        int next = _viewer + 1;
        while (next < ViewerCount && _sim.Players.IsNeutral((byte)next)) next++;
        SetViewer(next >= ViewerCount ? -1 : next);
    }

    /// <summary>Number of player slots a viewer can be (the sim's player count).</summary>
    public int ViewerCount => _sim.Setup.PlayerCount;

    /// <summary>
    /// Sets the view: -1 = full map vision, 0..N-1 = that player's fog. Out-of-range values are clamped to that range. Rebuilds
    /// the snapshots; reading a snapshot never changes the sim, so the hash is the same for every viewer.
    /// </summary>
    public void SetViewer(int viewer)
    {
        _viewer = Math.Clamp(viewer, -1, ViewerCount - 1);
        RefreshSnapshots();
    }

    /// <summary>Sets <see cref="SpeedMultiplier"/> to <see cref="SpeedSteps"/>[<paramref name="index"/>] (index clamped to 0..3).</summary>
    public void SetSpeedStep(int index) => SpeedMultiplier = SpeedSteps[Math.Clamp(index, 0, SpeedSteps.Count - 1)];

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
        HostProfile? prof = Profile;
        long t0 = prof is null ? 0 : Stopwatch.GetTimestamp();
        if (Ai is { } ai)
        {
            _aiScratch.Clear();
            ai.Collect(_sim, _aiScratch);
            foreach (Command command in _aiScratch)
            {
                _pending.Add(command);
                _recording?.RecordCommand(command);
            }
        }

        long t1 = prof is null ? 0 : Stopwatch.GetTimestamp();
        _sim.Step(_pending);
        // Commands for the tick that just ran have been applied (or were never due); drop them.
        _pending.RemoveAll(c => c.ExecTick <= ran);
        if (_sim.Tick % SimConstants.HashIntervalTicks == 0) _recording?.RecordHash(_sim);
        long t2 = prof is null ? 0 : Stopwatch.GetTimestamp();
        _current = _sim.Snapshot(_viewer);
        _projectiles = CaptureProjectiles();
        if (prof is not null)
        {
            long t3 = Stopwatch.GetTimestamp();
            prof.AiTicks += t1 - t0;
            prof.StepTicks += t2 - t1;
            prof.SnapshotTicks += t3 - t2;
            prof.Steps++;
            prof.Snapshots++;
        }
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
