// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Sim.Core;
using Rts.Sim.Serialization;

namespace Rts.Sim.Replay;

/// <summary>
/// Records a game as it is played (format version 6). The header is written when the writer is created: magic,
/// version, then the <see cref="SetupHeader"/> (build string, seed, player count, teams, limits, AllowDebug, rules
/// hash, map hash and name, and the rules source files, deflate-compressed). Records are appended as they happen:
/// spawn (taken from <see cref="Simulation.SetupLog"/>, never re-recorded by callers), command (with Arg2) and
/// hash check (with optional per-part sub-hashes). The stream is flushed every <see cref="FlushIntervalTicks"/>
/// ticks and by <see cref="Finish"/>, which appends the end record with the final tick; a replay that lost its end
/// record to a crash is still readable (<see cref="ReplayData.Truncated"/>).
/// </summary>
public sealed class ReplayWriter
{
    public static readonly byte[] Magic = "RTSR"u8.ToArray();
    public const int Version = 6;
    public const byte TypeEnd = 0, TypeCommand = 1, TypeHashCheck = 2, TypeSpawn = 3;

    /// <summary>Ticks between stream flushes.</summary>
    public const int FlushIntervalTicks = 64;

    private readonly Simulation _sim;
    private readonly BinaryWriter _w;
    private int _spawnsWritten;
    private int _lastFlushTick;
    private bool _finished;

    /// <summary>Starts a replay on <paramref name="output"/> and writes the header immediately. The stream stays
    /// open and belongs to the caller. <paramref name="build"/> is e.g. BuildInfo.Version; <paramref name="rulesFiles"/>
    /// are the rules source files (relative name, text) the game was loaded from.</summary>
    public ReplayWriter(Stream output, Simulation sim, string build, IReadOnlyList<(string Name, string Text)> rulesFiles)
    {
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(sim);
        ArgumentNullException.ThrowIfNull(build);
        ArgumentNullException.ThrowIfNull(rulesFiles);
        _sim = sim;
        _w = new BinaryWriter(output, System.Text.Encoding.UTF8, leaveOpen: true);
        _w.Write(Magic); _w.Write(Version);
        SetupHeader.From(sim, build, rulesFiles).Write(_w);
        _w.Flush();
    }

    /// <summary>When true (the default), <see cref="RecordHash(Simulation)"/> writes sub-hashes; when false only the
    /// state hash, which makes a replay smaller but a desync unnamed.</summary>
    public bool RecordSubHashes { get; init; } = true;

    /// <summary>Appends a spawn record for every <see cref="Simulation.SetupLog"/> entry not yet written, stamped with the
    /// sim's current tick (0 for the usual setup before the first Step). Called by every Record* method, so callers
    /// need not call it.</summary>
    public void Sync()
    {
        IReadOnlyList<SetupEntry> log = _sim.SetupLog;
        for (; _spawnsWritten < log.Count; _spawnsWritten++)
        {
            SetupEntry s = log[_spawnsWritten];
            _w.Write(TypeSpawn); _w.Write(_sim.Tick);
            _w.Write((byte)s.Kind); _w.Write(s.Player); _w.Write(s.TypeIndex); _w.Write(s.Cell.X); _w.Write(s.Cell.Y); _w.Write((byte)s.SubSlot);
        }
    }

    public void RecordCommand(Command c)
    {
        Sync();
        _w.Write(TypeCommand);
        _w.Write(c.ExecTick); _w.Write(c.Player); _w.Write(c.Sequence); _w.Write((byte)c.Kind);
        _w.Write(c.Subject.Index); _w.Write(c.Subject.Generation);
        _w.Write(c.Target.Index); _w.Write(c.Target.Generation);
        _w.Write(c.Cell.X); _w.Write(c.Cell.Y); _w.Write(c.Arg); _w.Write(c.Arg2);
    }

    /// <summary>Records the state hash (and sub-hashes if <see cref="RecordSubHashes"/>) of the sim at its current tick,
    /// from one serialisation pass.</summary>
    public void RecordHash(Simulation sim)
    {
        if (!ReferenceEquals(sim, _sim)) throw new ArgumentException("The writer records one simulation.", nameof(sim));
        if (RecordSubHashes)
        {
            var sub = sim.ComputeHashes(out ulong full);
            RecordHash(sim.Tick, full, sub);
        }
        else RecordHash(sim.Tick, sim.ComputeStateHash(), null);
    }

    /// <summary>Record the state hash at a tick, with optional per-part sub-hashes (written only when
    /// <see cref="RecordSubHashes"/> is true). Flushes the stream every <see cref="FlushIntervalTicks"/> ticks.</summary>
    public void RecordHash(int tick, ulong hash, IReadOnlyList<(string Part, ulong Hash)>? subHashes = null)
    {
        Sync();
        _w.Write(TypeHashCheck); _w.Write(tick); _w.Write(hash);
        if (!RecordSubHashes || subHashes is null)
        {
            _w.Write((byte)0);
        }
        else
        {
            _w.Write((byte)1);
            _w.Write(subHashes.Count);
            foreach ((string part, ulong h) in subHashes) { _w.Write(part); _w.Write(h); }
        }
        if (tick - _lastFlushTick >= FlushIntervalTicks)
        {
            _lastFlushTick = tick;
            _w.Flush();   // BinaryWriter.Flush also flushes the stream
        }
    }

    /// <summary>Writes the end record with the sim's current tick and flushes. The stream is left open.</summary>
    public void Finish()
    {
        if (_finished) return;
        _finished = true;
        Sync();
        _w.Write(TypeEnd); _w.Write(_sim.Tick);
        _w.Flush();
    }
}
