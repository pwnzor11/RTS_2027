// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Game.Menu;
using Rts.Sim.Core;

namespace Rts.Game.Saves;

/// <summary>
/// The single-player autosave rules (D-205, S18 GS-06): one save when a match starts and one every N minutes of play, written in turn into
/// <see cref="SlotCount"/> rotating slots ("autosave 1" to "autosave 3"; the oldest, or a missing one, is overwritten). Pure rules, so
/// they are testable; <c>GameScreen</c> does the writing.
/// </summary>
public static class Autosave
{
    /// <summary>Number of rotating autosave slots.</summary>
    public const int SlotCount = 3;

    /// <summary>The autosave interval choices the Settings screen steps through, in minutes (0 = off).</summary>
    public static readonly IReadOnlyList<int> IntervalChoices = new[] { 0, 1, 2, 3, 5, 10, 15, 30, 60 };

    /// <summary>The name of autosave slot <paramref name="number"/> (1..<see cref="SlotCount"/>).</summary>
    public static string SlotName(int number) => "autosave " + number;

    /// <summary>The interval in simulation ticks for <paramref name="minutes"/> of play, or 0 when autosave is off.</summary>
    public static int IntervalTicks(int minutes) => minutes <= 0 ? 0 : minutes * 60 * SimConstants.TicksPerSecond;

    /// <summary>
    /// True when an autosave is due: autosave is on (<paramref name="intervalTicks"/> above 0) and either none has been taken yet
    /// (<paramref name="lastTick"/> below 0, the match has just started) or a full interval of play has passed since <paramref name="lastTick"/>.
    /// </summary>
    public static bool Due(int tick, int lastTick, int intervalTicks) =>
        intervalTicks > 0 && (lastTick < 0 || tick - lastTick >= intervalTicks);

    /// <summary>The next interval choice after <paramref name="minutes"/> in <paramref name="direction"/> (wraps); a value not in the list starts at the first.</summary>
    public static int StepMinutes(int minutes, int direction)
    {
        int i = -1;
        for (int k = 0; k < IntervalChoices.Count; k++)
            if (IntervalChoices[k] == minutes) i = k;
        if (i < 0) return IntervalChoices[0];
        return IntervalChoices[((i + direction) % IntervalChoices.Count + IntervalChoices.Count) % IntervalChoices.Count];
    }

    /// <summary>Settings text for the interval, for example "Off" or "every 5 min".</summary>
    public static string IntervalText(int minutes) => minutes <= 0 ? "Off" : $"every {minutes} min";

    /// <summary>
    /// The slot the next autosave goes to: the first of the <see cref="SlotCount"/> slots that has no file, else the one written longest
    /// ago. <paramref name="existing"/> is the catalog listing of the game's kind.
    /// </summary>
    public static string NextSlot(IReadOnlyList<SaveEntry> existing)
    {
        string? oldest = null;
        DateTime oldestAt = DateTime.MaxValue;
        for (int n = 1; n <= SlotCount; n++)
        {
            string name = SlotName(n);
            SaveEntry? entry = existing.FirstOrDefault(e => e.Name == name);
            if (entry is null) return name;
            if (entry.Modified < oldestAt)
            {
                oldestAt = entry.Modified;
                oldest = name;
            }
        }

        return oldest ?? SlotName(1);
    }
}
