// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using System.Globalization;
using System.Text;

namespace Rts.Game.Lan;

/// <summary>
/// Writes a desync report folder (M9-10, design 20 §4.5): <c>&lt;root&gt;/&lt;yyyyMMdd-HHmmss&gt;/</c> with <c>replay.rpl</c> (the match so far),
/// <c>save.rtsg</c> (this peer's state at the current tick) and <c>subhashes.txt</c> (the named sub-hashes at the desync tick and, when
/// the other side's are known, the first part that differs). The default root is %APPDATA%\RTS_2027\desync. Never throws: a report that
/// cannot be written must not take the match down.
/// </summary>
public static class LanDesyncReport
{
    /// <summary>The default report root: RTS_2027\desync beside the settings file in the user's application-data folder.</summary>
    public static string DefaultRoot => Path.Combine(Path.GetDirectoryName(Settings.DefaultPath) ?? ".", "desync");

    /// <summary>
    /// The sub-hash listing: one line per part (own value, then the other side's when known) and the first part that differs.
    /// </summary>
    /// <param name="tick">The desync tick.</param>
    /// <param name="own">This peer's named sub-hashes at <paramref name="tick"/>, or null when it no longer has them.</param>
    /// <param name="other">The other side's sub-hashes in the same part order, or null when unknown (a client never sees the host's).</param>
    /// <param name="otherName">Who the other side is, for the header line.</param>
    public static string SubHashText(int tick, IReadOnlyList<(string Part, ulong Hash)>? own, IReadOnlyList<ulong>? other, string otherName)
    {
        var sb = new StringBuilder();
        sb.Append(CultureInfo.InvariantCulture, $"desync at tick {tick}\n");
        if (own is null)
        {
            sb.Append("this peer no longer has the sub-hashes of that tick\n");
            return sb.ToString();
        }
        string? first = FirstDifference(own, other);
        sb.Append(other is null ? "first differing part: unknown here (compare with the host's report)\n"
            : first is null ? $"first differing part: none (all parts agree with {otherName})\n"
            : $"first differing part: {first}\n");
        sb.Append(other is null ? "part own\n" : $"part own {otherName}\n");
        for (int i = 0; i < own.Count; i++)
        {
            sb.Append(CultureInfo.InvariantCulture, $"{own[i].Part} {own[i].Hash:x16}");
            if (other is not null) sb.Append(CultureInfo.InvariantCulture, $" {(i < other.Count ? other[i].ToString("x16", CultureInfo.InvariantCulture) : "-")}");
            sb.Append('\n');
        }
        return sb.ToString();
    }

    /// <summary>The name of the first part whose hashes differ, or null when they all agree (or the other side is unknown).</summary>
    public static string? FirstDifference(IReadOnlyList<(string Part, ulong Hash)> own, IReadOnlyList<ulong>? other)
    {
        if (other is null) return null;
        for (int i = 0; i < own.Count; i++)
            if (i >= other.Count || other[i] != own[i].Hash) return own[i].Part;
        return null;
    }

    /// <summary>Writes the folder and returns its path, or null when it could not be written.</summary>
    /// <param name="root">The report root (see <see cref="DefaultRoot"/>).</param>
    /// <param name="stamp">The local date and time, for the folder name.</param>
    /// <param name="replayPath">The replay being recorded (copied as it stands), or null.</param>
    /// <param name="save">The save bytes, or null when none could be made.</param>
    /// <param name="subHashes">The <see cref="SubHashText"/> listing.</param>
    public static string? Write(string root, DateTime stamp, string? replayPath, byte[]? save, string subHashes)
    {
        try
        {
            string name = stamp.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);
            string folder = Path.Combine(root, name);
            for (int n = 2; Directory.Exists(folder); n++) folder = Path.Combine(root, $"{name}-{n}");
            Directory.CreateDirectory(folder);
            File.WriteAllText(Path.Combine(folder, "subhashes.txt"), subHashes);
            if (save is not null) File.WriteAllBytes(Path.Combine(folder, "save.rtsg"), save);
            if (replayPath is not null && File.Exists(replayPath))
            {
                // the recording still has the file open for writing; share it and copy what has been flushed so far
                using var from = new FileStream(replayPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                using var to = new FileStream(Path.Combine(folder, "replay.rpl"), FileMode.Create, FileAccess.Write);
                from.CopyTo(to);
            }
            return folder;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            Console.Error.WriteLine($"error: desync report not written: {e.Message}");
            return null;
        }
    }
}
