// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Data;
using Rts.Sim;
using Rts.Sim.Core;
using Rts.Sim.Replay;

namespace Rts.Game.Sandbox;

/// <summary>
/// Streams one sandbox session to a replay file: opened at session start (the header is written at once), every
/// command the session issues and a hash check every <see cref="SimConstants.HashIntervalTicks"/> ticks appended as
/// they happen, flushed every 64 ticks, so a crash keeps the recording. Setup spawns come from the sim's setup log.
/// The file of the previous session is kept as the "previous" path. If the file cannot be opened the game still runs
/// and nothing is recorded.
/// </summary>
public sealed class ReplayRecording : IDisposable
{
    private readonly ReplayWriter? _writer;
    private readonly FileStream? _file;
    private readonly string _path;
    private bool _finished;

    /// <summary>Starts recording <paramref name="sim"/> to <paramref name="path"/> (folders are created; an existing file is
    /// moved to <paramref name="previousPath"/> first).</summary>
    public ReplayRecording(Simulation sim, IReadOnlyList<(string Name, string Text)> rulesFiles, string path, string previousPath)
    {
        _path = Path.GetFullPath(path);
        try
        {
            string? dir = Path.GetDirectoryName(_path);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            if (File.Exists(_path)) File.Move(_path, Path.GetFullPath(previousPath), overwrite: true);
            _file = new FileStream(_path, FileMode.Create, FileAccess.Write, FileShare.Read);
            _writer = new ReplayWriter(_file, sim, BuildInfo.Version, rulesFiles);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            Console.Error.WriteLine($"error: replay not recorded: {e.Message}");
            _file?.Dispose();
            _file = null;
            _writer = null;
        }
    }

    /// <summary>Records a command at the moment it is queued, so the replay carries exactly what the sim saw.</summary>
    public void RecordCommand(Command command) => Guard(() => _writer?.RecordCommand(command));

    /// <summary>Records the state hash and sub-hashes at the sim's current tick (one serialisation pass).</summary>
    public void RecordHash(Simulation sim) => Guard(() => _writer?.RecordHash(sim));

    /// <summary>
    /// Writes the end record and closes the file. Returns false and prints the problem if the replay could not be
    /// written (the game keeps running either way). A second call does nothing.
    /// </summary>
    public bool Finish()
    {
        if (_finished) return _writer is not null;
        _finished = true;
        if (_writer is null) return false;
        try
        {
            _writer.Finish();
            _file!.Dispose();
            Console.WriteLine($"replay saved: {_path}");
            return true;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            Console.Error.WriteLine($"error: replay not saved: {e.Message}");
            return false;
        }
    }

    /// <summary>Closes the file without an end record (what a crash leaves behind).</summary>
    public void Dispose() => _file?.Dispose();

    // A full disk or a vanished file must not take the game down.
    private void Guard(Action write)
    {
        if (_writer is null || _finished) return;
        try { write(); }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            Console.Error.WriteLine($"error: replay recording stopped: {e.Message}");
            _finished = true;
        }
    }
}
