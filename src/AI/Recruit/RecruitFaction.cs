// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
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

    /// <summary>True when a bag item that is not buildable yet (tech missing) is skipped instead of waited for.</summary>
    public virtual bool SkipUnbuildable => false;

    /// <summary>The build order for a tier's repertoire.</summary>
    public virtual string[] OrderFor(IReadOnlyList<string> repertoire) => repertoire.Count > 0 ? repertoire.ToArray() : DefaultOrder;

    /// <summary>Faction extras (tunnels, radiation, auras), or null when the faction needs none.</summary>
    public virtual IFactionTactics? CreateTactics() => null;

    /// <summary>Knowledge for a faction.</summary>
    public static RecruitFaction For(Faction faction) => faction == Faction.Directorate ? new DirectorateRecruitFaction() : new AuthorityRecruitFaction();
}
