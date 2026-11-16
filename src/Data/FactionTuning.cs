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
public sealed record FactionTuning(int ArmyBeforeTech, int ArmyBeforeAdapt, int AdaptCash, int AdaptMaxProduction, int Tier2GateTick)
{
    /// <summary>The former constants: 30, 30, 1500, 2, 3600.</summary>
    public static FactionTuning Default { get; } = new(30, 30, 1500, 2, 3600);
}
