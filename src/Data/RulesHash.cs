// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using System.IO.Hashing;
using System.Text;
using Rts.Sim.Rules;

namespace Rts.Data;

/// <summary>
/// Hash over the parsed rules, not over the file text. All integers are little-endian; strings are a 4-byte length
/// then UTF-8 bytes; booleans are one byte. Layout, in order:
/// <list type="number">
/// <item>Version byte (<see cref="FormatVersion"/>), then TickRate, DefaultMapWidth, DefaultMapHeight, then the economy table: StartingCash, CreditsAshfall,
/// CreditsPitchblende, HarvestTicksPerUnit, UnloadTicksPerUnit, RegrowthIntervalTicks, SellRefundPermille, RepairCostPermille,
/// RepairHpPerTick, BuildRadius, MaxQueue, BuildTicksPer100Credits, the three LowPowerSlowdownPermille values.</item>
/// <item>Weapons: count, then per weapon in id order: id, Damage, Range, MinRange, RateOfFire, ProjectileSpeed, Warhead index.</item>
/// <item>Warheads: count, then per warhead in id order: id, versus count, versus values, Splash, HitsAllies.</item>
/// <item>Vehicles: count, then per vehicle in id order: id, Locomotion, Speed, TurnRate, MaxHealth, Armor, Sight,
/// PrimaryWeapon, HasTurret, TurretTurnRate, Crusher, Cost, prerequisites (count, ids), HarvestCapacity, BuildTime.</item>
/// <item>Infantry: count, then per type in id order: id, Speed, MaxHealth, Armor, Sight, PrimaryWeapon, Cost,
/// prerequisites (count, ids), BuildTime.</item>
/// <item>Buildings: count, then per building in id order: id, FootprintW, FootprintH, MaxHealth, Armor, Sight,
/// PrimaryWeapon, Cost, Power, prerequisites (count, ids), provides (count, names), Category (byte), Produces (byte, 255 = none), BuildTime,
/// Storage, Refinery, Dock X/Y, Exit X/Y (resolved), FreeUnit, Wall, NeedsPower, Buildable.</item>
/// <item>Effects: count, then per effect in id order: id, Op, Target, Attr, Value.</item>
/// <item>Terrain: speed percent for every land (LandType order) and locomotion (Locomotion order), then the buildable
/// flag for every land.</item>
/// <item>Schema v3 block (M3-01, format 6): the soft flag for every land; tunnels (TransitTicks, NetworkCapPoints, PointsS,
/// PointsM, DiveTicks, TelegraphTicks, EntranceTelegraphTicks, EmergingHealthPermille, SurfacingRing); radiation (DecayTicks,
/// DamageIntervalTicks, DamageThreshold, DamagePermille, NoBuildThreshold); stealth DelayTicks; start kits (count, then per
/// faction: conyard, units); then per weapon Targets (byte); per warhead Contaminate, Decontaminate,
/// BlackoutTicks; per vehicle the mobile v3 fields (Faction, TunnelSize, AuraRadius, AuraRofPermille, AuraSpeedPermille,
/// AuraTargets, RadResistPermille, Stealth, DeathWeapon, JamRadius, SeismicRadius) then Passengers, DeploysInto;
/// per infantry the mobile v3 fields then Engineer; per building Faction, Entrance, TicksPerPoint, Researches, Gate,
/// SeismicRadius, SeesThroughBlackout, JamRadius, Superweapon, SuperweaponChargeTicks, SuperweaponEntranceRange, MorphsInto, MorphTicks; then upgrades
/// (count, then per upgrade in id order: id, Faction, Cost, BuildTime, prerequisites, Effect). Lists are in the same id order as
/// above, so the block needs no ids of its own.</item>
/// <item>Schema v4 block (M4-01, format 7): garrison (EjectDamagePermille, EnterRangeCells); foam SpeedPermille; per start kit
/// Tier2Provides, Tier3Provides; per warhead HitsBurrowed, BonusVsEntrancePercent, FoamTicks, GarrisonDamagePercent; per vehicle Tier;
/// per infantry Tier, CanGarrison; per building Tier, Garrison, GarrisonRangeBonus, GarrisonRofPermille; per upgrade Tier, Effects
/// (count, ids). Prerequisites above are the final lists after tier sugar. The land loops cover every land type, so Shallow (7)
/// joins the terrain and soft tables.</item>
/// <item>Schema v5 block (M6-02, format 8): economy WreckTicks, SalvagePercent, WreckMinValue, WreckMaxValue, PlunderPercent, PlunderCap,
/// SpoilsPermille; per warhead StormRadius, StormTicks, StormContaminateStep, StormContaminateCap, StormSpeedPermille; per vehicle
/// UnpacksInto, PackTicks, SalvageRate, Raider, RaiderOneUse, DropsBlackoutTicks, AuraRepairPermille, TowBonusPermille, OneUse, Buildable; per infantry
/// SalvageRate, Raider, RaiderOneUse, AshStealthDelayTicks, AshStealthThreshold, AuraRepairPermille; per building PacksInto, PackTicks;
/// per upgrade PackTicksPermille, SalvagePermille, PlunderPermille, RepairsVehiclesAtRange, then (S44) WreckValuePermille, RepairRangeCells, RepairRatePermille.</item>
/// <item>Schema v6 block (VET-1, format 9): Veterancy Enabled, Thresholds, KillPermilleUnit, KillPermilleBuilding, DamagePermille,
/// TakenPermille, ReloadPermille, SpeedPermille, HealIntervalTicks, HealPermille, HealQuietTicks; per vehicle and infantry CanRank
/// (the resolved <c>veteran</c> flag) and VetThresholdPermille.</item>
/// <item>Schema v7 block (SUP-1, format 10): Supply Enabled, Ceiling, Band1Max, Band2Max, BandValues, StockIntervals; per vehicle and
/// infantry Supply; per building SupplyProvided and SupplyUnstocked.</item>
/// <item>Schema v8 block (M7-02, format 11): per land type Landing; Shipments Enabled, IntervalTicks, Value, DecayPermille, FloorPermille,
/// CharterTicks, TendersMax, LocalAshBonusPermille, TenderType (M7-06); per vehicle DrawScalePermille, DeepOnly, Submerges, SonarRadius, AircraftSlots,
/// SortieTicks, UnloadTicks, VehicleSlots (M7-04b), then Ammo and RearmTicks only for a type with either set (D-156); then the eight mine fields only for a type with MineCapacity or MineSweepRadius set (S44 M-01); per building WaterBuilding, WaterMask (count, then cells), BeachheadSource, BeachheadRadius, ShipmentDock (M7-06), SonarRadius (simfu-4); then per warhead SalvoMissiles, SalvoTicks, SalvoRadius, SalvoDamage, SalvoWarnTicks, SalvoSplash (M7-12). The terrain
/// table above grows with the land and locomotion counts (Coast, Naval).</item>
/// <item>Schema v9 block (M12-16, format 12): Props TreeHealth, SnagHealth, WreckHealth, SplashArmor (byte), CrushPathPenalty.</item>
/// </list>
/// Hashed with XxHash64. Whitespace, comments and key order in the TOML do not affect it. The loader sorts each
/// list by id before this runs.
/// </summary>
public static class RulesHash
{
    /// <summary>Canonical layout version. Bump only with a DECISIONS entry.</summary>
    public const byte FormatVersion = 12;   // 12: schema v9 block (M12-16, D-264); 11: schema v8 block (M7-02); 10: schema v7 block (SUP-1); 9: schema v6 block (VET-1); 6: schema v3 block (M3-01); 7: schema v4 block + Shallow land (M4-01); 8: schema v5 block (M6-02)

