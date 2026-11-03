using Rts.Sim.Rules;

namespace Rts.Data;

/// <summary>Parses warhead and weapon entries (warheads.toml, weapons.toml).</summary>
internal static class WeaponRulesLoader
{
    /// <summary>Largest allowed damage percent in a warhead's versus table.</summary>
    public const int MaxVersusPercent = 1000;

    private static readonly string[] WarheadKeys = ["versus", "splash", "hits_allies"];
    private static readonly string[] WeaponKeys =
        ["damage", "range", "min_range", "rate_of_fire", "projectile_speed", "warhead"];

    /// <summary>Parses one warhead.</summary>
    public static WarheadType? ParseWarhead(RulesFields f)
    {
        f.CheckKeys(WarheadKeys);
        int[]? versus = f.IntArray("versus", RuleCounts.ArmorClasses, 0, MaxVersusPercent);
        int? splash = f.Int("splash", 0, 1);
        bool? hitsAllies = f.Bool("hits_allies");
        if (versus is null || splash is null || hitsAllies is null)
        {
            return null;
        }
        return new WarheadType(f.Id, versus, splash.Value, hitsAllies.Value);
    }

    /// <summary>Parses one weapon. <paramref name="warheads"/> must already be sorted by id.</summary>
    public static WeaponType? ParseWeapon(RulesFields f, IReadOnlyList<WarheadType> warheads)
    {
        f.CheckKeys(WeaponKeys);
        int? damage = f.Int("damage", 0, int.MaxValue);
        int? range = f.Int("range", 0, int.MaxValue);
        int? minRange = f.Int("min_range", 0, int.MaxValue);
        int? rateOfFire = f.Int("rate_of_fire", 1, int.MaxValue);
        int? projectileSpeed = f.Int("projectile_speed", 0, int.MaxValue);
        string? warhead = f.Str("warhead");
        if (damage is null || range is null || minRange is null || rateOfFire is null || projectileSpeed is null
            || warhead is null)
        {
            return null;
        }
        if (range.Value <= minRange.Value)
        {
            f.Problem("range", $"must be greater than min_range ({minRange.Value}), got {range.Value}");
            return null;
        }
        int warheadIndex = SimRules.IndexOf(warheads, warhead, w => w.Id);
        if (warheadIndex < 0)
        {
            f.Problem("warhead", $"unknown warhead '{warhead}'");
            return null;
        }
        return new WeaponType(f.Id, damage.Value, range.Value, minRange.Value, rateOfFire.Value,
            projectileSpeed.Value, warheadIndex);
    }
}
