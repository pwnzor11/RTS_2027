// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using System.Text.RegularExpressions;

namespace Rts.Net.Diagnostics;

/// <summary>
/// Keeps desync reports bounded (NETLLM-10): <see cref="IsFirstReport"/> lets a session write one report per desync tick (a repeated
/// "Continue" re-reports later ticks, never the same one twice) and <see cref="Prune"/> keeps only the newest report folders.
/// </summary>
public sealed partial class DesyncReportFolders
{
    /// <summary>How many report folders are kept under the report root.</summary>
    public const int DefaultKeep = 10;

    private readonly HashSet<int> _reported = new();

    /// <summary>True the first time <paramref name="tick"/> is seen; false for a repeat.</summary>
    public bool IsFirstReport(int tick) => _reported.Add(tick);

    /// <summary>Deletes all but the newest <paramref name="keep"/> report folders (names like 20261125-101500 or 20261125-101500-2) under <paramref name="root"/>. Never throws.</summary>
    public static void Prune(string root, int keep = DefaultKeep)
    {
        try
        {
            if (!Directory.Exists(root)) return;
            // The folder name starts with the local time stamp, so ordinal order is age order; the "-n" suffix breaks same-second ties.
            var folders = Directory.GetDirectories(root)
                .Where(d => ReportName().IsMatch(Path.GetFileName(d)))
                .OrderBy(d => Path.GetFileName(d)[..15], StringComparer.Ordinal)
                .ThenBy(d => Suffix(Path.GetFileName(d)))
                .ToList();
            for (int i = 0; i < folders.Count - Math.Max(1, keep); i++)
            {
                try { Directory.Delete(folders[i], true); }
                catch (Exception e) when (e is IOException or UnauthorizedAccessException) { }
            }
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or ArgumentException) { }
    }

    private static int Suffix(string name) => name.Length > 16 && int.TryParse(name.AsSpan(16), out int n) ? n : 1;

    [GeneratedRegex(@"^\d{8}-\d{6}(-\d+)?$")]
    private static partial Regex ReportName();
}
