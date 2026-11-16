// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
namespace Rts.Data;

/// <summary>
/// D-167: AI settings that depend on the faction, read from the optional <c>[faction.&lt;id&gt;]</c> tables of data/ai/difficulty.toml.
/// A missing table or key falls back to <see cref="DefaultHarassMinArmy"/>.
/// </summary>
/// <param name="HarassDirectorate">Army size before a Directorate raiding party is formed.</param>
/// <param name="HarassAuthority">Same for Authority.</param>
/// <param name="HarassLeftBehind">Same for LeftBehind.</param>
/// <param name="HarassExpedition">Same for Expedition.</param>
public sealed record FactionAiKeys(int HarassDirectorate, int HarassAuthority, int HarassLeftBehind, int HarassExpedition)
{
    /// <summary>Fallback for <c>harass_min_army</c>.</summary>
    public const int DefaultHarassMinArmy = 30;

    /// <summary>All keys at their fallback.</summary>
    public static FactionAiKeys Default { get; } = new(DefaultHarassMinArmy, DefaultHarassMinArmy, DefaultHarassMinArmy, DefaultHarassMinArmy);

    /// <summary>S29 (BAL-S29): the optional per-faction build levers, indexed like <c>DifficultyLoader.FactionIds</c> (authority, directorate, left_behind, expedition); null = all defaults.</summary>
    public FactionTuning[]? Tuning { get; init; }

    /// <summary>S29: the build levers of a faction (<paramref name="faction"/> is the <c>Faction</c> enum value: 0 Authority, 1 Directorate, 3 LeftBehind, 4 Expedition).</summary>
    public FactionTuning TuningOf(int faction)
    {
        int i = faction switch { 0 => 0, 1 => 1, 3 => 2, 4 => 3, _ => -1 };
        return i >= 0 && Tuning is { } t && i < t.Length ? t[i] : FactionTuning.Default;
    }

    /// <summary>The raiding-party army threshold for a faction (<paramref name="faction"/> is the <c>Faction</c> enum value: 0 Authority, 1 Directorate, 3 LeftBehind, 4 Expedition).</summary>
    public int HarassMinArmy(int faction) => faction switch
    {
        0 => HarassAuthority, 1 => HarassDirectorate, 3 => HarassLeftBehind, 4 => HarassExpedition, _ => DefaultHarassMinArmy,
    };
}
