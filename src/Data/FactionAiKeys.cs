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
/// <param name="AaDirectorate">s29: <c>aa_needs_air</c> for Directorate (0 = off, 1 = no anti-air-only unit before enemy aircraft were seen).</param>
/// <param name="AaAuthority">Same for Authority.</param>
/// <param name="AaLeftBehind">Same for LeftBehind.</param>
/// <param name="AaExpedition">Same for Expedition.</param>
/// <param name="RetreatFloorDirectorate">s30: <c>retreat_floor_pct</c> for Directorate (-1 = built-in: 40 for the Expedition, none for the others).</param>
/// <param name="RetreatFloorAuthority">Same for Authority.</param>
/// <param name="RetreatFloorLeftBehind">Same for LeftBehind.</param>
/// <param name="RetreatFloorExpedition">Same for Expedition.</param>
/// <param name="HuntDirectorate">s30: <c>mopup_hunt</c> for Directorate (0 = off, 1 = the mop-up hunts a relocating base).</param>
/// <param name="HuntAuthority">Same for Authority.</param>
/// <param name="HuntLeftBehind">Same for LeftBehind.</param>
/// <param name="HuntExpedition">Same for Expedition.</param>
public sealed record FactionAiKeys(int HarassDirectorate, int HarassAuthority, int HarassLeftBehind, int HarassExpedition,
    int CommitDirectorate = 0, int CommitAuthority = 0, int CommitLeftBehind = 0, int CommitExpedition = 0,
    int AaDirectorate = 0, int AaAuthority = 0, int AaLeftBehind = 0, int AaExpedition = 0,
    int RetreatFloorDirectorate = -1, int RetreatFloorAuthority = -1, int RetreatFloorLeftBehind = -1, int RetreatFloorExpedition = -1,
    int HuntDirectorate = 0, int HuntAuthority = 0, int HuntLeftBehind = 0, int HuntExpedition = 0)
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

    /// <summary>s29 <c>siege_commit_ratio</c> (percent, 0 = off): a wave whose army is worth at least this percent of the enemy's seen army plus known defences is never called off by the health or predicted-loss retreat (<paramref name="faction"/> as in <see cref="HarassMinArmy"/>).</summary>
    public int CommitRatio(int faction) => faction switch
    {
        0 => CommitAuthority, 1 => CommitDirectorate, 3 => CommitLeftBehind, 4 => CommitExpedition, _ => 0,
    };

    /// <summary>s29/s30 <c>aa_needs_air</c> (0/1/2) for a faction: 0 off; 1 the unit bags skip vehicles whose gun only hits aircraft until an enemy aircraft has
    /// been seen; 2 (s30, softer) the same, but one such vehicle may be alive (or queued) at a time.</summary>
    public int AaMode(int faction) => faction switch
    {
        0 => AaAuthority, 1 => AaDirectorate, 3 => AaLeftBehind, 4 => AaExpedition, _ => 0,
    };

    /// <summary>s29 <c>aa_needs_air</c>: true when the key is set (1 or 2) for the faction.</summary>
    public bool AaNeedsAir(int faction) => AaMode(faction) > 0;

    /// <summary>s30 <c>retreat_floor_pct</c>: the health percent below which the faction's waves never fight, or -1 for the built-in floor
    /// (<c>ExpeditionTactics.RetreatPct</c> for the Expedition, none for the others).</summary>
    public int RetreatFloor(int faction) => faction switch
    {
        0 => RetreatFloorAuthority, 1 => RetreatFloorDirectorate, 3 => RetreatFloorLeftBehind, 4 => RetreatFloorExpedition, _ => -1,
    };

    /// <summary>s30 <c>mopup_hunt</c> (0/1): when 1 the faction's mop-up also hunts a relocating base (a Mother Rig that packs and unpacks elsewhere):
    /// packed rigs no longer count as an enemy army, former enemy structure sites and the last-seen rig cell are swept first, and stale blocks are revisited sooner.</summary>
    public bool MopupHunt(int faction) => faction switch
    {
        0 => HuntAuthority, 1 => HuntDirectorate, 3 => HuntLeftBehind, 4 => HuntExpedition, _ => 0,
    } == 1;
}
