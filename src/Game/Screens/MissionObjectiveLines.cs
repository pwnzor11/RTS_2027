// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Scripting;
using Rts.Sim.Core;

namespace Rts.Game.Screens;

/// <summary>
/// S12: the text of the mission objectives panel. Visible objectives only (hidden ones left out), then running timers as "label m:ss".
/// Labels that are keys in the mission's locale show the locale text (<see cref="MissionHost.Label"/>). Display only.
/// </summary>
public static class MissionObjectiveLines
{
    /// <summary>The panel lines for the frame's sim <paramref name="tick"/>, starting with the "OBJECTIVES" header.</summary>
    public static List<string> Build(MissionHost host, int tick)
    {
        ArgumentNullException.ThrowIfNull(host);
        var lines = new List<string> { "OBJECTIVES" };
        foreach (MissionObjective o in host.VisibleObjectives)
        {
            string mark = o.Status switch { ObjectiveStatus.Completed => "[x] ", ObjectiveStatus.Failed => "[!] ", _ => "[ ] " };
            lines.Add(mark + (o.Primary ? "" : "(optional) ") + host.Label(o.Text));
        }

        foreach (MissionTimer t in host.Timers)
        {
            int left = t.Paused ? t.EndTick : t.EndTick - tick;   // a paused timer holds its ticks left in EndTick
            if (left <= 0) continue;
            lines.Add(TimerLine(host.Label(t.Text), left, t.Paused));
        }

        return lines;
    }

    /// <summary>A timer line: "label m:ss" from ticks left (rounded up to whole seconds), "(paused)" appended when paused.</summary>
    public static string TimerLine(string label, int ticksLeft, bool paused)
    {
        int tps = SimConstants.TicksPerSecond;
        int seconds = (ticksLeft + tps - 1) / tps;
        return $"{label} {seconds / 60}:{seconds % 60:00}" + (paused ? " (paused)" : "");
    }
}
