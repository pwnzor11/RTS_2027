// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Sim.Rules;

namespace Rts.AI.Recruit;

/// <summary>
/// Directorate knowledge for the Recruit tier. Build order: Reactor Vent, Smelter Vent (the free Scraper comes with it), Muster Hatch,
/// a second Reactor Vent, Assembly Bore, Cistern, Pillbox, then the Isotope Works and a Rad Mortar so the Reactor Crawler unlocks.
/// Ids only; costs, power and prerequisites come from the rules.
/// </summary>
internal sealed class DirectorateRecruitFaction : RecruitFaction
{
    public override Faction Faction => Faction.Directorate;
    public override string YardId => "dir_shaft";
    public override string PowerId => "dir_reactor_vent";   // ASSUMED (M3-10): the Reactor Vent is the only power structure

    // ASSUMED (M3-10): mirrors the Authority order step for step (power, refinery, infantry, power, factory, silo, turret), then the radiation tech.
    public override string[] DefaultOrder { get; } =
    [
        "dir_reactor_vent", "dir_smelter_vent", "dir_muster_hatch", "dir_reactor_vent", "dir_assembly_bore", "dir_cistern",
        "dir_pillbox", "dir_isotope_works", "dir_rad_mortar",
    ];

    // ASSUMED (M3-10): conscripts are the cheap bulk, an Ashthrower now and then (Authority: riflemen with rocketeers).
    public override string[] InfantryBag { get; } = ["dir_conscript", "dir_conscript", "dir_ashthrower", "dir_conscript"];

    // ASSUMED (M3-10): rocket trucks are the backbone; the Reactor Crawler joins once the Isotope Works stands (skipped until then).
    public override string[] VehicleBag { get; } = ["dir_rocket_truck", "dir_rocket_truck", "dir_reactor_crawler", "dir_rocket_truck"];
    public override string FirstVehicle => "dir_rocket_truck";
    public override bool SkipUnbuildable => true;

    /// <summary>A repertoire that names Directorate structures is used as is; Authority ids in a shared profile are ignored.</summary>
    public override string[] OrderFor(IReadOnlyList<string> repertoire)
    {
        foreach (string id in repertoire)
            if (!id.StartsWith("dir_", StringComparison.Ordinal)) return DefaultOrder;
        return repertoire.Count > 0 ? repertoire.ToArray() : DefaultOrder;
    }

    public override IFactionTactics? CreateTactics() => new DirectorateTactics();
}
