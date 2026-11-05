// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Sim.Core;
using Rts.Sim.Rules;

namespace Rts.Data;

/// <summary>Parses vehicle and infantry entries (vehicles.toml, infantry.toml).</summary>
internal static class UnitRulesLoader
{
    private static readonly string[] VehicleKeys =
    [
        "speed", "max_health", "locomotion", "turn_rate", "armor", "sight", "primary_weapon",
        "has_turret", "turret_turn_rate", "crusher", "cost", "prerequisites",
        "harvest_capacity", "build_time",
        // schema v3 (M3-01)
        "faction", "tunnel_size", "aura_radius", "aura_rof_permille", "aura_speed_permille", "aura_targets",
        "rad_resist_permille", "irradiated", "stealth", "death_weapon", "jam_radius", "seismic_radius",
        "passengers", "deploys_into",
        // schema v4 (M4-01)
        "tier",
        // schema v5 (M6-02)
        "unpacks_into", "pack_ticks", "salvage_rate", "raider", "raider_one_use", "drops_blackout_ticks", "aura_repair_permille",
        "tow_bonus_permille",
        // schema v6 (VET-1)
        "veteran",
    ];

    private static readonly string[] InfantryKeys =
    [
        "speed", "max_health", "armor", "sight", "primary_weapon", "cost", "prerequisites", "build_time",
        // schema v3 (M3-01)
        "faction", "tunnel_size", "aura_radius", "aura_rof_permille", "aura_speed_permille", "aura_targets",
        "rad_resist_permille", "irradiated", "stealth", "death_weapon", "jam_radius", "seismic_radius",
        "engineer",
        // schema v4 (M4-01)
        "tier", "can_garrison",
        // schema v5 (M6-02)
        "salvage_rate", "raider", "raider_one_use", "ash_stealth_delay_ticks", "ash_stealth_threshold", "aura_repair_permille",
        // schema v6 (VET-1)
        "veteran",
    ];

    /// <summary>The schema v3 keys vehicles and infantry share (all optional).</summary>
    private sealed record MobileV3(Faction Faction, TunnelSize TunnelSize, int AuraRadius, int AuraRof, int AuraSpeed,
        IReadOnlyList<string> AuraTargets, int RadResist, bool Irradiated, bool Stealth, int DeathWeapon, int JamRadius,
        int SeismicRadius);

    /// <summary>Reads the shared v3 keys; null after a recorded problem. Aura targets are checked as references.</summary>
    private static MobileV3? ParseMobileV3(RulesFields f, IReadOnlyList<WeaponType> weapons, RulesReferences refs,
        TunnelSize defaultSize)
    {
        Faction? faction = f.FactionOrDefault();
        TunnelSize? size = f.OptChoice("tunnel_size", RulesEnums.ParseTunnelSize, RulesEnums.TunnelSizeKeys, defaultSize);
        int? auraRadius = f.OptInt("aura_radius", 0, SimConstants.MaxMapCells, 0);
        int? auraRof = f.OptInt("aura_rof_permille", 1, 100_000, 1000);
        int? auraSpeed = f.OptInt("aura_speed_permille", 1, 100_000, 1000);
        List<string>? auraTargets = f.OptStrList("aura_targets");
        int? radResist = f.OptInt("rad_resist_permille", 0, 1000, 0);
        bool? irradiated = f.OptBool("irradiated", false);
        bool? stealth = f.OptBool("stealth", false);
        int? deathWeapon = f.OptIndex("death_weapon", WeaponIds(weapons), "weapon");
        int? jam = f.OptInt("jam_radius", 0, SimConstants.MaxMapCells, 0);
        int? seismic = f.OptInt("seismic_radius", 0, SimConstants.MaxMapCells, 0);
        if (faction is null || size is null || auraRadius is null || auraRof is null || auraSpeed is null || auraTargets is null
            || radResist is null || irradiated is null || stealth is null || deathWeapon is null || jam is null || seismic is null)
        {
            return null;
        }
        refs.AddAll(f, "aura_targets", auraTargets);
        return new MobileV3(faction.Value, size.Value, auraRadius.Value, auraRof.Value, auraSpeed.Value, auraTargets,
            radResist.Value, irradiated.Value, stealth.Value, deathWeapon.Value, jam.Value, seismic.Value);
    }

