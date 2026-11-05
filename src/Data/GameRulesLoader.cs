// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Sim.Core;
using Rts.Sim.Rules;
using Tomlyn.Model;

namespace Rts.Data;

/// <summary>Parses the global constants in game.toml.</summary>
internal static class GameRulesLoader
{
    private const string Section = "game";
    private const string EconomySection = "economy";
    private const string TunnelsSection = "tunnels";
    private const string RadiationSection = "radiation";
    private const string StealthSection = "stealth";
    private const string FactionSection = "faction";
    private const string GarrisonSection = "garrison";   // M4-01
    private const string FoamSection = "foam";           // M4-01

    private static readonly string[] TopLevel =
        [Section, EconomySection, TunnelsSection, RadiationSection, StealthSection, FactionSection, GarrisonSection, FoamSection];

    /// <summary>Parses the [game] table. Returns null when any problem was recorded.</summary>
    public static GameRules? Parse(TomlTable root, string file, List<RulesProblem> problems)
    {
        foreach (var kv in root)
        {
            if (Array.IndexOf(TopLevel, kv.Key) < 0)
            {
                problems.Add(new RulesProblem(file, kv.Key, "unknown key"));
            }
        }
        if (!root.TryGetValue(Section, out object? section) || section is null)
        {
            problems.Add(new RulesProblem(file, Section, "missing required [game] table"));
            return null;
        }
        if (section is not TomlTable table)
        {
            problems.Add(new RulesProblem(file, Section, "expected a [game] table"));
            return null;
        }

        var f = new RulesFields(RulesEntry.FromTable(Section, file, table), problems);
        f.CheckKeys("tick_rate", "default_map_width", "default_map_height");
        int? tickRate = f.Int("tick_rate", 1, int.MaxValue);
        if (tickRate is { } tr && tr != SimConstants.TicksPerSecond)
        {
            problems.Add(new RulesProblem(file, "game.tick_rate",
                $"must be {SimConstants.TicksPerSecond} (the sim tick rate), got {tr}"));
        }
        int? width = f.Int("default_map_width", 1, SimConstants.MaxMapCells);
        int? height = f.Int("default_map_height", 1, SimConstants.MaxMapCells);
        if (tickRate is null || width is null || height is null)
        {
            return null;
        }
        return new GameRules(tickRate.Value, width.Value, height.Value);
    }