    /// <summary>Computes the rules hash over the game constants and every sim type table.</summary>
    public static ulong Compute(GameRules game, SimRules sim)
    {
        using var ms = new MemoryStream();
        using (var w = new BinaryWriter(ms, Encoding.UTF8, leaveOpen: true))
        {
            w.Write(FormatVersion);
            w.Write(game.TickRate);
            w.Write(game.DefaultMapWidth);
            w.Write(game.DefaultMapHeight);

            EconomyRules e = sim.Economy;
            w.Write(e.StartingCash);
            w.Write(e.CreditsAshfall);
            w.Write(e.CreditsPitchblende);
            w.Write(e.HarvestTicksPerUnit);
            w.Write(e.UnloadTicksPerUnit);
            w.Write(e.RegrowthIntervalTicks);
            w.Write(e.SellRefundPermille);
            w.Write(e.RepairCostPermille);
            w.Write(e.RepairHpPerTick);
            w.Write(e.BuildRadius);
            w.Write(e.MaxQueue);
            w.Write(e.BuildTicksPer100Credits);
            w.Write(e.LowPowerSlowdownPermille.Count);
            foreach (int v in e.LowPowerSlowdownPermille) w.Write(v);

            w.Write(sim.Weapons.Count);
            foreach (WeaponType x in sim.Weapons)
            {
                WriteString(w, x.Id);
                w.Write(x.Damage);
                w.Write(x.Range);
                w.Write(x.MinRange);
                w.Write(x.RateOfFire);
                w.Write(x.ProjectileSpeed);
                w.Write(x.Warhead);
            }

            w.Write(sim.Warheads.Count);
            foreach (WarheadType x in sim.Warheads)
            {
                WriteString(w, x.Id);
                w.Write(x.VersusPercent.Count);
                foreach (int v in x.VersusPercent) w.Write(v);
                w.Write(x.Splash);
                w.Write(x.HitsAllies);
            }

            w.Write(sim.Vehicles.Count);
            foreach (VehicleType v in sim.Vehicles)
            {
                WriteString(w, v.Id);
                w.Write((byte)v.Locomotion);
                w.Write(v.Speed);
                w.Write(v.TurnRate);
                w.Write(v.MaxHealth);
                w.Write((byte)v.Armor);
                w.Write(v.Sight);
                w.Write(v.PrimaryWeapon);
                w.Write(v.HasTurret);
                w.Write(v.TurretTurnRate);
                w.Write(v.Crusher);
                w.Write(v.Cost);
                WriteStrings(w, v.Prerequisites);
                w.Write(v.HarvestCapacity);
                w.Write(v.BuildTime);
                if (v.SecondaryWeapon >= 0 || v.TertiaryWeapon >= 0) { w.Write(v.SecondaryWeapon); w.Write(v.TertiaryWeapon); }   // S44 (M-04): absent = unchanged bytes
            }

            w.Write(sim.Infantry.Count);
            foreach (InfantryType x in sim.Infantry)
            {
                WriteString(w, x.Id);
                w.Write(x.Speed);
                w.Write(x.MaxHealth);
                w.Write((byte)x.Armor);
                w.Write(x.Sight);
                w.Write(x.PrimaryWeapon);
                w.Write(x.Cost);
                WriteStrings(w, x.Prerequisites);
                w.Write(x.BuildTime);
            }

            w.Write(sim.Buildings.Count);
            foreach (BuildingType x in sim.Buildings)
            {
                WriteString(w, x.Id);
                w.Write(x.FootprintW);
                w.Write(x.FootprintH);
                w.Write(x.MaxHealth);
                w.Write((byte)x.Armor);
                w.Write(x.Sight);
                w.Write(x.PrimaryWeapon);
                w.Write(x.Cost);
                w.Write(x.Power);
                WriteStrings(w, x.Prerequisites);
                WriteStrings(w, x.Provides);
                w.Write((byte)x.Category);
                w.Write(x.Produces.HasValue ? (byte)x.Produces.Value : (byte)255);
                w.Write(x.BuildTime);
                w.Write(x.Storage);
                w.Write(x.Refinery);
                w.Write(x.Dock.X);
                w.Write(x.Dock.Y);
                w.Write(x.Exit.X);
                w.Write(x.Exit.Y);
                w.Write(x.FreeUnit);
                w.Write(x.Wall);
                w.Write(x.NeedsPower);
                w.Write(x.Buildable);
            }

            w.Write(sim.Effects.Count);
            foreach (EffectDef x in sim.Effects)
            {
                WriteString(w, x.Id);
                WriteString(w, x.Op);
                WriteString(w, x.Target);
                WriteString(w, x.Attr);
                w.Write(x.Value);
            }

            for (int land = 0; land < RuleCounts.LandTypes; land++)
            {
                for (int loco = 0; loco < RuleCounts.Locomotions; loco++)
                {
                    w.Write(sim.Terrain.SpeedPercent((LandType)land, (Locomotion)loco));
                }
            }
            for (int land = 0; land < RuleCounts.LandTypes; land++)
            {
                w.Write(sim.Terrain.Buildable((LandType)land));
            }
            WriteSchemaV3(w, sim);
            WriteSchemaV4(w, sim);
            WriteSchemaV5(w, sim);
            WriteSchemaV6(w, sim);
            WriteSchemaV7(w, sim);
            WriteSchemaV8(w, sim);
            WriteSchemaV9(w, sim);
            if (sim.Movement.StuckRecovery != 0) { w.Write((byte)0xA1); w.Write(sim.Movement.StuckRecovery); }   // S41 M13-44: only when on, so every other rule set keeps its hash
        }
        return XxHash64.HashToUInt64(ms.GetBuffer().AsSpan(0, (int)ms.Length));
    }