    private static IReadOnlyList<string> WeaponIds(IReadOnlyList<WeaponType> weapons)
    {
        var ids = new string[weapons.Count];
        for (int i = 0; i < ids.Length; i++) ids[i] = weapons[i].Id;
        return ids;
    }

    /// <summary>Parses one vehicle. <paramref name="weapons"/> must already be sorted by id; <paramref name="buildingIds"/> is every
    /// building entry id sorted ordinal (a building's index in the final list, used for <c>deploys_into</c>).</summary>
    public static VehicleType? ParseVehicle(RulesFields f, IReadOnlyList<WeaponType> weapons, IReadOnlyList<string> buildingIds,
        RulesReferences refs)
    {
        f.CheckKeys(VehicleKeys);
        int? speed = f.Int("speed", 1, int.MaxValue);
        int? maxHealth = f.Int("max_health", 1, int.MaxValue);
        Locomotion? locomotion = f.Choice("locomotion", RulesEnums.ParseLocomotion, RulesEnums.LocomotionKeys);
        int? turnRate = f.Int("turn_rate", 0, 255);
        ArmorClass? armor = f.Choice("armor", RulesEnums.ParseArmor, RulesEnums.ArmorKeys);
        int? sight = f.Int("sight", 0, SimConstants.MaxMapCells);
        int? weapon = f.WeaponIndex(weapons);
        bool? hasTurret = f.Bool("has_turret");
        int? turretTurnRate = f.Int("turret_turn_rate", 0, 255);
        bool? crusher = f.Bool("crusher");
        int? cost = f.Int("cost", 0, int.MaxValue);
        List<string>? prerequisites = f.StrList("prerequisites");
        int? harvestCapacity = f.OptInt("harvest_capacity", 0, 1000, 0);
        int? buildTime = f.OptInt("build_time", 0, int.MaxValue, 0);
        MobileV3? v3 = ParseMobileV3(f, weapons, refs, TunnelSize.None);
        int? passengers = f.OptInt("passengers", 0, 100, 0);
        int? deploysInto = f.OptIndex("deploys_into", buildingIds, "building");
        int? tier = f.OptInt("tier", 1, RulesLoader.MaxTier, 1);
        int? unpacksInto = f.OptIndex("unpacks_into", buildingIds, "building");   // M6-02
        int? packTicks = f.OptInt("pack_ticks", 0, 1_000_000, 0);
        int? salvageRate = f.OptInt("salvage_rate", 0, 10_000, 0);
        bool? raider = f.OptBool("raider", false);
        bool? raiderOneUse = f.OptBool("raider_one_use", false);
        int? dropsBlackout = f.OptInt("drops_blackout_ticks", 0, 1_000_000, 0);
        int? auraRepair = f.OptInt("aura_repair_permille", 0, 1000, 0);
        int? towBonus = f.OptInt("tow_bonus_permille", 0, 100_000, 0);
        bool hasVeteran = f.Has("veteran");   // VET-1: absent = armed types rank
        bool? veteran = hasVeteran ? f.Bool("veteran") : false;
        if (veteran is null) return null;
        if (unpacksInto is null || packTicks is null || salvageRate is null || raider is null || raiderOneUse is null
            || dropsBlackout is null || auraRepair is null || towBonus is null) return null;
        if (v3 is null || passengers is null || deploysInto is null || tier is null) return null;
        if (harvestCapacity is null || buildTime is null || speed is null || maxHealth is null || locomotion is null || turnRate is null || armor is null
            || sight is null || weapon is null || hasTurret is null || turretTurnRate is null || crusher is null
            || cost is null || prerequisites is null)
        {
            return null;
        }
        refs.AddAll(f, "prerequisites", prerequisites);
        return new VehicleType(f.Id, locomotion.Value, speed.Value, turnRate.Value, maxHealth.Value, armor.Value,
            sight.Value, weapon.Value, hasTurret.Value, turretTurnRate.Value, crusher.Value, cost.Value, prerequisites)
        {
            HarvestCapacity = harvestCapacity.Value,
            BuildTime = buildTime.Value,
            Faction = v3.Faction,
            TunnelSize = v3.TunnelSize,
            AuraRadius = v3.AuraRadius,
            AuraRofPermille = v3.AuraRof,
            AuraSpeedPermille = v3.AuraSpeed,
            AuraTargets = v3.AuraTargets,
            RadResistPermille = v3.RadResist,
            Irradiated = v3.Irradiated,
            Stealth = v3.Stealth,
            DeathWeapon = v3.DeathWeapon,
            JamRadius = v3.JamRadius,
            SeismicRadius = v3.SeismicRadius,
            Passengers = passengers.Value,
            DeploysInto = deploysInto.Value,
            Tier = tier.Value,
            UnpacksInto = unpacksInto.Value,
            PackTicks = packTicks.Value,
            SalvageRate = salvageRate.Value,
            Raider = raider.Value,
            RaiderOneUse = raiderOneUse.Value,
            DropsBlackoutTicks = dropsBlackout.Value,
            AuraRepairPermille = auraRepair.Value,
            TowBonusPermille = towBonus.Value,
            Veteran = hasVeteran ? veteran.Value : null,
        };
    }