    /// <summary>Parses the [economy] table (required). Returns null when any problem was recorded.
    /// Unknown top-level keys are reported by <see cref="Parse"/>.</summary>
    public static EconomyRules? ParseEconomy(TomlTable root, string file, List<RulesProblem> problems)
    {
        if (!root.TryGetValue(EconomySection, out object? section) || section is null)
        {
            problems.Add(new RulesProblem(file, EconomySection, "missing required [economy] table"));
            return null;
        }
        if (section is not TomlTable table)
        {
            problems.Add(new RulesProblem(file, EconomySection, "expected an [economy] table"));
            return null;
        }

        var f = new RulesFields(RulesEntry.FromTable(EconomySection, file, table), problems);
        f.CheckKeys("starting_cash", "credits_ashfall", "credits_pitchblende", "harvest_ticks_per_unit",
            "unload_ticks_per_unit", "regrowth_interval_ticks", "sell_refund_permille", "repair_cost_permille",
            "repair_hp_per_tick", "build_radius", "max_queue", "build_ticks_per_100_credits",
            "low_power_slowdown_permille",
            // schema v5 (M6-02)
            "wreck_ticks", "salvage_percent", "wreck_min_value", "wreck_max_value", "plunder_percent", "plunder_cap", "spoils_permille");
        EconomyRules d = EconomyRules.Default;
        int? wreckTicks = f.OptInt("wreck_ticks", 0, 1_000_000, d.WreckTicks);
        int? salvagePercent = f.OptInt("salvage_percent", 0, 1000, d.SalvagePercent);
        int? wreckMin = f.OptInt("wreck_min_value", 0, 1_000_000, d.WreckMinValue);
        int? wreckMax = f.OptInt("wreck_max_value", 0, 1_000_000, d.WreckMaxValue);
        int? plunderPercent = f.OptInt("plunder_percent", 0, 100, d.PlunderPercent);
        int? plunderCap = f.OptInt("plunder_cap", 0, 10_000_000, d.PlunderCap);
        int? spoils = f.OptInt("spoils_permille", 0, 1000, d.SpoilsPermille);
        int? startingCash = f.Int("starting_cash", 0, 10_000_000);
        int? ashfall = f.Int("credits_ashfall", 0, 100_000);
        int? pitchblende = f.Int("credits_pitchblende", 0, 100_000);
        int? harvestTicks = f.Int("harvest_ticks_per_unit", 1, 10_000);
        int? unloadTicks = f.Int("unload_ticks_per_unit", 1, 10_000);
        int? regrowth = f.Int("regrowth_interval_ticks", 0, 1_000_000);
        int? sell = f.Int("sell_refund_permille", 0, 1000);
        int? repairCost = f.Int("repair_cost_permille", 0, 10_000);
        int? repairHp = f.Int("repair_hp_per_tick", 1, 10_000);
        int? radius = f.Int("build_radius", 0, SimConstants.MaxMapCells);
        int? maxQueue = f.Int("max_queue", 1, 99);
        int? ticksPer100 = f.Int("build_ticks_per_100_credits", 1, 100_000);
        int[]? slowdown = f.IntArray("low_power_slowdown_permille", 3, 1000, 100_000);
        if (startingCash is null || ashfall is null || pitchblende is null || harvestTicks is null
            || unloadTicks is null || regrowth is null || sell is null || repairCost is null || repairHp is null
            || radius is null || maxQueue is null || ticksPer100 is null || slowdown is null
            || wreckTicks is null || salvagePercent is null || wreckMin is null || wreckMax is null || plunderPercent is null
            || plunderCap is null || spoils is null)
        {
            return null;
        }
        if (wreckMin.Value > wreckMax.Value)
        {
            f.Problem("wreck_min_value", $"must not exceed wreck_max_value ({wreckMax.Value}), got {wreckMin.Value}");
            return null;
        }
        return new EconomyRules(startingCash.Value, ashfall.Value, pitchblende.Value, harvestTicks.Value,
            unloadTicks.Value, regrowth.Value, sell.Value, repairCost.Value, repairHp.Value, radius.Value,
            maxQueue.Value, ticksPer100.Value, slowdown)
        {
            WreckTicks = wreckTicks.Value, SalvagePercent = salvagePercent.Value, WreckMinValue = wreckMin.Value,
            WreckMaxValue = wreckMax.Value, PlunderPercent = plunderPercent.Value, PlunderCap = plunderCap.Value,
            SpoilsPermille = spoils.Value,
        };
    }

    /// <summary>Reads an optional sub-table of game.toml as fields; null when absent (or not a table, after a recorded problem).</summary>
    private static RulesFields? OptionalTable(TomlTable root, string name, string file, List<RulesProblem> problems)
    {
        if (!root.TryGetValue(name, out object? section) || section is null)
        {
            return null;
        }
        if (section is not TomlTable table)
        {
            problems.Add(new RulesProblem(file, name, $"expected a [{name}] table"));
            return null;
        }
        return new RulesFields(RulesEntry.FromTable(name, file, table), problems);
    }

