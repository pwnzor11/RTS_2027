// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Sim.Core;
using Rts.Sim.Rules;

namespace Rts.Data;

/// <summary>Parses warhead and weapon entries (warheads.toml, weapons.toml).</summary>
internal static class WeaponRulesLoader
{
    /// <summary>Largest allowed damage percent in a warhead's versus table.</summary>
    public const int MaxVersusPercent = 1000;

    private static readonly string[] WarheadKeys =
        ["versus", "splash", "hits_allies", "contaminate", "decontaminate", "bonus_vs_irradiated_percent", "blackout_ticks",
         // schema v4 (M4-01)
         "hits_burrowed", "bonus_vs_entrance_percent", "foam_ticks", "garrison_damage_percent",
         // schema v5 (M6-02)
         "storm_radius", "storm_ticks", "storm_contaminate_step", "storm_contaminate_cap", "storm_speed_permille",
         // schema v8 (M7-12)
         "salvo_missiles", "salvo_ticks", "salvo_radius", "salvo_damage", "salvo_warn_ticks", "salvo_splash"];
    private static readonly string[] WeaponKeys =
        ["damage", "range", "min_range", "rate_of_fire", "projectile_speed", "warhead", "targets"];

    /// <summary>The armour classes that existed before M7-02 (unarmored..fortified); a shorter <c>versus</c> list of at least this length is padded.</summary>
    private const int LegacyArmorClasses = 5;

    /// <summary>Reads <c>versus</c>: <see cref="RuleCounts.ArmorClasses"/> entries, or 5 to 7 entries with the missing (ship and submerged)
    /// classes padded with 100 percent (M7-02).</summary>
    private static int[]? ParseVersus(RulesFields f)
    {
        int len = f.ArrayLength("versus");
        if (len < LegacyArmorClasses || len >= RuleCounts.ArmorClasses) return f.IntArray("versus", RuleCounts.ArmorClasses, 0, MaxVersusPercent);
        int[]? old = f.IntArray("versus", len, 0, MaxVersusPercent);
        if (old is null) return null;
        var full = new int[RuleCounts.ArmorClasses];
        Array.Fill(full, 100);
        old.CopyTo(full, 0);
        return full;
    }

    /// <summary>Parses one warhead.</summary>
    public static WarheadType? ParseWarhead(RulesFields f)
    {
        f.CheckKeys(WarheadKeys);
        int[]? versus = ParseVersus(f);
        int? splash = f.Int("splash", 0, 1);
        bool? hitsAllies = f.Bool("hits_allies");
        int? contaminate = f.OptInt("contaminate", 0, 255, 0);
        int? decontaminate = f.OptInt("decontaminate", 0, 255, 0);
        int? bonus = f.OptInt("bonus_vs_irradiated_percent", 0, MaxVersusPercent, 100);
        int? blackout = f.OptInt("blackout_ticks", 0, 1_000_000, 0);
        bool? hitsBurrowed = f.OptBool("hits_burrowed", false);
        int? bonusEntrance = f.OptInt("bonus_vs_entrance_percent", 0, MaxVersusPercent, 100);
        int? foam = f.OptInt("foam_ticks", 0, 1_000_000, 0);
        int? garrisonDamage = f.OptInt("garrison_damage_percent", 0, MaxVersusPercent, 0);
        int? stormRadius = f.OptInt("storm_radius", 0, SimConstants.MaxMapCells, 0);   // M6-02
        int? stormTicks = f.OptInt("storm_ticks", 0, 1_000_000, 0);
        int? stormStep = f.OptInt("storm_contaminate_step", 0, 255, 0);
        int? stormCap = f.OptInt("storm_contaminate_cap", 0, 255, 64);
        int? stormSpeed = f.OptInt("storm_speed_permille", 0, 1000, 0);
        int? salvoMissiles = f.OptInt("salvo_missiles", 0, 1000, 0);   // M7-12
        int? salvoTicks = f.OptInt("salvo_ticks", 0, 1_000_000, 0);
        int? salvoRadius = f.OptInt("salvo_radius", 0, SimConstants.MaxMapCells, 0);
        int? salvoDamage = f.OptInt("salvo_damage", 0, int.MaxValue, 0);
        int? salvoWarn = f.OptInt("salvo_warn_ticks", 0, 1_000_000, 0);
        int? salvoSplash = f.OptInt("salvo_splash", 0, SimConstants.MaxMapCells, 1);
        if (salvoMissiles is null || salvoTicks is null || salvoRadius is null || salvoDamage is null || salvoWarn is null || salvoSplash is null) return null;
        if (stormRadius is null || stormTicks is null || stormStep is null || stormCap is null || stormSpeed is null) return null;
        if (versus is null || splash is null || hitsAllies is null || contaminate is null || decontaminate is null
            || bonus is null || blackout is null || hitsBurrowed is null || bonusEntrance is null || foam is null || garrisonDamage is null)
        {
            return null;
        }
        return new WarheadType(f.Id, versus, splash.Value, hitsAllies.Value)
        {
            Contaminate = contaminate.Value,
            Decontaminate = decontaminate.Value,
            BonusVsIrradiatedPercent = bonus.Value,
            BlackoutTicks = blackout.Value,
            HitsBurrowed = hitsBurrowed.Value,
            BonusVsEntrancePercent = bonusEntrance.Value,
            FoamTicks = foam.Value,
            GarrisonDamagePercent = garrisonDamage.Value,
            StormRadius = stormRadius.Value,
            StormTicks = stormTicks.Value,
            StormContaminateStep = stormStep.Value,
            StormContaminateCap = stormCap.Value,
            StormSpeedPermille = stormSpeed.Value,
            SalvoMissiles = salvoMissiles.Value,
            SalvoTicks = salvoTicks.Value,
            SalvoRadius = salvoRadius.Value,
            SalvoDamage = salvoDamage.Value,
            SalvoWarnTicks = salvoWarn.Value,
            SalvoSplash = salvoSplash.Value,
        };
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
        WeaponTargets? targets = f.OptChoice("targets", RulesEnums.ParseWeaponTargets, RulesEnums.WeaponTargetKeys, WeaponTargets.Ground);
        if (targets is null) return null;
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
            projectileSpeed.Value, warheadIndex) { Targets = targets.Value };
    }
}