    /// <summary>Parses one infantry type. <paramref name="weapons"/> must already be sorted by id.</summary>
    public static InfantryType? ParseInfantry(RulesFields f, IReadOnlyList<WeaponType> weapons, RulesReferences refs)
    {
        f.CheckKeys(InfantryKeys);
        int? speed = f.Int("speed", 1, int.MaxValue);
        int? maxHealth = f.Int("max_health", 1, int.MaxValue);
        ArmorClass? armor = f.Choice("armor", RulesEnums.ParseArmor, RulesEnums.ArmorKeys);
        int? sight = f.Int("sight", 0, SimConstants.MaxMapCells);
        int? weapon = f.WeaponIndex(weapons);
        int? cost = f.Int("cost", 0, int.MaxValue);
        List<string>? prerequisites = f.StrList("prerequisites");
        int? buildTime = f.OptInt("build_time", 0, int.MaxValue, 0);
        MobileV3? v3 = ParseMobileV3(f, weapons, refs, TunnelSize.S);
        bool? engineer = f.OptBool("engineer", false);
        int? tier = f.OptInt("tier", 1, RulesLoader.MaxTier, 1);
        bool? canGarrison = engineer is null ? null : f.OptBool("can_garrison", !engineer.Value);   // engineers stay out unless set
        int? salvageRate = f.OptInt("salvage_rate", 0, 10_000, 0);   // M6-02
        bool? raider = f.OptBool("raider", false);
        bool? raiderOneUse = f.OptBool("raider_one_use", false);
        int? ashDelay = f.OptInt("ash_stealth_delay_ticks", 0, 1_000_000, 0);
        int? ashThreshold = f.OptInt("ash_stealth_threshold", 0, 255, 0);
        int? auraRepair = f.OptInt("aura_repair_permille", 0, 1000, 0);
        bool hasVeteran = f.Has("veteran");   // VET-1: absent = armed types rank
        bool? veteran = hasVeteran ? f.Bool("veteran") : false;
        if (veteran is null) return null;
        if (salvageRate is null || raider is null || raiderOneUse is null || ashDelay is null || ashThreshold is null || auraRepair is null)
            return null;
        if (v3 is null || engineer is null || tier is null || canGarrison is null) return null;
        if (buildTime is null || speed is null || maxHealth is null || armor is null || sight is null || weapon is null
            || cost is null || prerequisites is null)
        {
            return null;
        }
        refs.AddAll(f, "prerequisites", prerequisites);
        return new InfantryType(f.Id, speed.Value, maxHealth.Value, armor.Value, sight.Value, weapon.Value,
            cost.Value, prerequisites)
        {
            BuildTime = buildTime.Value,
            Faction = v3.Faction,
            TunnelSize = v3.TunnelSize,
            AuraRadius = v3.AuraRadius,
            AuraRofPermille = v3.AuraRof,
            AuraSpeedPermille = v3.AuraSpeed,
            AuraTargets = v3.AuraTargets,
            RadResistPermille = v3.RadResist,
            Irradiated = v3.Irradiated,
            Stealth = v3.Stealth,
            DeathWeapon = v3.DeathWeapon,
            JamRadius = v3.JamRadius,
            SeismicRadius = v3.SeismicRadius,
            Engineer = engineer.Value,
            Tier = tier.Value,
            CanGarrison = canGarrison.Value,
            SalvageRate = salvageRate.Value,
            Raider = raider.Value,
            RaiderOneUse = raiderOneUse.Value,
            AshStealthDelayTicks = ashDelay.Value,
            AshStealthThreshold = ashThreshold.Value,
            AuraRepairPermille = auraRepair.Value,
            Veteran = hasVeteran ? veteran.Value : null,
        };
    }
}
