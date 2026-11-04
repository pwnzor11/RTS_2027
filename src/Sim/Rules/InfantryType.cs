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
}