    private static void WriteSchemaV3(BinaryWriter w, SimRules sim)
    {
        for (int land = 0; land < RuleCounts.LandTypes; land++) w.Write(sim.Terrain.Soft((LandType)land));
        TunnelRules t = sim.Tunnels;
        w.Write(t.TransitTicks); w.Write(t.NetworkCapPoints); w.Write(t.PointsS); w.Write(t.PointsM); w.Write(t.DiveTicks);
        w.Write(t.TelegraphTicks); w.Write(t.EntranceTelegraphTicks); w.Write(t.EmergingHealthPermille); w.Write(t.SurfacingRing);
        RadiationRules r = sim.Radiation;
        w.Write(r.DecayTicks); w.Write(r.DamageIntervalTicks); w.Write(r.DamageThreshold); w.Write(r.DamagePermille); w.Write(r.NoBuildThreshold);
        w.Write(r.AshfallDamage); w.Write(r.AshfallIntervalTicks); w.Write(r.AshPathPenalty);   // S19 M13-12
        w.Write(sim.Stealth.DelayTicks);
        w.Write(sim.StartKits.Count);
        foreach (FactionStartKit k in sim.StartKits) { WriteString(w, k.Conyard); WriteStrings(w, k.Units); }

        foreach (WeaponType x in sim.Weapons) { w.Write((byte)x.Targets); if (x.NeedsSpotter) w.Write((byte)0xD5); }   // S44 M-17: only spotter weapons, so other hashes keep
        foreach (WarheadType x in sim.Warheads)
        {
            w.Write(x.Contaminate); w.Write(x.Decontaminate); w.Write(x.BlackoutTicks);
        }
        foreach (VehicleType x in sim.Vehicles)
        {
            WriteMobile(w, x.Faction, x.TunnelSize, x.AuraRadius, x.AuraRofPermille, x.AuraSpeedPermille, x.AuraTargets,
                x.RadResistPermille, x.Stealth, x.DeathWeapon, x.JamRadius, x.SeismicRadius);
            w.Write(x.Passengers); w.Write(x.DeploysInto);
        }
        foreach (InfantryType x in sim.Infantry)
        {
            WriteMobile(w, x.Faction, x.TunnelSize, x.AuraRadius, x.AuraRofPermille, x.AuraSpeedPermille, x.AuraTargets,
                x.RadResistPermille, x.Stealth, x.DeathWeapon, x.JamRadius, x.SeismicRadius);
            w.Write(x.Engineer);
        }
        foreach (BuildingType x in sim.Buildings)
        {
            w.Write((byte)x.Faction); w.Write((byte)x.Entrance); w.Write(x.TicksPerPoint); w.Write(x.Researches); w.Write(x.Gate);
            w.Write(x.SeismicRadius); w.Write(x.SeesThroughBlackout); w.Write(x.JamRadius); WriteString(w, x.Superweapon);
            w.Write(x.SuperweaponChargeTicks); w.Write(x.SuperweaponEntranceRange); w.Write(x.MorphsInto); w.Write(x.MorphTicks);
        }
        w.Write(sim.Upgrades.Count);
        foreach (UpgradeType u in sim.Upgrades)
        {
            WriteString(w, u.Id); w.Write((byte)u.Faction); w.Write(u.Cost); w.Write(u.BuildTime);
            WriteStrings(w, u.Prerequisites); WriteString(w, u.Effect);
        }
    }

