// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
namespace Rts.AI.Recruit;

/// <summary>
/// S12 (M7-18): the Expedition AI's own per-tier numbers and flags. The generic tier ladder (data/ai/difficulty.toml) is shared with the other
/// factions and tuned elsewhere; the Expedition needs a different curve (few, dear units; ships and carriers instead of a big land army), so the tactics read
/// this table and test the flags, never the tier number. Integers only. Every number is <c>(tune)</c>.
/// </summary>
/// <param name="Fleet">Ships alive and queued before the navy stops growing.</param>
/// <param name="Cruisers">Cruisers wanted (0 = none).</param>
/// <param name="Carriers">Carriers wanted (0 = none).</param>
/// <param name="Escorts">Ships that stand guard at the dock while a shipment is due or a tender sails.</param>
/// <param name="SubHunt">Launches are bought and sent against enemy submarines.</param>
/// <param name="SortieBuildings">A full carrier also launches against the nearest known enemy building, not only against enemies near the carrier.</param>
/// <param name="WaveBonus">Extra units every land wave waits for.</param>
internal readonly record struct ExpeditionPlan(int Fleet, int Cruisers, int Carriers, int Escorts, bool SubHunt, bool SortieBuildings, int WaveBonus)
{
    private static readonly ExpeditionPlan[] Tiers =
    [
        new(Fleet: 3, Cruisers: 0, Carriers: 0, Escorts: 1, SubHunt: false, SortieBuildings: false, WaveBonus: 2),   // tier 1
        new(Fleet: 3, Cruisers: 0, Carriers: 0, Escorts: 1, SubHunt: true, SortieBuildings: false, WaveBonus: 2),    // tier 2
        new(Fleet: 4, Cruisers: 1, Carriers: 0, Escorts: 2, SubHunt: true, SortieBuildings: false, WaveBonus: 2),    // tier 3
        new(Fleet: 5, Cruisers: 1, Carriers: 1, Escorts: 2, SubHunt: true, SortieBuildings: true, WaveBonus: 2),     // tier 4
        new(Fleet: 5, Cruisers: 2, Carriers: 1, Escorts: 2, SubHunt: true, SortieBuildings: true, WaveBonus: 2),     // tier 5
    ];

    /// <summary>The plan for a profile tier 1..5 (clamped).</summary>
    public static ExpeditionPlan For(int tier) => Tiers[Math.Clamp(tier, 1, Tiers.Length) - 1];
}
