// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Sim.Rules;

namespace Rts.AI.Recruit;

/// <summary>Authority knowledge for the Recruit tier: the M2 build order and tier-1 unit mix, plus (M4-12) the tier-2/3 roster, gates,
/// Blockhouse, Flak, Airfield and Atomic Battery.</summary>
internal sealed class AuthorityRecruitFaction : RecruitFaction
{
    public override Faction Faction => Faction.Authority;
    public override string YardId => "auth_conyard";
    public override string PowerId => "auth_power";
    public override string[] DefaultOrder { get; } =
        ["auth_power", "auth_refinery", "auth_barracks", "auth_power", "auth_factory", "auth_silo", "auth_turret"];
    public override string[] InfantryBag { get; } = ["auth_rifleman", "auth_rifleman", "auth_rocketeer", "auth_rocketeer"];
    public override string[] VehicleBag { get; } = ["auth_tank", "auth_tank", "auth_tank", "auth_jeep"];
    public override string FirstVehicle => "auth_tank";
    public override string HarvesterId => "auth_scraper";
    public override string RefineryId => "auth_refinery";
    public override string BarracksId => "auth_barracks";
    public override string FactoryId => "auth_factory";
    public override string DefenceId => "auth_turret";
    public override string? ResearchId => "auth_engineering";
    // M4-12: tier-2/3 roster (design 08 §4). Ids only; prerequisites and tiers come from the rules. ASSUMED (M4-12) mixes (tune).
    public override string[] InfantryTier2 { get; } = ["auth_marksman", "auth_marksman"];
    public override string[] VehicleTier2 { get; } = ["auth_seismic_mortar", "auth_foam_truck", "auth_tank"];
    public override string[] VehicleTier3 { get; } = ["auth_bulwark", "auth_bulwark", "auth_howitzer"];
    public override string? Tier2GateId => "auth_engineering";
    public override string? Tier3GateId => "auth_signals_hq";
    public override string? GarrisonId => "auth_blockhouse";
    public override string? AntiAirId => "auth_flak";
    public override string? AirProducerId => "auth_airfield";
    public override string? SuperweaponBuildingId => "auth_atomic_battery";
    // M4-12: the bag gains tier types that a lost gate locks again; skip them instead of waiting (all tier-1 items share prerequisites,
    // so tier-1 games are unchanged)
    public override bool SkipUnbuildable => true;
}
