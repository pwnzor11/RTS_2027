// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
namespace Rts.Sim.Rules;

/// <summary>
/// Supply cap constants (game.toml <c>[supply]</c>, docs/design/13_supply_cap.md, D-125). Every unit costs supply (bands of its cost,
/// or the per-type <c>supply</c> override); the cap is the sum of <c>supply_provided</c> over the owner's completed, unpacked, alive
/// buildings, limited by <see cref="Ceiling"/>. <see cref="Enabled"/> defaults to false, so rules sets without a <c>[supply]</c> table
/// behave exactly as before.
/// </summary>
public sealed record SupplyRules
{
    /// <summary>Master switch (TOML <c>enabled</c>, default false). Off: nothing is counted and nothing is blocked.</summary>
    public bool Enabled { get; init; }

    /// <summary>Hard cap on the supply cap (TOML <c>ceiling</c>).</summary>
    public int Ceiling { get; init; } = 200;

    /// <summary>Largest cost of band 1 (TOML <c>band1_max</c>): cost 0..band1_max uses <c>band_values[0]</c>.</summary>
    public int Band1Max { get; init; } = 400;

    /// <summary>Largest cost of band 2 (TOML <c>band2_max</c>); above it band 3 applies.</summary>
    public int Band2Max { get; init; } = 900;

    /// <summary>Supply per band 1..3 (TOML <c>band_values</c>).</summary>
    public IReadOnlyList<int> BandValues { get; init; } = new[] { 1, 2, 3 };

    /// <summary>Shipment gaps before an Expedition store counts as unstocked (TOML <c>stock_intervals</c>; parsed only, no behaviour yet).</summary>
    public int StockIntervals { get; init; } = 3;

    /// <summary>Supply of a unit: the per-type override when it is not negative, else the band of its cost.</summary>
    public int SupplyOf(int cost, int overrideValue)
    {
        if (overrideValue >= 0) return overrideValue;
        return cost <= Band1Max ? BandValues[0] : cost <= Band2Max ? BandValues[1] : BandValues[2];
    }

    /// <summary>The defaults: disabled, design values.</summary>
    public static SupplyRules Default { get; } = new();
}
