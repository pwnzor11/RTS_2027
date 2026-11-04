// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
namespace Rts.Sim.Rules;

/// <summary>Movement classes (D-023, D-035). Append only: values are persisted.</summary>
public enum Locomotion : byte { Foot = 0, Wheeled = 1, Tracked = 2, Hover = 3, Air = 4, Burrow = 5 }

/// <summary>Five armour classes for the warhead × armour table (design 02 §4). Append only.</summary>
public enum ArmorClass : byte { Unarmored = 0, Light = 1, Heavy = 2, Structure = 3, Fortified = 4 }

/// <summary>Land types; the terrain table maps land × locomotion to speed %. Append only.
/// M4: Water now means deep water; Shallow (7) is a fordable shallow (TOML key <c>shallow</c>). Coast = 8 is reserved for M7.</summary>
public enum LandType : byte { Clear = 0, Road = 1, Rough = 2, Ash = 3, Water = 4, Rock = 5, Cliff = 6, Shallow = 7 }

/// <summary>What a map cell's overlay holds. Append only.</summary>
public enum OverlayKind : byte { None = 0, Ashfall = 1, Pitchblende = 2, Wall = 3 }

/// <summary>What a production queue builds (M2). Append only: values are persisted.</summary>
public enum ProductionCategory : byte { Infantry = 0, Vehicle = 1, Aircraft = 2, Structure = 3, Defence = 4, Upgrade = 5 }

/// <summary>The factions (design 05 §4, M3; Neutral Q-37). Every type has one (default from its id prefix); every player has one
/// (<see cref="Core.SimSetup.Factions"/>). Neutral is the passive owner of ruins and map features: never a playable faction
/// (<see cref="RuleCounts.PlayableFactions"/>), no start kit, no AI. Append only: values are persisted.</summary>
public enum Faction : byte { Authority = 0, Directorate = 1, Neutral = 2 }

/// <summary>Tunnel size class of a mobile type, or the largest size a building entrance admits (design 05 §1).
/// S costs <see cref="TunnelRules.PointsS"/>, M <see cref="TunnelRules.PointsM"/>; L never uses the network.
/// Ordered so that "fits" is <c>unit &lt;= entrance</c> (with None meaning "cannot use / not an entrance"). Append only.</summary>
public enum TunnelSize : byte { None = 0, S = 1, M = 2, L = 3 }

/// <summary>What a weapon may target (design 05 §4, air). Append only.</summary>
public enum WeaponTargets : byte { Ground = 0, Air = 1, Both = 2 }

/// <summary>Counts for sizing tables.</summary>
public static class RuleCounts
{
    public const int Locomotions = 6;
    public const int ArmorClasses = 5;
    public const int LandTypes = 8;
    /// <summary>Number of <see cref="ProductionCategory"/> values (M3: Upgrade appended).</summary>
    public const int ProductionCategories = 6;
    /// <summary>Number of <see cref="Faction"/> values.</summary>
    public const int Factions = 3;
    /// <summary>Factions a player may pick (Authority, Directorate): everything below <see cref="Faction.Neutral"/>. Start kits exist only for these.</summary>
    public const int PlayableFactions = 2;
    /// <summary>Number of <see cref="TunnelSize"/> values.</summary>
    public const int TunnelSizes = 4;
    /// <summary>Most upgrades a rules set may define: the per-player mask is a <c>ulong</c>.</summary>
    public const int MaxUpgrades = 64;
}