    private static void WriteSchemaV4(BinaryWriter w, SimRules sim)
    {
        w.Write(sim.Garrison.EjectDamagePermille); w.Write(sim.Garrison.EnterRangeCells);
        w.Write(sim.Foam.SpeedPermille);
        foreach (FactionStartKit k in sim.StartKits) { WriteString(w, k.Tier2Provides); WriteString(w, k.Tier3Provides); }
        foreach (WarheadType x in sim.Warheads)
        {
            w.Write(x.HitsBurrowed); w.Write(x.BonusVsEntrancePercent); w.Write(x.FoamTicks); w.Write(x.GarrisonDamagePercent);
        }
        foreach (VehicleType x in sim.Vehicles) w.Write(x.Tier);
        foreach (InfantryType x in sim.Infantry) { w.Write(x.Tier); w.Write(x.CanGarrison); }
        foreach (BuildingType x in sim.Buildings)
        {
            w.Write(x.Tier); w.Write(x.Garrison); w.Write(x.GarrisonRangeBonus); w.Write(x.GarrisonRofPermille);
        }
        foreach (UpgradeType u in sim.Upgrades) { w.Write(u.Tier); WriteStrings(w, u.Effects); }
    }

    private static void WriteSchemaV6(BinaryWriter w, SimRules sim)
    {
        VeterancyRules v = sim.Veterancy;
        w.Write(v.Enabled);
        WriteInts(w, v.Thresholds);
        w.Write(v.KillPermilleUnit); w.Write(v.KillPermilleBuilding);
        WriteInts(w, v.DamagePermille); WriteInts(w, v.TakenPermille); WriteInts(w, v.ReloadPermille); WriteInts(w, v.SpeedPermille);
        w.Write(v.HealIntervalTicks);
        WriteInts(w, v.HealPermille);
        w.Write(v.HealQuietTicks);
        foreach (VehicleType x in sim.Vehicles) { w.Write(x.CanRank); w.Write(x.VetThresholdPermille); }
        foreach (InfantryType x in sim.Infantry) { w.Write(x.CanRank); w.Write(x.VetThresholdPermille); }
    }

