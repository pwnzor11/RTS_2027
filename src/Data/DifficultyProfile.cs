// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
namespace Rts.Data;

/// <summary>
/// One difficulty tier's capability profile, loaded from data/ai/difficulty.toml (design 02 §7). Integers only.
/// Tier 1..5 = Recruit, Regular, Veteran, Elite, Commissar. Fields an AI does not use yet are reserved.
/// </summary>
public sealed record DifficultyProfile(
    string Id, int Tier, int DecisionIntervalTicks, int CommandDelayTicks, int ScoutInfoAgeTicks, int AttackThreshold,
    bool FocusFire, int RetreatHealthPct, int ExpansionTick, bool Superweapons, bool MultiFront,
    IReadOnlyList<string> BuildRepertoire)
{
    /// <summary>A neutral profile for tests and for AIs run without a data file.</summary>
    public static DifficultyProfile Default { get; } =
        new("regular", 2, 30, 1, 600, 12, false, 0, 0, false, false, Array.Empty<string>());

    // ---- M3-11 capability flags (all optional in the TOML; absent = off / 1). Each AI capability is gated on one of these, never on Tier. ----

    /// <summary>Repair damaged structures (Repair command) while cash allows.</summary>
    public bool Repair { get; init; }

    /// <summary>Build a new harvester when fewer than <see cref="HarvesterTarget"/> are alive or queued.</summary>
    public bool ReplaceHarvesters { get; init; }

    /// <summary>Send idle army units to defend when a structure takes damage.</summary>
    public bool RespondToAttacks { get; init; }

    /// <summary>Scatter units that are taking splash damage while standing still.</summary>
    public bool Scatter { get; init; }

    /// <summary>Choose units by their damage per credit against the armour the enemy has been seen to field.</summary>
    public bool EnemyAwareMix { get; init; }

    /// <summary>Split a large wave into two squads that hit two different targets.</summary>
    public bool TwoPronged { get; init; }

    /// <summary>Favour long-range units once the enemy is seen to hold several defensive structures.</summary>
    public bool CounterDefences { get; init; }

    /// <summary>Build and use aircraft where the faction can.</summary>
    public bool Air { get; init; }

    /// <summary>Keep a scout moving round the likely enemy positions.</summary>
    public bool ActiveScouting { get; init; }

    /// <summary>Send a small raiding party after remembered enemy harvesters and power structures.</summary>
    public bool Harass { get; init; }

    /// <summary>Research upgrades (builds the research structure and queues upgrades).</summary>
    public bool UseUpgrades { get; init; }

    /// <summary>Add production structures when cash piles up and defences when the enemy is armour-heavy.</summary>
    public bool AdaptBuild { get; init; }

    /// <summary>Harvesters wanted alive (1..6). With <see cref="ReplaceHarvesters"/> missing ones are built; expansion adds one.</summary>
    public int HarvesterTarget { get; init; } = 1;
}
