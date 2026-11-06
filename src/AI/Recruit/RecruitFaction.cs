// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Data;
using Rts.Sim.Rules;

namespace Rts.AI.Recruit;

/// <summary>
/// Per-faction knowledge of the Recruit framework (M3-10): the structure ids the AI anchors on, its default build order and its
/// unit mix. The managers are faction-neutral and read everything faction-specific from here; ids are resolved against the
/// rules at run time (costs, prerequisites and sizes always come from the data). A new faction (or M3-11's tiers) adds a subclass.
/// </summary>
internal abstract class RecruitFaction
{
    /// <summary>The faction this knowledge describes.</summary>
    public abstract Faction Faction { get; }

    /// <summary>Construction yard id (the base anchor).</summary>
    public abstract string YardId { get; }

    /// <summary>Power structure id (inserted first whenever power would run short).</summary>
    public abstract string PowerId { get; }

    /// <summary>Structure build order used when the tier's repertoire does not name this faction's structures.</summary>
    public abstract string[] DefaultOrder { get; }

    /// <summary>Infantry unit bag (shuffled, seeded).</summary>
    public abstract string[] InfantryBag { get; }

    /// <summary>Vehicle unit bag (shuffled, seeded).</summary>
    public abstract string[] VehicleBag { get; }

    /// <summary>First vehicle built.</summary>
    public abstract string FirstVehicle { get; }

    /// <summary>M3-11: the harvester vehicle (replaced when lost).</summary>
    public abstract string HarvesterId { get; }

    /// <summary>M3-11: the refinery / smelter structure (a second one is the expansion).</summary>
    public abstract string RefineryId { get; }

    /// <summary>M3-11: infantry production structure.</summary>
    public abstract string BarracksId { get; }

    /// <summary>M3-11: vehicle production structure.</summary>
    public abstract string FactoryId { get; }

    /// <summary>M3-11: a ground defence structure for the Adapt build capability.</summary>
    public abstract string DefenceId { get; }

    /// <summary>M3-11: the research structure id (upgrades), or null when the faction has none.</summary>
    public abstract string? ResearchId { get; }

    /// <summary>M3-11: the structure that produces aircraft, or null (Authority has none).</summary>
    public virtual string? AirProducerId => null;

    /// <summary>M3-11: the superweapon structure, or null (Authority has none).</summary>
    public virtual string? SuperweaponBuildingId => null;

    /// <summary>M4-12: infantry added to the bag once the player is at tech tier 2 (and kept at tier 3).</summary>
    public virtual string[] InfantryTier2 => [];

    /// <summary>M4-12: vehicles added to the bag at tech tier 2.</summary>
    public virtual string[] VehicleTier2 => [];

    /// <summary>M4-12: vehicles added to the bag at tech tier 3.</summary>
    public virtual string[] VehicleTier3 => [];

    /// <summary>M4-12: the tier-2 gate structure (provides the faction's tier-2 name), or null.</summary>
    public virtual string? Tier2GateId => null;

    /// <summary>M4-12: the tier-3 gate structure, or null.</summary>
    public virtual string? Tier3GateId => null;

    /// <summary>M4-12: the faction's own garrisonable defence (Blockhouse / Redoubt), or null.</summary>
    public virtual string? GarrisonId => null;

    /// <summary>M4-12: an anti-air defence built once enemy aircraft were seen, or null.</summary>
    public virtual string? AntiAirId => null;

    /// <summary>True when a bag item that is not buildable yet (tech missing) is skipped instead of waited for.</summary>
    public virtual bool SkipUnbuildable => false;

    /// <summary>The build order for a tier's repertoire.</summary>
    public virtual string[] OrderFor(IReadOnlyList<string> repertoire) => repertoire.Count > 0 ? repertoire.ToArray() : DefaultOrder;

    /// <summary>Faction extras (tunnels, radiation, auras), or null when the faction needs none.</summary>
    public virtual IFactionTactics? CreateTactics() => null;

    /// <summary>M6-10: faction extras that depend on the tier's profile (Left Behind: relocation from tier 3, Dust Sails at the top). The
    /// default ignores the profile.</summary>
    public virtual IFactionTactics? CreateTactics(DifficultyProfile profile) => CreateTactics();

    /// <summary>M6-10: the aircraft type id the faction prefers when several are buildable (Left Behind: the Kite), or null.</summary>
    public virtual string? PreferredAircraftId => null;

    /// <summary>Knowledge for a faction.</summary>
    public static RecruitFaction For(Faction faction) => faction switch
    {
        Faction.Directorate => new DirectorateRecruitFaction(),
        Faction.LeftBehind => new LeftBehindRecruitFaction(),   // M6-10
        Faction.Expedition => new ExpeditionRecruitFaction(),   // M7-10
        _ => new AuthorityRecruitFaction(),
    };
}
