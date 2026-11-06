// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Data;
using Rts.Sim.Rules;

namespace Rts.AI.Recruit;

/// <summary>
/// M7-10: Expedition knowledge for the Recruit tier (design 07 §7). The base starts as a Command Ship at sea; the first act is to sail it
/// to a landing coast and Deploy it into the Landing Depot (see <see cref="ExpeditionTactics"/>). The build order then runs Pile House
/// (power), Dredge Works (refinery with a free Skimmer), Anchorage (the shipment dock), Billet, Hover Yard, a second Pile House, a Beacon
/// for the zone, Quay, and the Plotting Room (the tier-2 gate), then the Shipyard and Flight Deck. Few, dear units: Marines on foot,
/// Glide Tanks on hover, Destroyers and Cruisers from the tactics' special orders. Ids only; costs, power and prerequisites come from the rules.
/// </summary>
internal sealed class ExpeditionRecruitFaction : RecruitFaction
{
    public override Faction Faction => Faction.Expedition;
    public override string YardId => "exp_landing_depot";
    public override string PowerId => "exp_pile_house";

    // (tune) M7-10 / S11 (naval strike): the Plotting Room, Quay and Shipyard come before the second Pile House and the Beacons.
    // Power, then the refinery (a free Skimmer), the shipment dock early (the first tender sails at once), the producers,
    // a beacon to grow the zone, the Quay (ships), then the tier-2 gate and the fleet and air tech.
    public override string[] DefaultOrder { get; } =
    [
        "exp_pile_house", "exp_dredge_works", "exp_anchorage", "exp_billet", "exp_hover_yard", "exp_plotting_room", "exp_quay", "exp_shipyard",
        "exp_pile_house", "exp_beacon", "exp_beacon", "exp_flight_deck",
    ];

    // (tune) M7-10: Marines are the only soldier available at tier 1.
    public override string[] InfantryBag { get; } = ["exp_marine", "exp_marine", "exp_marine", "exp_marine"];

    // (tune) M7-10: no armed vehicle exists below tier 2; the Glide Tank is the first one (the lane waits for the Plotting Room).
    public override string[] VehicleBag { get; } = ["exp_glide_tank", "exp_glide_tank", "exp_glide_tank"];
    public override string FirstVehicle => "exp_glide_tank";
    public override bool SkipUnbuildable => true;
    public override string HarvesterId => "exp_skimmer";
    public override int ExtraHarvesters => 2;   // (tune) M7-10
    public override string RefineryId => "exp_dredge_works";
    public override string BarracksId => "exp_billet";
    public override string FactoryId => "exp_hover_yard";
    public override string DefenceId => "exp_pillbox";
    public override string? ResearchId => "exp_plotting_room";
    public override string? AirProducerId => "exp_flight_deck";
    public override string? SuperweaponBuildingId => "exp_salvo_array";
    public override string? PreferredAircraftId => "exp_gunship";

    // (tune) M7-10: tier-2/3 roster (design 07 §5): the Pathfinder and Ironbound join the Marines, Sparkers and Menders from the tactics.
    public override string[] InfantryTier2 { get; } = ["exp_pathfinder", "exp_marine", "exp_ironbound"];
    public override string[] VehicleTier2 { get; } = ["exp_glide_tank", "exp_glide_tank", "exp_sparker"];
    public override string[] VehicleTier3 { get; } = ["exp_barrage_lander"];
    public override string? Tier2GateId => "exp_plotting_room";
    public override string? Tier3GateId => "exp_core_hall";
    public override string? AntiAirId => "exp_sam";

    /// <summary>A repertoire that names Expedition structures is used as is; other factions' ids in a shared profile are ignored.</summary>
    public override string[] OrderFor(IReadOnlyList<string> repertoire)
    {
        foreach (string id in repertoire)
            if (!id.StartsWith("exp_", StringComparison.Ordinal)) return DefaultOrder;
        return repertoire.Count > 0 ? repertoire.ToArray() : DefaultOrder;
    }

    public override IFactionTactics? CreateTactics() => new ExpeditionTactics(null);

    public override IFactionTactics? CreateTactics(DifficultyProfile profile) => new ExpeditionTactics(profile);
}
