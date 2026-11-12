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

    // PENDING Q-M10C-1: no par medal. No campaign mission carries a par time yet (data/missions/*/mission.toml and data/campaigns/*.toml
    // have no such value), so the owner has to say what the par times are, or whether to derive them. Add a Par medal here then.

    /// <summary>The medal names a record shows, in display order.</summary>
    public static IReadOnlyList<string> Names(MissionRecord record)
    {
        var names = new List<string>();
        if (record.Completed) names.Add("Completed");
        if (record.NoLosses) names.Add("No losses");
        return names;
    }

    /// <summary>The medals this win earned that the player did not have before (for the debrief).</summary>
    public static IReadOnlyList<string> NewNames(MissionRecord before, MissionRecord win)
    {
        MissionRecord merged = before.Merge(win);
        return Names(merged).Where(n => !Names(before).Contains(n)).ToList();
    }
}
