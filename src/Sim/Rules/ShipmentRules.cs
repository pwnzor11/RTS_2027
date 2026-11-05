// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
namespace Rts.Sim.Rules;

/// <summary>
/// Home shipment constants (game.toml <c>[shipments]</c>, docs/design/11_m7_plan.md §2.3 and docs/design/07_expedition.md §2.2, D-126).
/// A shipment of <see cref="Value"/> credits sails in every <see cref="IntervalTicks"/>; each shipment pays
/// <c>Value x (1000 - decay) / 1000</c> and never less than <see cref="FloorPermille"/> of the base value. <see cref="Enabled"/> defaults to
/// false, so rules sets without a <c>[shipments]</c> table behave exactly as before. Behaviour in Simulation.Shipments.cs (M7-06); it also needs a <see cref="TenderType"/>.
/// </summary>
public sealed record ShipmentRules
{
    /// <summary>Master switch (TOML <c>enabled</c>, default false). Off: no shipment is ever scheduled.</summary>
    public bool Enabled { get; init; }

    /// <summary>Ticks between shipments (TOML <c>interval_ticks</c>).</summary>
    public int IntervalTicks { get; init; } = 1800;

    /// <summary>Credits of an undecayed shipment (TOML <c>value</c>).</summary>
    public int Value { get; init; } = 1500;

    /// <summary>Permille the shipment shrinks per decay step (TOML <c>decay_permille</c>).</summary>
    public int DecayPermille { get; init; } = 40;

    /// <summary>Lowest shipment value, permille of <see cref="Value"/> (TOML <c>floor_permille</c>).</summary>
    public int FloorPermille { get; init; } = 350;

    /// <summary>Ticks a held pit or ash field must stay held to win back one decay step (TOML <c>charter_ticks</c>).</summary>
    public int CharterTicks { get; init; } = 1200;

    /// <summary>Most tenders (shipment ships) in flight per player (TOML <c>tenders_max</c>).</summary>
    public int TendersMax { get; init; } = 3;

    /// <summary>Balance knob: extra permille on local ash income for the faction (TOML <c>local_ash_bonus_permille</c>, default 0).</summary>
    public int LocalAshBonusPermille { get; init; }

    /// <summary>Id of the unarmed naval vehicle that carries a shipment (TOML <c>tender_type</c>, default empty = no shipments even when
    /// <see cref="Enabled"/>). Validated against the vehicle table when rules load (M7-06).</summary>
    public string TenderType { get; init; } = "";

    /// <summary>The defaults: disabled, design values.</summary>
    public static ShipmentRules Default { get; } = new();
}
