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

    /// <summary>M4-16 s12: a wave leaves only when our army value is at least this percent of the biggest enemy army seen lately (0 = the default 100).</summary>
    public int AttackRatioPct { get; init; }

    /// <summary>M4-16 s12: a wave on the march turns back when the visible local fight is clearly lost (integer value comparison).</summary>
    public bool PredictedRetreat { get; init; }

    /// <summary>M4-16 s12: with <see cref="PredictedRetreat"/>, the wave turns back when its value is below this percent of the visible enemy value near it (0 = 70).</summary>
    public int RetreatRatioPct { get; init; }

    /// <summary>D-167: per-faction AI keys from the <c>[faction.*]</c> tables (the same on every tier of one file).</summary>
    public FactionAiKeys FactionKeys { get; init; } = FactionAiKeys.Default;

    /// <summary>Harvesters wanted alive (1..6). With <see cref="ReplaceHarvesters"/> missing ones are built; expansion adds one.</summary>
    public int HarvesterTarget { get; init; } = 1;

    /// <summary>S22: the mop-up (finishing a beaten enemy) is allowed for this tier. Takes effect only with <see cref="RespondToAttacks"/>; on by default, off for measurement (<c>ai match --mopup off</c>) and in missions.</summary>
    public bool Mopup { get; init; } = true;

    /// <summary>S22: a copy with <see cref="Mopup"/> set, in the faction versions too.</summary>
    public DifficultyProfile WithMopup(bool on)
    {
        DifficultyProfile copy = this with { Mopup = on };
        copy._byFaction = (DifficultyProfile?[])_byFaction.Clone();
        for (int i = 0; i < copy._byFaction.Length; i++)
            if (copy._byFaction[i] is { } f) copy._byFaction[i] = f with { Mopup = on };
        return copy;
    }

    /// <summary>S15 lbknob: credits an AI keeps in hand before it buys a unit (added to the build-order reserve; 0 = none). Slows the army build-up only.</summary>
    public int UnitReserve { get; init; }

    /// <summary>S15 lbknob: army units (alive, harvesters excluded) at which unit production pauses until losses or a wave leave room (0 = no cap).</summary>
    public int ArmyCap { get; init; }

    /// <summary>S15 lbknob: with <see cref="ArmyCap"/>, the cap holds only before this tick (0 = for the whole game).</summary>
    public int ArmyCapUntilTick { get; init; }

    // S15 lbknob: per-faction versions of this tier (index = Faction enum value), from the [faction.<id>.tier<N>] tables; null = the shared profile.
    private DifficultyProfile?[] _byFaction = new DifficultyProfile?[5];

    /// <summary>S15 lbknob: this tier as <paramref name="faction"/> (the <c>Faction</c> enum value) plays it: the shared profile, or the same
    /// instance every call when a <c>[faction.&lt;id&gt;.tier&lt;N&gt;]</c> table overrides some numbers.</summary>
    public DifficultyProfile ForFaction(int faction) => faction >= 0 && faction < _byFaction.Length && _byFaction[faction] is { } p ? p : this;

    /// <summary>S15 lbknob: a copy of this profile that plays as <paramref name="over"/> for <paramref name="faction"/>.</summary>
    public DifficultyProfile WithFactionOverride(int faction, DifficultyProfile over)
    {
        DifficultyProfile copy = this with { };
        copy._byFaction = (DifficultyProfile?[])_byFaction.Clone();   // a record copy shares the field
        copy._byFaction[faction] = over;
        return copy;
    }
}
