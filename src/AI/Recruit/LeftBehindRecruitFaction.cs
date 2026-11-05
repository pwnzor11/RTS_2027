// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Data;
using Rts.Sim.Rules;

namespace Rts.AI.Recruit;

/// <summary>
/// M6-10: Left Behind knowledge for the Recruit tier (the Raider Warlord, design 06 §8). The base starts as a packed Mother Rig, so the
/// first act is the unpack (see <see cref="LeftBehindTactics"/>); the build order then runs Yard (fixed refinery and silo, free
/// Gleaner), Dynamo Rig, Muster Rig, Garage Rig, a Spike Nest and the Lookout (the tier-2 gate). Units are raiders first: Hook Runners
/// and Buggies. Ids only; costs, power and prerequisites come from the rules.
/// </summary>
internal sealed class LeftBehindRecruitFaction : RecruitFaction
{
    public override Faction Faction => Faction.LeftBehind;
    public override string YardId => "lb_mother_rig";
    public override string PowerId => "lb_dynamo_rig";

    // (tune) M6-10: economy first (the Yard brings a Gleaner), then the two producers; the Lookout opens the tier-2 roster for every tier.
    public override string[] DefaultOrder { get; } =
    [
        "lb_yard", "lb_dynamo_rig", "lb_muster_rig", "lb_garage_rig", "lb_dynamo_rig", "lb_spike_nest", "lb_lookout",
    ];

    // (tune) M6-10: Hook Runners are the raiding infantry, Scavengers the cheap body (and a slow salvager).
    public override string[] InfantryBag { get; } = ["lb_hook_runner", "lb_scavenger", "lb_hook_runner", "lb_scavenger"];

    // (tune) M6-10: Buggies are the raiders; Haulers and Dust Sails come from the tactics' special orders.
    public override string[] VehicleBag { get; } = ["lb_buggy", "lb_buggy", "lb_buggy"];
    public override string FirstVehicle => "lb_buggy";
    public override bool SkipUnbuildable => true;
    public override string HarvesterId => "lb_gleaner";
    public override string RefineryId => "lb_yard";
    public override string BarracksId => "lb_muster_rig";
    public override string FactoryId => "lb_garage_rig";
    public override string DefenceId => "lb_spike_nest";
    public override string? ResearchId => "lb_bench";
    public override string? AirProducerId => "lb_rotor_pad";
    public override string? SuperweaponBuildingId => "lb_second_drift";
    public override string? PreferredAircraftId => "lb_rotor";

    // (tune) M6-10: tier-2/3 roster (design 06 §5): the push units join at tier 2, the Rolling Fort at tier 3.
    public override string[] InfantryTier2 { get; } = ["lb_slinger", "lb_slinger", "lb_drifter_brute", "lb_tinker"];
    public override string[] VehicleTier2 { get; } = ["lb_harpoon_wagon", "lb_harpoon_wagon", "lb_organ"];
    public override string[] VehicleTier3 { get; } = ["lb_rolling_fort"];
    public override string? Tier2GateId => "lb_lookout";
    public override string? Tier3GateId => "lb_rotor_pad";
    public override string? AntiAirId => "lb_flak_net";

    /// <summary>A repertoire that names Left Behind structures is used as is; other factions' ids in a shared profile are ignored.</summary>
    public override string[] OrderFor(IReadOnlyList<string> repertoire)
    {
        foreach (string id in repertoire)
            if (!id.StartsWith("lb_", StringComparison.Ordinal)) return DefaultOrder;
        return repertoire.Count > 0 ? repertoire.ToArray() : DefaultOrder;
    }

    public override IFactionTactics? CreateTactics() => new LeftBehindTactics(null);

    public override IFactionTactics? CreateTactics(DifficultyProfile profile) => new LeftBehindTactics(profile);
}
