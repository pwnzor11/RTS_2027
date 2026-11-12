// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Sim.Core;

namespace Rts.Net.Lockstep;

/// <summary>
/// Turn bundles a peer holds but has not stepped yet (M9-03). Tick T may run only when bundle T has arrived; bundles 0..Delay-1 are
/// implicitly empty (no command can be stamped that early) and are never sent. Duplicates and bundles for ticks already taken are
/// ignored. The host keeps one too, for its own sim.
/// </summary>
public sealed class TurnBuffer
{
    private readonly Dictionary<int, Command[]> _held = new();
    private int _nextTake;

    /// <summary>Creates a buffer for a fixed command delay (ticks, at least 1).</summary>
    public TurnBuffer(int delay)
    {
        if (delay < 1) throw new ArgumentOutOfRangeException(nameof(delay), "Command delay must be at least 1 tick.");
        Delay = delay;
    }

    /// <summary>The command delay in ticks.</summary>
    public int Delay { get; }

    /// <summary>Bundles held and not yet taken.</summary>
    public int Count => _held.Count;

    /// <summary>The next tick <see cref="Take"/> expects.</summary>
    public int NextTick => _nextTake;

    /// <summary>Stores bundle <paramref name="tick"/>. False when it is implicit, already taken, or already held.</summary>
    public bool Add(int tick, Command[] commands)
    {
        ArgumentNullException.ThrowIfNull(commands);
        if (tick < Delay || tick < _nextTake || _held.ContainsKey(tick)) return false;
        _held[tick] = commands;
        return true;
    }

    /// <summary>True when tick <paramref name="tick"/> can run: it is implicit (below Delay) or its bundle is held.</summary>
    public bool CanStep(int tick) => tick >= _nextTake && (tick < Delay || _held.ContainsKey(tick));

    /// <summary>The last tick that can run without waiting, counting on from <see cref="NextTick"/> (NextTick - 1 when none can).</summary>
    public int ReadyThrough
    {
        get
        {
            int t = _nextTake;
            while (CanStep(t)) t++;
            return t - 1;
        }
    }

    /// <summary>Takes the commands of <see cref="NextTick"/> (empty for an implicit tick) and moves on one tick.</summary>
    public Command[] Take()
    {
        int tick = _nextTake;
        if (!CanStep(tick)) throw new InvalidOperationException($"Bundle {tick} has not arrived.");
        Command[] commands = Array.Empty<Command>();
        if (tick >= Delay)
        {
            commands = _held[tick];
            _held.Remove(tick);
        }
        _nextTake++;
        return commands;
    }
}