    private static void WriteSchemaV7(BinaryWriter w, SimRules sim)
    {
        SupplyRules s = sim.Supply;
        w.Write(s.Enabled); w.Write(s.Ceiling); w.Write(s.Band1Max); w.Write(s.Band2Max);
        WriteInts(w, s.BandValues);
        w.Write(s.StockIntervals);
        foreach (VehicleType x in sim.Vehicles) w.Write(x.Supply);
        foreach (InfantryType x in sim.Infantry) w.Write(x.Supply);
        foreach (BuildingType x in sim.Buildings) { w.Write(x.SupplyProvided); w.Write(x.SupplyUnstocked); }
    }

    private static void WriteSchemaV9(BinaryWriter w, SimRules sim)
    {
        PropRules p = sim.Props;   // M12-16 (D-264)
        w.Write(p.TreeHealth); w.Write(p.SnagHealth); w.Write(p.WreckHealth); w.Write((byte)p.SplashArmor); w.Write(p.CrushPathPenalty);
    }

    private static void WriteSchemaV8(BinaryWriter w, SimRules sim)
    {
        for (int land = 0; land < RuleCounts.LandTypes; land++) w.Write(sim.Terrain.Landing((LandType)land));
        ShipmentRules s = sim.Shipments;
        w.Write(s.Enabled); w.Write(s.IntervalTicks); w.Write(s.Value); w.Write(s.DecayPermille); w.Write(s.FloorPermille);
        w.Write(s.CharterTicks); w.Write(s.TendersMax); w.Write(s.LocalAshBonusPermille); w.Write(s.TenderType);   // M7-06: tender type
        foreach (VehicleType x in sim.Vehicles)
        {
            w.Write(x.DrawScalePermille); w.Write(x.DeepOnly); w.Write(x.Submerges); w.Write(x.SonarRadius);
            w.Write(x.AircraftSlots); w.Write(x.SortieTicks); w.Write(x.UnloadTicks); w.Write(x.VehicleSlots);
            if (x.Ammo > 0 || x.RearmTicks > 0) { w.Write(x.Ammo); w.Write(x.RearmTicks); }   // D-156: only types with ammo, so old hashes keep
            if (x.MineCapacity > 0 || x.MineSweepRadius > 0)   // S44 M-01: only minelayers, so other hashes keep
            {
                w.Write(x.MineCapacity); w.Write(x.MineLayTicks); w.Write(x.MineDamage); w.Write(x.MineWarhead); w.Write(x.MineTriggerRadius);
                w.Write(x.MineSweepRadius); w.Write(x.MineSweepTicks); w.Write(x.MineRestockTicks);
            }
            if (x.DiveTicks > 0) { w.Write((byte)0xD6); w.Write(x.DiveTicks); }   // S44 M-18: only types with a dive delay
            if (x.BeaconDrop.Length > 0) { w.Write(true); WriteString(w, x.BeaconDrop); }   // S44 M-15: only a beacon carrier, so other hashes keep
            if (x.BuiltAt.Length > 0 || x.BayRepairPermille > 0) { w.Write((byte)0xD4); w.Write(x.BuiltAt); w.Write(x.BayRepairPermille); }   // S44 M-03/M-12: only types that set either
        }
        foreach (BuildingType x in sim.Buildings)
        {
            w.Write(x.WaterBuilding);
            w.Write(x.WaterMask.Count);
            foreach (bool b in x.WaterMask) w.Write(b);
            w.Write(x.BeachheadSource); w.Write(x.BeachheadRadius); w.Write(x.ShipmentDock);   // M7-06
            w.Write(x.SonarRadius);   // simfu-4
            if (x.HeavyYard || x.ShipRepairPermille > 0) { w.Write((byte)0xD5); w.Write(x.HeavyYard); w.Write(x.ShipRepairPermille); w.Write(x.ShipRepairRadius); }   // S44 M-03
        }
        foreach (WarheadType x in sim.Warheads)   // M7-12: Salvo Array
        {
            w.Write(x.SalvoMissiles); w.Write(x.SalvoTicks); w.Write(x.SalvoRadius); w.Write(x.SalvoDamage);
            w.Write(x.SalvoWarnTicks); w.Write(x.SalvoSplash);
        }
        // S44 (M-05, M-06): written only for warheads / upgrades that set them, so a rules set without tether or breach keeps its hash.
        for (int i = 0; i < sim.Warheads.Count; i++)
        {
            WarheadType x = sim.Warheads[i];
            if (x.TetherTicks > 0) { w.Write((byte)0xA5); w.Write(i); w.Write(x.TetherTicks); w.Write(x.TetherSpeedPermille); }
        }
        for (int i = 0; i < sim.Upgrades.Count; i++)
        {
            UpgradeType u = sim.Upgrades[i];
            if (u.BreachTicks > 0) { w.Write((byte)0xA6); w.Write(i); w.Write(u.BreachTicks); w.Write(u.BreachCooldownTicks); }
        }
    }

