// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Sim.Rules;

namespace Rts.Game.Profile;

/// <summary>M10-19: the text lines of the Profile page, with no graphics.</summary>
public static class ProfileModel
{
    /// <summary>The page lines: campaign medals, then skirmish records.</summary>
    public static IReadOnlyList<string> Lines(PlayerProfile profile)
    {
        var lines = new List<string> { "Campaign medals" };
        if (profile.Missions.Count == 0) lines.Add("  None yet. Win a mission to earn the first.");
        foreach ((string id, MissionRecord r) in profile.Missions)
        {
            string medals = MedalRules.Names(r).Count == 0 ? "no medals" : string.Join(", ", MedalRules.Names(r));
            lines.Add("  " + id + ": " + medals + (r.BestSeconds > 0 ? "   best " + Clock(r.BestSeconds) : ""));
        }

        lines.Add("");
        lines.Add("Skirmish records");
        lines.Add("  Fastest win: " + (profile.FastestWinSeconds > 0 ? Clock(profile.FastestWinSeconds) : "none yet"));
        lines.Add("  Biggest army: " + (profile.BiggestArmy > 0 ? profile.BiggestArmy.ToString() : "none yet"));
        lines.Add("  By faction (wins / losses)");
        if (profile.ByFaction.Count == 0) lines.Add("    none yet");
        foreach ((Faction f, SkirmishTally t) in profile.ByFaction) lines.Add($"    {f}: {t.Wins} / {t.Losses}");
        lines.Add("  By AI tier (wins / losses)");
        if (profile.ByAiTier.Count == 0) lines.Add("    none yet");
        foreach ((int tier, SkirmishTally t) in profile.ByAiTier) lines.Add($"    Tier {tier}: {t.Wins} / {t.Losses}");
        return lines;
    }

    /// <summary>A time in seconds as m:ss.</summary>
    public static string Clock(int seconds) => $"{seconds / 60}:{seconds % 60:D2}";
}
