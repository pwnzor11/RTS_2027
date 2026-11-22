// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
namespace Rts.Game.Profile;

/// <summary>M10-19: the rules that turn a finished campaign mission into medals. Pure.</summary>
public static class MedalRules
{
    /// <summary>The record of one winning run: Completed always; No losses when no own unit (vehicle or infantry) died.</summary>
    /// <param name="ticks">Mission length in sim ticks.</param>
    /// <param name="ownUnitsLost">Own vehicles and infantry that were destroyed (the sim's UnitsLost counter for the local player).</param>
    public static MissionRecord ForWin(int ticks, int ownUnitsLost) =>
        new(true, ownUnitsLost <= 0, Math.Max(1, ticks / Rts.Sim.Core.SimConstants.TicksPerSecond));

    /// <summary>The medal names a record shows, in display order. Par time needs the mission's par (0 = the mission has none, D-289).</summary>
    public static IReadOnlyList<string> Names(MissionRecord record, int parSeconds = 0)
    {
        var names = new List<string>();
        if (record.Completed) names.Add("Completed");
        if (record.NoLosses) names.Add("No losses");
        if (record.Completed && parSeconds > 0 && record.BestSeconds > 0 && record.BestSeconds <= parSeconds) names.Add("Par time");
        return names;
    }

    /// <summary>The medals this win earned that the player did not have before (for the debrief).</summary>
    public static IReadOnlyList<string> NewNames(MissionRecord before, MissionRecord win, int parSeconds = 0)
    {
        MissionRecord merged = before.Merge(win);
        return Names(merged, parSeconds).Where(n => !Names(before, parSeconds).Contains(n)).ToList();
    }
}
