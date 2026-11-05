// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
namespace Rts.Sim.Rules;

/// <summary>Sim-side infantry type (5 per cell, crushable by Crusher vehicles).</summary>
public sealed record InfantryType(string Id, int Speed, int MaxHealth, ArmorClass Armor, int Sight,
    int PrimaryWeapon, int Cost, IReadOnlyList<string> Prerequisites)
{
    /// <summary>Build time in ticks; 0 = derive from cost.</summary>
    public int BuildTime { get; init; }

    // ---- schema v3 (M3-01, design 05 §1–§4). Defaults keep pre-M3 behaviour; see VehicleType for the meanings. ----

    /// <summary>Owning faction (TOML <c>faction</c>; default from the id prefix <c>auth_</c>/<c>dir_</c>, else Authority).</summary>
    public Faction Faction { get; init; }

    /// <summary>Tunnel size class (TOML <c>tunnel_size</c>; infantry default S).</summary>
    public TunnelSize TunnelSize { get; init; } = TunnelSize.S;

    /// <summary>Aura radius in cells (0 = no aura).</summary>
    public int AuraRadius { get; init; }

    /// <summary>Aura rate-of-fire multiplier per mille of the reload time (1000 = unchanged).</summary>
    public int AuraRofPermille { get; init; } = 1000;

    /// <summary>Aura speed multiplier per mille (1000 = unchanged).</summary>
    public int AuraSpeedPermille { get; init; } = 1000;

    /// <summary>Type ids (or <c>class:vehicle</c>/<c>class:infantry</c>) the aura affects.</summary>
    public IReadOnlyList<string> AuraTargets { get; init; } = Array.Empty<string>();

    /// <summary>Radiation resistance per mille (1000 = immune, 0 = full damage).</summary>
    public int RadResistPermille { get; init; }

    /// <summary>Reactor-cored: takes the warhead's bonus vs irradiated.</summary>
    public bool Irradiated { get; init; }

    /// <summary>Becomes invisible to enemies after standing still (see <see cref="StealthRules"/>).</summary>
    public bool Stealth { get; init; }

    /// <summary>Weapon index fired at the soldier's cell when it dies, or -1.</summary>
    public int DeathWeapon { get; init; } = -1;

    /// <summary>Static Broadcast radius in cells (view only; 0 = none).</summary>
    public int JamRadius { get; init; }

    /// <summary>Seismograph radius in cells (0 = none).</summary>
    public int SeismicRadius { get; init; }

    /// <summary>Engineer: Enter on an enemy building below half health captures it, on a friendly one repairs it (M3-06).</summary>
    public bool Engineer { get; init; }

    // ---- schema v4 (M4-01, design 08 §3–§4). ---------------------------------------------------------

    /// <summary>Tech tier 1..3 (TOML <c>tier</c>, default 1).</summary>
    public int Tier { get; init; } = 1;

    /// <summary>May enter a garrisonable building (TOML <c>can_garrison</c>; default true, false for engineers unless set).</summary>
    public bool CanGarrison { get; init; } = true;

    // ---- schema v5 (M6-02, docs/design/10_m6_plan.md §3.2). Every default is "off". -------------------

    /// <summary>Ash drained from a wreck per 10 ticks (TOML <c>salvage_rate</c>, 0 = cannot salvage).</summary>
    public int SalvageRate { get; init; }

    /// <summary>Raider: Enter on an enemy Silo/Refinery steals ash; kills of harvesters pay spoils (TOML <c>raider</c>).</summary>
    public bool Raider { get; init; }

    /// <summary>Raider consumed by a successful plunder instead of withdrawing (TOML <c>raider_one_use</c>, default false).</summary>
    public bool RaiderOneUse { get; init; }

    /// <summary>Stealth delay in ticks on cells whose ash is above <see cref="AshStealthThreshold"/> (TOML <c>ash_stealth_delay_ticks</c>, 0 = off).</summary>
    public int AshStealthDelayTicks { get; init; }

    /// <summary>Ash level (0..255) above which <see cref="AshStealthDelayTicks"/> applies (TOML <c>ash_stealth_threshold</c>).</summary>
    public int AshStealthThreshold { get; init; }

    /// <summary>Repair aura: permille of max HP per 10 ticks on own vehicles and rigs within <see cref="AuraRadius"/> (TOML <c>aura_repair_permille</c>, 0 = off).</summary>
    public int AuraRepairPermille { get; init; }

    // ---- schema v6 (VET-1, docs/design/14_veterancy.md §3). ---------------------------------------------

    /// <summary>Explicit TOML <c>veteran</c> flag; null = not set (then armed types rank, unarmed ones do not).</summary>
    public bool? Veteran { get; init; }

    /// <summary>True if this type earns veterancy ranks: the <c>veteran</c> flag, else whether it has a primary weapon.</summary>
    public bool CanRank => Veteran ?? PrimaryWeapon >= 0;

    /// <summary>Scales the veterancy thresholds of this type, permille (effect attribute <c>vet_threshold_permille</c>; 1000 = unchanged).</summary>
    public int VetThresholdPermille { get; init; } = 1000;
}
