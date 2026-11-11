// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using System.Text;

namespace Rts.Game;

/// <summary>
/// S18 GAMEREND-03: one small log file for the diagnostics the game prints. The game is a WinExe, so <c>Console.Error</c> goes nowhere
/// on Windows; <see cref="Install"/> tees the console writers into <c>%APPDATA%/RTS_2027/logs/rts_2027.log</c> (beside the settings file).
/// The file is size-capped: when it reaches the cap it is moved to <c>.old</c> (replacing the previous one), so two files at most
/// are kept. Writing never throws.
/// </summary>
public sealed class AppLog
{
    /// <summary>Default size cap of the log file in bytes.</summary>
    public const long DefaultMaxBytes = 256 * 1024;

    private readonly object _gate = new();
    private readonly long _maxBytes;

    /// <summary>Creates a log writing to <paramref name="path"/>; the file is created on the first write.</summary>
    public AppLog(string path, long maxBytes = DefaultMaxBytes)
    {
        Path = path;
        _maxBytes = maxBytes;
    }

    /// <summary>The process-wide log used by <see cref="Install"/>, <see cref="Line"/> and the crash report.</summary>
    public static AppLog Shared { get; } = new(DefaultPath);

    /// <summary>The default log path: the <c>logs</c> folder beside the settings file.</summary>
    public static string DefaultPath =>
        System.IO.Path.Combine(System.IO.Path.GetDirectoryName(Settings.DefaultPath) ?? ".", "logs", "rts_2027.log");

    /// <summary>The file this log writes to.</summary>
    public string Path { get; }

    /// <summary>Appends a timestamped line. Returns false (and never throws) when the file cannot be written.</summary>
    public bool Write(string line)
    {
        try
        {
            lock (_gate)
            {
                string? dir = System.IO.Path.GetDirectoryName(Path);
                if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
                var info = new FileInfo(Path);
                if (info.Exists && info.Length >= _maxBytes) File.Move(Path, Path + ".old", overwrite: true);
                File.AppendAllText(Path, $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} {line}{Environment.NewLine}", Encoding.UTF8);
            }

            return true;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or System.Security.SecurityException)
        {
            return false;
        }
    }

    /// <summary>Appends a line to the shared log.</summary>
    public static void Line(string line) => Shared.Write(line);

    /// <summary>
    /// Makes every <c>Console.Error</c> and <c>Console.Out</c> line also go to the shared log (the original writers keep working), so
    /// the existing diagnostics need no change. Safe to call once at start-up.
    /// </summary>
    public static void Install()
    {
        try
        {
            Console.SetError(new TeeWriter(Console.Error, Shared, "ERR "));
            Console.SetOut(new TeeWriter(Console.Out, Shared, ""));
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            // No console to wrap: the log simply stays empty.
        }
    }

    // Collects characters into lines, logs each finished line and forwards everything to the writer it wraps.
    private sealed class TeeWriter : TextWriter
    {
        private readonly TextWriter _inner;
        private readonly AppLog _log;
        private readonly string _prefix;
        private readonly StringBuilder _line = new();

        public TeeWriter(TextWriter inner, AppLog log, string prefix)
        {
            _inner = inner;
            _log = log;
            _prefix = prefix;
        }

        public override Encoding Encoding => _inner.Encoding;

        public override void Write(char value)
        {
            try { _inner.Write(value); }
            catch (Exception e) when (e is IOException or ObjectDisposedException) { /* the console is gone (WinExe): the log still gets the line */ }
            if (value == '\n')
            {
                Flush(_line);
            }
            else if (value != '\r')
            {
                _line.Append(value);
            }
        }

        public override void Write(string? value)
        {
            if (value is null) return;
            foreach (char c in value) Write(c);
        }

        private void Flush(StringBuilder line)
        {
            if (line.Length > 0) _log.Write(_prefix + line);
            line.Clear();
        }
    }
}
