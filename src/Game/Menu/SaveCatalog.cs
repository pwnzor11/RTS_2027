// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Sim.Serialization;

namespace Rts.Game.Menu;

/// <summary>Which menu a saved game belongs to. ASSUMED (M3-16): saves live in one folder per kind.</summary>
public enum SaveKind
{
    /// <summary>A campaign save.</summary>
    Campaign,
    /// <summary>A skirmish save.</summary>
    Skirmish,
}

/// <summary>One save file found on disk.</summary>
/// <param name="Kind">Campaign or skirmish (from the folder).</param>
/// <param name="Path">Full path.</param>
/// <param name="Name">File name without extension.</param>
/// <param name="Modified">Last write time (UTC).</param>
/// <param name="Summary">Human line from the save's own header, or why it cannot be read.</param>
/// <param name="Readable">False when the header could not be read (damaged, or written by another build).</param>
public sealed record SaveEntry(SaveKind Kind, string Path, string Name, DateTime Modified, string Summary, bool Readable);

/// <summary>
/// Finds saved games without inventing a format: it lists the files the simulation's own save writer produces
/// (<c>Simulation.Save</c>, magic <c>RTSS</c>) in <c>saves/campaign</c> and <c>saves/skirmish</c> under the settings folder and reads each
/// header with <c>SimReader.ReadHeader</c>. The game does not write such files yet and cannot restore one (see
/// <see cref="CannotResumeReason"/>), so in practice the list is empty and Continue is disabled.
/// </summary>
public sealed class SaveCatalog
{
    /// <summary>Extension of save files.</summary>
    public const string Extension = ".rtss";

    /// <summary>Why a chosen save cannot be started yet. Shown to the player instead of failing silently.</summary>
    public const string CannotResumeReason =
        "Loading saved games is not wired into the game yet (no in-game Save, and no way to rebuild a skirmish or mission around a loaded simulation).";

    // ASSUMED (M3-16): saves are kept as saves/campaign/*.rtss and saves/skirmish/*.rtss beside settings.toml. The game writes none yet.
    private readonly string _root;

    /// <summary>Creates a catalog over <paramref name="root"/> (the folder that holds the campaign and skirmish sub-folders).</summary>
    public SaveCatalog(string root) => _root = root;

    /// <summary>The default folder: <c>saves</c> next to the settings file.</summary>
    public static string DefaultRoot => System.IO.Path.Combine(System.IO.Path.GetDirectoryName(Settings.DefaultPath) ?? ".", "saves");

    /// <summary>The folder for one kind of save.</summary>
    public string FolderOf(SaveKind kind) => System.IO.Path.Combine(_root, kind == SaveKind.Campaign ? "campaign" : "skirmish");

    /// <summary>Saves of one kind, newest first. Never throws.</summary>
    public IReadOnlyList<SaveEntry> List(SaveKind kind)
    {
        var list = new List<SaveEntry>();
        try
        {
            string dir = FolderOf(kind);
            if (!Directory.Exists(dir)) return list;
            foreach (string file in Directory.EnumerateFiles(dir, "*" + Extension))
            {
                var info = new FileInfo(file);
                (string summary, bool ok) = Describe(file);
                list.Add(new SaveEntry(kind, file, System.IO.Path.GetFileNameWithoutExtension(file), info.LastWriteTimeUtc, summary, ok));
            }
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            // An unreadable folder is the same as no saves.
        }
        return list.OrderByDescending(s => s.Modified).ThenBy(s => s.Name, StringComparer.Ordinal).ToList();
    }

    /// <summary>The most recent readable save of either kind, or null.</summary>
    public SaveEntry? Newest() =>
        List(SaveKind.Campaign).Concat(List(SaveKind.Skirmish)).Where(s => s.Readable).OrderByDescending(s => s.Modified).FirstOrDefault();

    private static (string Summary, bool Ok) Describe(string file)
    {
        try
        {
            using var stream = File.OpenRead(file);
            using var r = new BinaryReader(stream);
            Rts.Sim.Serialization.SetupHeader h = SimReader.ReadHeader(r);
            return ($"{h.MapName}, {h.PlayerCount} players, build {h.Build}", true);
        }
        catch (Exception e) when (e is InvalidDataException or EndOfStreamException or IOException or UnauthorizedAccessException)
        {
            return (e is InvalidDataException ? e.Message : "damaged or unreadable", false);
        }
    }
}
