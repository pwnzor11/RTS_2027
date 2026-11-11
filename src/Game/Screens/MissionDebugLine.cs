// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Scripting;

namespace Rts.Game.Screens;

/// <summary>
/// S19 MIS-07: the one-line script debug note of the mission overlay. A failed script callback used to be visible only in <c>--mission-check</c>; now a
/// halted script, or the latest script error, shows on screen.
/// </summary>
public static class MissionDebugLine
{
    /// <summary>The longest the line gets, in characters.</summary>
    public const int MaxChars = 78;

    /// <summary>The note for this mission, or null when the script is healthy.</summary>
    public static string? Of(MissionHost host)
    {
        if (host.Halted) return Truncate("SCRIPT HALTED" + (host.Errors.Count > 0 ? ": " + host.Errors[^1] : ""));
        return host.Errors.Count > 0 ? Truncate("SCRIPT ERROR (" + host.Errors.Count + "): " + host.Errors[^1]) : null;
    }

    private static string Truncate(string text) => text.Length <= MaxChars ? text : text.Substring(0, MaxChars - 3) + "...";
}
