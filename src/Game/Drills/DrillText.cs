// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Game.Profile;
using Rts.Scripting;

namespace Rts.Game.Drills;

/// <summary>S47 U-12: the short texts of the Drills screens (pure, so they are unit-tested).</summary>
public static class DrillText
{
    /// <summary>The medal name, or "No medal" for <see cref="DrillMedal.None"/>.</summary>
    public static string MedalName(DrillMedal medal) => medal switch
    {
        DrillMedal.Gold => "Gold",
        DrillMedal.Silver => "Silver",
        DrillMedal.Bronze => "Bronze",
        _ => "No medal",
    };

    /// <summary>Seconds as m:ss.</summary>
    public static string Time(int seconds) => $"{seconds / 60}:{seconds % 60:D2}";

    /// <summary>A limit of the drill's metric: "2:30" for time, "3" for losses.</summary>
    public static string Limit(DrillDef def, int value) => def.IsTime ? Time(value) : value.ToString(System.Globalization.CultureInfo.InvariantCulture);

    /// <summary>The limit and unit-keeping rule of one medal, for example "Gold: 0:50 or less, keep 6 units".</summary>
    public static string Threshold(DrillDef def, DrillMedal medal)
    {
        (int limit, int keep) = medal switch
        {
            DrillMedal.Gold => (def.Gold, def.KeepGold),
            DrillMedal.Silver => (def.Silver, def.KeepSilver),
            _ => (def.Bronze, def.KeepBronze),
        };
        string rule = def.IsTime ? Limit(def, limit) + " or less" : limit == 0 ? "none lost" : limit + " or fewer lost";
        if (keep > 0) rule += ", keep " + keep + (keep == 1 ? " unit" : " units");
        return MedalName(medal) + ": " + rule;
    }

    /// <summary>One line with all three thresholds.</summary>
    public static string Thresholds(DrillDef def) =>
        Threshold(def, DrillMedal.Gold) + "   " + Threshold(def, DrillMedal.Silver) + "   " + Threshold(def, DrillMedal.Bronze);

    /// <summary>The best result of a drill for the list: "Gold  1:52" or "Not played".</summary>
    public static string Best(DrillRecord record) =>
        !record.Won ? "Not played" : (record.Medal > 0 ? MedalName(record.BestMedal) : "No medal") + "  " + Time(record.BestSeconds);
}
