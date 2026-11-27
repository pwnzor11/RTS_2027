// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Game.Screens;
using Rts.Sim.Rules;

namespace Rts.Game.Profile;

/// <summary>M10-19: loads, updates and saves the profile when a match ends. Never throws; a failed write is ignored (the profile is a bonus, not progress).</summary>
public static class ProfileRecorder
{
    /// <summary>The profile file for these services: beside the saves when a test set <see cref="GameServices.SaveRoot"/>, otherwise the default path.</summary>
    public static string PathFor(GameServices s) =>
        s.SaveRoot is { } root ? Path.Combine(root, "profile.toml") : PlayerProfile.DefaultPath;

    /// <summary>Records a won campaign mission and returns the medals that are new, for the debrief.</summary>
    /// <param name="parSeconds">The mission's par time (D-289), or 0 when it has none.</param>
    public static IReadOnlyList<string> RecordMissionWin(string path, string missionId, int ticks, int ownUnitsLost, int parSeconds = 0)
    {
        PlayerProfile profile = PlayerProfile.Load(path);
        MissionRecord win = MedalRules.ForWin(ticks, ownUnitsLost);
        MissionRecord before = profile.Missions.TryGetValue(missionId, out MissionRecord old) ? old : default;
        profile.WithMission(missionId, win).Save(path);
        return MedalRules.NewNames(before, win, parSeconds);
    }

    /// <summary>S47 U-12: merges a won drill run into the profile (atomic write, tolerant of a missing or old file) and returns the stored record.</summary>
    public static DrillRecord RecordDrill(string path, string drillId, Rts.Scripting.DrillRun run, Rts.Scripting.DrillMedal medal)
    {
        PlayerProfile profile = PlayerProfile.Load(path).WithDrill(drillId, new DrillRecord((int)medal, run.Seconds, run.Lost));
        profile.Save(path);
        return profile.Drills.TryGetValue(drillId, out DrillRecord r) ? r : default;
    }

    /// <summary>Records a finished skirmish against the AI.</summary>
    public static void RecordSkirmish(string path, Faction faction, int aiTier, bool won, int ticks, int armyValue) =>
        PlayerProfile.Load(path).WithSkirmish(faction, aiTier, won, ticks / Rts.Sim.Core.SimConstants.TicksPerSecond, armyValue).Save(path);
}