    private static void WriteInts(BinaryWriter w, IReadOnlyList<int> values)
    {
        w.Write(values.Count);
        foreach (int x in values) w.Write(x);
    }

    private static void WriteSchemaV5(BinaryWriter w, SimRules sim)
    {
        EconomyRules e = sim.Economy;
        w.Write(e.WreckTicks); w.Write(e.SalvagePercent); w.Write(e.WreckMinValue); w.Write(e.WreckMaxValue);
        w.Write(e.PlunderPercent); w.Write(e.PlunderCap); w.Write(e.SpoilsPermille);
        foreach (WarheadType x in sim.Warheads)
        {
            w.Write(x.StormRadius); w.Write(x.StormTicks); w.Write(x.StormContaminateStep); w.Write(x.StormContaminateCap);
            w.Write(x.StormSpeedPermille);
        }
        foreach (VehicleType x in sim.Vehicles)
        {
            w.Write(x.UnpacksInto); w.Write(x.PackTicks); w.Write(x.SalvageRate); w.Write(x.Raider); w.Write(x.RaiderOneUse);
            w.Write(x.DropsBlackoutTicks); w.Write(x.AuraRepairPermille); w.Write(x.TowBonusPermille);
            w.Write(x.OneUse); w.Write(x.Buildable);   // D-205 (M13-11)
        }
        foreach (InfantryType x in sim.Infantry)
        {
            w.Write(x.SalvageRate); w.Write(x.Raider); w.Write(x.RaiderOneUse); w.Write(x.AshStealthDelayTicks);
            w.Write(x.AshStealthThreshold); w.Write(x.AuraRepairPermille);
            if (x.Swims) w.Write(true);   // S44 M-02: written only for a swimmer, so rules without one hash exactly as before
            if (x.BeaconBuild.Length > 0) { w.Write(true); WriteString(w, x.BeaconBuild); }   // S44 M-15
        }
        foreach (BuildingType x in sim.Buildings) { w.Write(x.PacksInto); w.Write(x.PackTicks); }
        foreach (UpgradeType u in sim.Upgrades)
        {
            w.Write(u.PackTicksPermille); w.Write(u.SalvagePermille); w.Write(u.PlunderPermille); w.Write(u.RepairsVehiclesAtRange);
            w.Write(u.WreckValuePermille); w.Write(u.RepairRangeCells); w.Write(u.RepairRatePermille);   // S44
        }
    }

    private static void WriteMobile(BinaryWriter w, Faction faction, TunnelSize size, int auraRadius, int auraRof, int auraSpeed,
        IReadOnlyList<string> auraTargets, int radResist, bool stealth, int deathWeapon, int jam, int seismic)
    {
        w.Write((byte)faction); w.Write((byte)size); w.Write(auraRadius); w.Write(auraRof); w.Write(auraSpeed);
        WriteStrings(w, auraTargets); w.Write(radResist); w.Write(stealth); w.Write(deathWeapon);
        w.Write(jam); w.Write(seismic);
    }

    private static void WriteStrings(BinaryWriter w, IReadOnlyList<string> values)
    {
        w.Write(values.Count);
        foreach (string s in values) WriteString(w, s);
    }

    private static void WriteString(BinaryWriter w, string s)
    {
        byte[] b = Encoding.UTF8.GetBytes(s);
        w.Write(b.Length);
        w.Write(b);
    }
}
