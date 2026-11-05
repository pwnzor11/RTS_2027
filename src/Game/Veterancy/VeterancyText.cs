// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using System.Globalization;
using Rts.Sim.Rules;

namespace Rts.Game.Veterancy;

/// <summary>
/// Text keys of the veterancy UI (VET-5) with their PLACEHOLDER English text. There is no string table yet (see <c>LeftBehindText</c>,
/// <c>SuperweaponText</c>); when one lands, the keys below are what to register and <see cref="Placeholder"/> is the one place to swap.
/// The bitmap font is ASCII.
/// </summary>
public static class VeterancyText
{
    /// <summary>Rank 1 name (key).</summary>
    public const string VeteranKey = "hud.vet.rank_veteran";

    /// <summary>Rank 2 name (key).</summary>
    public const string EliteKey = "hud.vet.rank_elite";

    /// <summary>Rank 3 name (key).</summary>
    public const string HeroKey = "hud.vet.rank_hero";

    /// <summary>Info line: kill value, the threshold of the next rank and that rank's name. Arguments 0 = kill value, 1 = threshold,
    /// 2 = next rank name (key).</summary>
    public const string NextRankKey = "hud.vet.next_rank";

    /// <summary>Info line at the top rank (key).</summary>
    public const string MaxRankKey = "hud.vet.max_rank";

    /// <summary>Placeholder text for a key, with the arguments filled in. Unknown keys return the key.</summary>
    /// <param name="key">One of the constants above.</param>
    /// <param name="kill">Kill value (for <see cref="NextRankKey"/>).</param>
    /// <param name="threshold">Threshold of the next rank (for <see cref="NextRankKey"/>).</param>
    /// <param name="nextRank">The rank being worked towards, 1..3 (for <see cref="NextRankKey"/>).</param>
    public static string Placeholder(string key, int kill = 0, int threshold = 0, int nextRank = 1) => key switch
    {
        VeteranKey => "Veteran",
        EliteKey => "Elite",
        HeroKey => "Hero",
        NextRankKey => "Kills " + kill.ToString(CultureInfo.InvariantCulture) + " / " + threshold.ToString(CultureInfo.InvariantCulture)
            + " to " + Placeholder(RankNameKey(nextRank) ?? VeteranKey),
        MaxRankKey => "Max rank",
        _ => key,
    };

    /// <summary>
    /// The kill value a unit needs for its next rank (VET-5), from the rules' thresholds scaled like <c>Simulation.RankOf</c>
    /// (<c>max(1, threshold x scale / 1000)</c>). 0 at the top rank, for an out-of-range rank, or when the unit cannot rank.
    /// </summary>
    /// <param name="thresholds">The [veterancy] thresholds, one per rank 1..3.</param>
    /// <param name="rank">The unit's current rank.</param>
    /// <param name="scalePermille">The unit type's veterancy threshold scale (1000 = none).</param>
    /// <param name="canRank">Whether the unit type earns ranks.</param>
    public static int NextThreshold(IReadOnlyList<int> thresholds, int rank, int scalePermille, bool canRank)
    {
        if (!canRank || rank < 0 || rank >= thresholds.Count) return 0;
        return (int)Math.Max(1L, (long)thresholds[rank] * scalePermille / 1000);
    }

    /// <summary>The key of a rank's name, or null for recruits (rank 0) and out-of-range ranks.</summary>
    public static string? RankNameKey(int rank) => rank switch
    {
        1 => VeteranKey,
        2 => EliteKey,
        3 => HeroKey,
        _ => null,
    };

    /// <summary>
    /// The info lines of one vehicle or soldier (own units only; an empty list for anyone else). The rank name first when ranked, then
    /// either "Max rank" or "Next rank: kill/threshold". A unit that cannot rank (threshold 0 and recruit) gets no lines.
    /// </summary>
    /// <param name="own">True only for the viewing player's own unit.</param>
    /// <param name="rank">Rank 0..3.</param>
    /// <param name="kill">Kill value.</param>
    /// <param name="nextThreshold">Kill value the next rank needs (0 at the top rank, or for a unit that cannot rank).</param>
    public static IReadOnlyList<string> InfoLines(bool own, int rank, int kill, int nextThreshold)
    {
        if (!own) return Array.Empty<string>();
        var lines = new List<string>(2);
        string? name = RankNameKey(rank);
        if (name is not null) lines.Add(Placeholder(name));
        if (rank >= VeterancyRules.Ranks) lines.Add(Placeholder(MaxRankKey));
        else if (nextThreshold > 0) lines.Add(Placeholder(NextRankKey, kill, nextThreshold, rank + 1));
        return lines;
    }
}
