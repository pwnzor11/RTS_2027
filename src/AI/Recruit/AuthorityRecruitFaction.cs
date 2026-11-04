// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Sim.Rules;

namespace Rts.AI.Recruit;

/// <summary>Authority knowledge for the Recruit tier: exactly the M2 build order and unit mix (unchanged behaviour).</summary>
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
}
