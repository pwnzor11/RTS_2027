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
/// <param name="CommitDirectorate">s29: <c>siege_commit_ratio</c> for Directorate (0 = off).</param>
/// <param name="CommitAuthority">Same for Authority.</param>
/// <param name="CommitLeftBehind">Same for LeftBehind.</param>
/// <param name="CommitExpedition">Same for Expedition.</param>
public sealed record FactionAiKeys(int HarassDirectorate, int HarassAuthority, int HarassLeftBehind, int HarassExpedition,
    int CommitDirectorate = 0, int CommitAuthority = 0, int CommitLeftBehind = 0, int CommitExpedition = 0)
{
    /// <summary>Fallback for <c>harass_min_army</c>.</summary>
    public const int DefaultHarassMinArmy = 30;

    /// <summary>All keys at their fallback.</summary>
    public static FactionAiKeys Default { get; } = new(DefaultHarassMinArmy, DefaultHarassMinArmy, DefaultHarassMinArmy, DefaultHarassMinArmy);

    /// <summary>The raiding-party army threshold for a faction (<paramref name="faction"/> is the <c>Faction</c> enum value: 0 Authority, 1 Directorate, 3 LeftBehind, 4 Expedition).</summary>
    public int HarassMinArmy(int faction) => faction switch
    {
        0 => HarassAuthority, 1 => HarassDirectorate, 3 => HarassLeftBehind, 4 => HarassExpedition, _ => DefaultHarassMinArmy,
    };

    /// <summary>s29 <c>siege_commit_ratio</c> (percent, 0 = off): a wave whose army is worth at least this percent of the enemy's seen army plus known defences is never called off by the health or predicted-loss retreat (<paramref name="faction"/> as in <see cref="HarassMinArmy"/>).</summary>
    public int CommitRatio(int faction) => faction switch
    {
        0 => CommitAuthority, 1 => CommitDirectorate, 3 => CommitLeftBehind, 4 => CommitExpedition, _ => 0,
    };
}