    /// <summary>Parses the optional [tunnels] table (M3); every key optional with the design 05 default. Null after a problem.</summary>
    public static TunnelRules? ParseTunnels(TomlTable root, string file, List<RulesProblem> problems)
    {
        TunnelRules d = TunnelRules.Default;
        RulesFields? f = OptionalTable(root, TunnelsSection, file, problems);
        if (f is null)
        {
            return root.ContainsKey(TunnelsSection) ? null : d;
        }
        f.CheckKeys("transit_ticks", "network_cap_points", "points_s", "points_m", "dive_ticks", "telegraph_ticks",
            "entrance_telegraph_ticks", "emerging_health_permille", "surfacing_ring");
        int? transit = f.OptInt("transit_ticks", 0, 100_000, d.TransitTicks);
        int? cap = f.OptInt("network_cap_points", 1, 10_000, d.NetworkCapPoints);
        int? ps = f.OptInt("points_s", 1, 1000, d.PointsS);
        int? pm = f.OptInt("points_m", 1, 1000, d.PointsM);
        int? dive = f.OptInt("dive_ticks", 0, 100_000, d.DiveTicks);
        int? tel = f.OptInt("telegraph_ticks", 0, 100_000, d.TelegraphTicks);
        int? etel = f.OptInt("entrance_telegraph_ticks", 0, 100_000, d.EntranceTelegraphTicks);
        int? emerge = f.OptInt("emerging_health_permille", 1, 1000, d.EmergingHealthPermille);
        int? ring = f.OptInt("surfacing_ring", 0, 16, d.SurfacingRing);
        if (transit is null || cap is null || ps is null || pm is null || dive is null || tel is null || etel is null
            || emerge is null || ring is null)
        {
            return null;
        }
        return new TunnelRules(transit.Value, cap.Value, ps.Value, pm.Value, dive.Value, tel.Value, etel.Value, emerge.Value, ring.Value);
    }

    /// <summary>Parses the optional [radiation] table (M3); every key optional with its default. Null after a problem.</summary>
    public static RadiationRules? ParseRadiation(TomlTable root, string file, List<RulesProblem> problems)
    {
        RadiationRules d = RadiationRules.Default;
        RulesFields? f = OptionalTable(root, RadiationSection, file, problems);
        if (f is null)
        {
            return root.ContainsKey(RadiationSection) ? null : d;
        }
        f.CheckKeys("decay_ticks", "damage_interval_ticks", "damage_threshold", "damage_permille", "no_build_threshold");
        int? decay = f.OptInt("decay_ticks", 1, 1_000_000, d.DecayTicks);
        int? interval = f.OptInt("damage_interval_ticks", 1, 1_000_000, d.DamageIntervalTicks);
        int? threshold = f.OptInt("damage_threshold", 0, 255, d.DamageThreshold);
        int? permille = f.OptInt("damage_permille", 0, 100_000, d.DamagePermille);
        int? noBuild = f.OptInt("no_build_threshold", 0, 256, d.NoBuildThreshold);
        if (decay is null || interval is null || threshold is null || permille is null || noBuild is null)
        {
            return null;
        }
        return new RadiationRules(decay.Value, interval.Value, threshold.Value, permille.Value, noBuild.Value);
    }

    /// <summary>Parses the optional [stealth] table (M3). Null after a problem.</summary>
    public static StealthRules? ParseStealth(TomlTable root, string file, List<RulesProblem> problems)
    {
        RulesFields? f = OptionalTable(root, StealthSection, file, problems);
        if (f is null)
        {
            return root.ContainsKey(StealthSection) ? null : StealthRules.Default;
        }
        f.CheckKeys("delay_ticks");
        int? delay = f.OptInt("delay_ticks", 0, 1_000_000, StealthRules.Default.DelayTicks);
        return delay is null ? null : new StealthRules(delay.Value);
    }

    /// <summary>Parses the optional [garrison] table (M4); every key optional with the design 08 default. Null after a problem.</summary>
    public static GarrisonRules? ParseGarrison(TomlTable root, string file, List<RulesProblem> problems)
    {
        GarrisonRules d = GarrisonRules.Default;
        RulesFields? f = OptionalTable(root, GarrisonSection, file, problems);
        if (f is null)
        {
            return root.ContainsKey(GarrisonSection) ? null : d;
        }
        f.CheckKeys("eject_damage_permille", "enter_range_cells");
        int? eject = f.OptInt("eject_damage_permille", 0, 1000, d.EjectDamagePermille);
        int? range = f.OptInt("enter_range_cells", 0, 16, d.EnterRangeCells);
        return eject is null || range is null ? null : new GarrisonRules(eject.Value, range.Value);
    }

