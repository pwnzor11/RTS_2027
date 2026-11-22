// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
namespace Rts.Data;

/// <summary>
/// S29 (BAL-S29): optional per-faction AI build levers read from <c>[faction.&lt;id&gt;]</c> of data/ai/difficulty.toml. Profile values only (no AI state, nothing to save).
/// The defaults equal the former <c>TierPlanner</c> constants, so a faction without the keys plays exactly as before.
/// </summary>
/// <param name="ArmyBeforeTech">Army size before the research structure and the tier-2 gate are added (<c>army_before_tech</c>).</param>
/// <param name="ArmyBeforeAdapt">Army size before AdaptBuild adds production or defence (<c>army_before_adapt</c>).</param>
/// <param name="AdaptCash">Funds at which AdaptBuild adds a barracks / factory (<c>adapt_cash</c>).</param>
/// <param name="AdaptMaxProduction">Most barracks and most factories AdaptBuild grows to (<c>adapt_max_production</c>).</param>
/// <param name="Tier2GateTick">Tick from which the tier-2 gate is added (<c>tier2_gate_tick</c>).</param>
/// <param name="AdaptBuild">0 or 1 forces the AdaptBuild flag for this faction at every tier; -1 (key missing) keeps the tier's own flag (<c>adapt_build</c>; only the tier-5 table sets it).</param>
public sealed record FactionTuning(int ArmyBeforeTech, int ArmyBeforeAdapt, int AdaptCash, int AdaptMaxProduction, int Tier2GateTick, int AdaptBuild = -1)
{
    /// <summary>The former constants: 30, 30, 1500, 2, 3600.</summary>
    public static FactionTuning Default { get; } = new(30, 30, 1500, 2, 3600, -1);

    // s39 (M13-38): rich-side production growth (TierPlanner.AdaptBuild); 0 = off, so a missing key plays as before.
    /// <summary>Funds (cash + stored) at which AdaptBuild skips the army gate and grows production to <see cref="AdaptRichMax"/>; 0 = off (<c>adapt_rich_cash</c>).</summary>
    public int AdaptRichCash { get; init; } = 0;
    /// <summary>The most barracks and factories a rich AI grows to (<c>adapt_rich_max</c>; only read while <see cref="AdaptRichCash"/> is set).</summary>
    public int AdaptRichMax { get; init; } = 3;

    // s39 (M13-37): Directorate Bore levers (DirectorateTactics); the defaults are the former constants, so a missing key plays as before.
    /// <summary>Bores bought over a whole game; 0 = the AI never buys one (<c>bore_max</c>).</summary>
    public int BoreMax { get; init; } = 5;
    /// <summary>Army size before the first Bore is bought (<c>bore_army</c>).</summary>
    public int BoreArmy { get; init; } = 4;
    /// <summary>Ticks between Bore purchases (<c>bore_gap_ticks</c>).</summary>
    public int BoreGapTicks { get; init; } = 1500;
    /// <summary>Cells short of the enemy target at which a forward Hatch is dug (<c>bore_dig_short</c>).</summary>
    public int BoreDigShort { get; init; } = 15;
    /// <summary>Where Hatches go: 0 alternate enemy side / ore field, 1 ore field only, 2 enemy side only (<c>bore_sites</c>).</summary>
    public int BoreSites { get; init; } = 0;
    /// <summary>Funds (cash + stored) needed beyond the Bore's cost before one is bought (<c>bore_min_cash</c>; the built-in cushion is 200).</summary>
    public int BoreMinCash { get; init; } = 200;
}
