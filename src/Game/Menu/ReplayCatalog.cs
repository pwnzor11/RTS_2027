// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Sim.Replay;

namespace Rts.Game.Menu;

/// <summary>One replay file found on disk.</summary>
/// <param name="Path">Full path.</param>
/// <param name="Name">File name.</param>
/// <param name="Modified">Last write time (UTC).</param>
/// <param name="Summary">Map, players, length and seed from the replay header, or why it cannot be read.</param>
/// <param name="Readable">False when the header could not be read.</param>
public sealed record ReplayEntry(string Path, string Name, DateTime Modified, string Summary, bool Readable)
{
    /// <summary>The command that checks this replay (the game has no playback viewer yet).</summary>
    public string VerifyCommand => $"dotnet run --project src/Tools -- replay verify \"{Path}\"";
}

/// <summary>Lists the <c>.rpl</c> files the game records (sandbox and skirmish sessions) and reads their headers.</summary>
public sealed class ReplayCatalog
{
    private const int MaxListed = 60;
    private readonly string _folder;

    /// <summary>Creates a catalog over a folder.</summary>
    public ReplayCatalog(string folder) => _folder = folder;

    /// <summary>The folder the game records into: <c>replays</c> in the working directory (see ReplayRecording).</summary>
    public static string DefaultFolder => System.IO.Path.GetFullPath("replays");

    /// <summary>Replays, newest first (at most 60). Never throws.</summary>
    public IReadOnlyList<ReplayEntry> List()
    {
        var list = new List<ReplayEntry>();
        try
        {
            if (!Directory.Exists(_folder)) return list;
            var files = new DirectoryInfo(_folder).EnumerateFiles("*.rpl").OrderByDescending(f => f.LastWriteTimeUtc).Take(MaxListed);
            foreach (FileInfo f in files)
            {
                (string summary, bool ok) = Describe(f.FullName);
                list.Add(new ReplayEntry(f.FullName, f.Name, f.LastWriteTimeUtc, summary, ok));
            }
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            // Same as an empty folder.
        }
        return list;
    }

    private static (string Summary, bool Ok) Describe(string file)
    {
        try
        {
            using var stream = File.OpenRead(file);
            ReplayData d = ReplayReader.Read(stream);
            double minutes = d.FinalTick / (double)Rts.Sim.Core.SimConstants.TicksPerSecond / 60.0;
            string state = d.Truncated ? ", unfinished" : "";
            return (FormattableString.Invariant($"{d.Header.MapName}, {d.PlayerCount} players, {minutes:0.0} min, seed {d.Seed}{state}"), true);
        }
        catch (Exception e) when (e is InvalidDataException or EndOfStreamException or IOException or UnauthorizedAccessException)
        {
            return (e is InvalidDataException ? e.Message : "damaged or unreadable", false);
        }
    }
}