    /// <summary>Parses the optional [foam] table (M4); every key optional with the design 08 default. Null after a problem.</summary>
    public static FoamRules? ParseFoam(TomlTable root, string file, List<RulesProblem> problems)
    {
        RulesFields? f = OptionalTable(root, FoamSection, file, problems);
        if (f is null)
        {
            return root.ContainsKey(FoamSection) ? null : FoamRules.Default;
        }
        f.CheckKeys("speed_permille");
        int? speed = f.OptInt("speed_permille", 0, 1000, FoamRules.Default.SpeedPermille);
        return speed is null ? null : new FoamRules(speed.Value);
    }

    /// <summary>
    /// Parses the optional [faction.authority] / [faction.directorate] start kits (M3): <c>conyard</c> (building id) and
    /// <c>units</c> (vehicle/infantry ids). An absent faction keeps its default kit. Ids are NOT checked here: they are
    /// validated when a player of that faction is set up (<see cref="SimRules.ResolveStartKit"/>). Null after a problem.
    /// </summary>
    public static IReadOnlyList<FactionStartKit>? ParseStartKits(TomlTable root, string file, List<RulesProblem> problems)
    {
        // M6-08: the table grows to RuleCounts.Factions entries (neutral placeholder at 2) only when a [faction.left_behind] kit is given,
        // so rules sets without that faction keep exactly the two-kit table (and their hashes).
        bool withLb = root.TryGetValue(FactionSection, out object? sec0) && sec0 is TomlTable t0 && t0.ContainsKey("left_behind");
        FactionStartKit[] kits = withLb
            ? new[] { FactionStartKit.AuthorityDefault, FactionStartKit.DirectorateDefault, FactionStartKit.NeutralNone, FactionStartKit.LeftBehindDefault }
            : new[] { FactionStartKit.AuthorityDefault, FactionStartKit.DirectorateDefault };
        if (!root.TryGetValue(FactionSection, out object? section) || section is null)
        {
            return kits;
        }
        if (section is not TomlTable table)
        {
            problems.Add(new RulesProblem(file, FactionSection, "expected [faction.<name>] tables"));
            return null;
        }
        bool ok = true;
        foreach (var kv in table)
        {
            string key = FactionSection + "." + kv.Key;
            Faction? faction = RulesEnums.ParseFaction(kv.Key);
            if (faction == Faction.Neutral)
            {
                problems.Add(new RulesProblem(file, key, "neutral has no start kit"));
                ok = false;
                continue;
            }
            if (faction is null)
            {
                problems.Add(new RulesProblem(file, key, $"unknown faction; must be one of {RulesEnums.Join(RulesEnums.FactionKeys)}"));
                ok = false;
                continue;
            }
            if (kv.Value is not TomlTable kitTable)
            {
                problems.Add(new RulesProblem(file, key, "expected a table"));
                ok = false;
                continue;
            }
            var f = new RulesFields(RulesEntry.FromTable(key, file, kitTable), problems);
            f.CheckKeys("conyard", "units", "tier2", "tier3");
            FactionStartKit d = kits[(int)faction.Value];
            string? conyard = f.OptStr("conyard", d.Conyard);
            List<string>? units = f.Has("units") ? f.StrList("units") : new List<string>(d.Units);
            string? tier2 = f.OptStr("tier2", d.Tier2Provides);   // M4-01: provides names, checked by RulesLoader
            string? tier3 = f.OptStr("tier3", d.Tier3Provides);
            // M6-08: an empty conyard is legal for a kit that starts with a packed rig vehicle (checked when a player of the faction is set up)
            if (conyard is null || units is null || tier2 is null || tier3 is null)
            {
                ok = false;
                continue;
            }
            kits[(int)faction.Value] = new FactionStartKit(conyard, units) { Tier2Provides = tier2, Tier3Provides = tier3 };
        }
        return ok ? kits : null;
    }
}
