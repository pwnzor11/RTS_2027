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
/// faction: conyard, units); then per weapon Targets (byte); per warhead Contaminate, Decontaminate, BonusVsIrradiatedPercent,
/// BlackoutTicks; per vehicle the mobile v3 fields (Faction, TunnelSize, AuraRadius, AuraRofPermille, AuraSpeedPermille,
/// AuraTargets, RadResistPermille, Irradiated, Stealth, DeathWeapon, JamRadius, SeismicRadius) then Passengers, DeploysInto;
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
/// UnpacksInto, PackTicks, SalvageRate, Raider, RaiderOneUse, DropsBlackoutTicks, AuraRepairPermille, TowBonusPermille; per infantry
/// SalvageRate, Raider, RaiderOneUse, AshStealthDelayTicks, AshStealthThreshold, AuraRepairPermille; per building PacksInto, PackTicks;
/// per upgrade PackTicksPermille, SalvagePermille, PlunderPermille, RepairsVehiclesAtRange.</item>
/// <item>Schema v6 block (VET-1, format 9): Veterancy Enabled, Thresholds, KillPermilleUnit, KillPermilleBuilding, DamagePermille,
/// TakenPermille, ReloadPermille, SpeedPermille, HealIntervalTicks, HealPermille, HealQuietTicks; per vehicle and infantry CanRank
/// (the resolved <c>veteran</c> flag) and VetThresholdPermille.</item>
/// </list>
/// Hashed with XxHash64. Whitespace, comments and key order in the TOML do not affect it. The loader sorts each
/// list by id before this runs.
/// </summary>
public static class RulesHash
{
    /// <summary>Canonical layout version. Bump only with a DECISIONS entry.</summary>
    public const byte FormatVersion = 9;   // 9: schema v6 block (VET-1); 6: schema v3 block (M3-01); 7: schema v4 block + Shallow land (M4-01); 8: schema v5 block (M6-02)

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
        w.Write(sim.Stealth.DelayTicks);
        w.Write(sim.StartKits.Count);
        foreach (FactionStartKit k in sim.StartKits) { WriteString(w, k.Conyard); WriteStrings(w, k.Units); }

        foreach (WeaponType x in sim.Weapons) w.Write((byte)x.Targets);
        foreach (WarheadType x in sim.Warheads)
        {
            w.Write(x.Contaminate); w.Write(x.Decontaminate); w.Write(x.BonusVsIrradiatedPercent); w.Write(x.BlackoutTicks);
        }
        foreach (VehicleType x in sim.Vehicles)
        {
            WriteMobile(w, x.Faction, x.TunnelSize, x.AuraRadius, x.AuraRofPermille, x.AuraSpeedPermille, x.AuraTargets,
                x.RadResistPermille, x.Irradiated, x.Stealth, x.DeathWeapon, x.JamRadius, x.SeismicRadius);
            w.Write(x.Passengers); w.Write(x.DeploysInto);
        }
        foreach (InfantryType x in sim.Infantry)
        {
            WriteMobile(w, x.Faction, x.TunnelSize, x.AuraRadius, x.AuraRofPermille, x.AuraSpeedPermille, x.AuraTargets,
                x.RadResistPermille, x.Irradiated, x.Stealth, x.DeathWeapon, x.JamRadius, x.SeismicRadius);
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
        }
        foreach (InfantryType x in sim.Infantry)
        {
            w.Write(x.SalvageRate); w.Write(x.Raider); w.Write(x.RaiderOneUse); w.Write(x.AshStealthDelayTicks);
            w.Write(x.AshStealthThreshold); w.Write(x.AuraRepairPermille);
        }
        foreach (BuildingType x in sim.Buildings) { w.Write(x.PacksInto); w.Write(x.PackTicks); }
        foreach (UpgradeType u in sim.Upgrades)
        {
            w.Write(u.PackTicksPermille); w.Write(u.SalvagePermille); w.Write(u.PlunderPermille); w.Write(u.RepairsVehiclesAtRange);
        }
    }

    private static void WriteMobile(BinaryWriter w, Faction faction, TunnelSize size, int auraRadius, int auraRof, int auraSpeed,
        IReadOnlyList<string> auraTargets, int radResist, bool irradiated, bool stealth, int deathWeapon, int jam, int seismic)
    {
        w.Write((byte)faction); w.Write((byte)size); w.Write(auraRadius); w.Write(auraRof); w.Write(auraSpeed);
        WriteStrings(w, auraTargets); w.Write(radResist); w.Write(irradiated); w.Write(stealth); w.Write(deathWeapon);
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
