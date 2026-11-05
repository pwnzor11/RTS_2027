// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
namespace Rts.Sim.Rules;

/// <summary>Sim-side vehicle type. Speed leptons/tick; TurnRate facing steps/tick; Sight in cells;
/// weapon fields are indexes into SimRules.Weapons or -1.</summary>
public sealed record VehicleType(string Id, Locomotion Locomotion, int Speed, int TurnRate, int MaxHealth,
    ArmorClass Armor, int Sight, int PrimaryWeapon, bool HasTurret, int TurretTurnRate, bool Crusher, int Cost,
    IReadOnlyList<string> Prerequisites)
{
    /// <summary>Credits a harvester carries (0 = not a harvester).</summary>
    public int HarvestCapacity { get; init; }

    /// <summary>Build time in ticks; 0 = derive from cost.</summary>
    public int BuildTime { get; init; }

    // ---- schema v3 (M3-01, design 05 §1–§4). Defaults keep pre-M3 behaviour. -------------------------

    /// <summary>Owning faction (TOML <c>faction</c>; default from the id prefix <c>auth_</c>/<c>dir_</c>, else Authority).</summary>
    public Faction Faction { get; init; }

    /// <summary>Tunnel size class (TOML <c>tunnel_size</c> none|S|M|L; vehicles default None). None = cannot use the network.</summary>
    public TunnelSize TunnelSize { get; init; }

    /// <summary>Aura radius in cells (0 = no aura). The aura applies <see cref="AuraRofPermille"/> and
    /// <see cref="AuraSpeedPermille"/> to friendly actors whose type matches <see cref="AuraTargets"/> (M3-06).</summary>
    public int AuraRadius { get; init; }

    /// <summary>Rate-of-fire multiplier given by the aura, per mille of the reload time (1000 = unchanged, 800 = 20 % faster).</summary>
    public int AuraRofPermille { get; init; } = 1000;

    /// <summary>Speed multiplier given by the aura, per mille (1000 = unchanged).</summary>
    public int AuraSpeedPermille { get; init; } = 1000;

    /// <summary>Type ids (or <c>class:vehicle</c>/<c>class:infantry</c>) the aura affects; empty = none.</summary>
    public IReadOnlyList<string> AuraTargets { get; init; } = Array.Empty<string>();

    /// <summary>Radiation resistance per mille: damage is scaled by (1000 - this)/1000; 1000 = immune, 0 = full damage.</summary>
    public int RadResistPermille { get; init; }

    /// <summary>Reactor-cored: takes the warhead's <see cref="WarheadType.BonusVsIrradiatedPercent"/>.</summary>
    public bool Irradiated { get; init; }

    /// <summary>Becomes invisible to enemies after <see cref="StealthRules.DelayTicks"/> without moving or firing.</summary>
    public bool Stealth { get; init; }

    /// <summary>Weapon index fired at the unit's own cell when it dies, or -1 (TOML <c>death_weapon</c>, a weapon id).</summary>
    public int DeathWeapon { get; init; } = -1;

    /// <summary>Static Broadcast radius in cells: hides those cells on enemy minimaps (view only; 0 = none).</summary>
    public int JamRadius { get; init; }

    /// <summary>Seismograph radius in cells: reveals enemy burrowed units and telegraphs to the owner (0 = none).</summary>
    public int SeismicRadius { get; init; }

    /// <summary>Infantry capacity as a transport (0 = not a transport). Enter loads, Deploy unloads (M3-06).</summary>
    public int Passengers { get; init; }

    /// <summary>Building type index this vehicle deploys into, or -1 (TOML <c>deploys_into</c>, a building id).</summary>
    public int DeploysInto { get; init; } = -1;

    /// <summary>M4: tech tier 1..3 (TOML <c>tier</c>, default 1).</summary>
    public int Tier { get; init; } = 1;

    // ---- schema v5 (M6-02, docs/design/10_m6_plan.md §3.2). Every default is "off". -------------------

    /// <summary>Packed rig: building type index it unpacks into on a free footprint, or -1 (TOML <c>unpacks_into</c>, a building id).</summary>
    public int UnpacksInto { get; init; } = -1;

    /// <summary>Ticks the unpacking takes (TOML <c>pack_ticks</c>, 0 = off).</summary>
    public int PackTicks { get; init; }

    /// <summary>Ash drained from a wreck per 10 ticks (TOML <c>salvage_rate</c>, 0 = cannot salvage).</summary>
    public int SalvageRate { get; init; }

    /// <summary>Raider: Enter on an enemy Silo/Refinery steals ash; kills of harvesters pay spoils (TOML <c>raider</c>).</summary>
    public bool Raider { get; init; }

    /// <summary>Raider consumed by a successful plunder instead of withdrawing (TOML <c>raider_one_use</c>, default false).</summary>
    public bool RaiderOneUse { get; init; }

    /// <summary>Blackout ticks laid on its own cell while moving (TOML <c>drops_blackout_ticks</c>, 0 = off; Dust Sail).</summary>
    public int DropsBlackoutTicks { get; init; }

    /// <summary>Repair aura: permille of max HP per 10 ticks on own vehicles and rigs within <see cref="AuraRadius"/> (TOML <c>aura_repair_permille</c>, 0 = off).</summary>
    public int AuraRepairPermille { get; init; }

    /// <summary>Speed aura applying only to packed rigs, permille bonus (TOML <c>tow_bonus_permille</c>, 0 = off; Tow Truck).</summary>
    public int TowBonusPermille { get; init; }

    // ---- schema v6 (VET-1, docs/design/14_veterancy.md §3). ---------------------------------------------

    /// <summary>Explicit TOML <c>veteran</c> flag; null = not set (then armed types rank, unarmed ones do not).</summary>
    public bool? Veteran { get; init; }

    /// <summary>True if this type earns veterancy ranks: the <c>veteran</c> flag, else whether it has a primary weapon.</summary>
    public bool CanRank => Veteran ?? PrimaryWeapon >= 0;

    /// <summary>Scales the veterancy thresholds of this type, permille (effect attribute <c>vet_threshold_permille</c>; 1000 = unchanged).</summary>
    public int VetThresholdPermille { get; init; } = 1000;
}
