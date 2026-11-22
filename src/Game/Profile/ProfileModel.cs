// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Sim.Rules;

namespace Rts.Game.Profile;

/// <summary>M10-19: the text lines of the Profile page, with no graphics.</summary>
public static class ProfileModel
{
    /// <summary>The page lines: campaign medals, then skirmish records.</summary>
    /// <param name="parOf">The par time in seconds of a mission id (0 or unknown = no par); null = no mission has a par.</param>
    public static IReadOnlyList<string> Lines(PlayerProfile profile, Func<string, int>? parOf = null)
    {
        var lines = new List<string> { "Campaign medals" };
        if (profile.Missions.Count == 0) lines.Add("  None yet. Win a mission to earn the first.");
        foreach ((string id, MissionRecord r) in profile.Missions)
        {
            int par = parOf?.Invoke(id) ?? 0;
            IReadOnlyList<string> names = MedalRules.Names(r, par);
            string medals = names.Count == 0 ? "no medals" : string.Join(", ", names);
            var times = new List<string>();
            if (r.BestSeconds > 0) times.Add("best " + Clock(r.BestSeconds));
            if (par > 0) times.Add("par " + Clock(par));
            lines.Add("  " + id + ": " + medals + (times.Count > 0 ? "   " + string.Join(", ", times) : ""));
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
