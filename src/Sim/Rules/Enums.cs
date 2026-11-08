// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
namespace Rts.Sim.Rules;

/// <summary>Movement classes (D-023, D-035). M7-02: Naval = 6 (ships; deep and shallow water, never land). Append only: values are persisted.</summary>
public enum Locomotion : byte { Foot = 0, Wheeled = 1, Tracked = 2, Hover = 3, Air = 4, Burrow = 5, Naval = 6 }

/// <summary>Armour classes for the warhead × armour table (design 02 §4). M7-02 appends ShipLight, ShipHeavy and Submerged (warhead <c>versus</c> lists shorter than <see cref="RuleCounts.ArmorClasses"/> pad the new classes with 100). Append only.</summary>
public enum ArmorClass : byte { Unarmored = 0, Light = 1, Heavy = 2, Structure = 3, Fortified = 4, ShipLight = 5, ShipHeavy = 6, Submerged = 7 }

/// <summary>Land types; the terrain table maps land × locomotion to speed %. Append only.
/// M4: Water now means deep water; Shallow (7) is a fordable shallow (TOML key <c>shallow</c>). M7-02: Coast = 8 is the beach (TOML key <c>coast</c>, <c>landing = true</c>).</summary>
public enum LandType : byte { Clear = 0, Road = 1, Rough = 2, Ash = 3, Water = 4, Rock = 5, Cliff = 6, Shallow = 7, Coast = 8 }

/// <summary>What a map cell's overlay holds. Append only. M12-04 (map format 4, D-180) adds the static blocking props after
/// <see cref="Wall"/>: <see cref="Tree"/> (dead tree), <see cref="Wreck"/>, <see cref="Block"/> (barrier or concrete block),
/// <see cref="Pylon"/> and <see cref="Snag"/> (driftwood or debris). A prop blocks ground units like a wall, never sight or shots,
/// cannot be destroyed, and its density byte is the sprite variant (see <see cref="OverlayKinds"/>).</summary>
public enum OverlayKind : byte { None = 0, Ashfall = 1, Pitchblende = 2, Wall = 3, Tree = 4, Wreck = 5, Block = 6, Pylon = 7, Snag = 8 }

/// <summary>What a production queue builds (M2). Append only: values are persisted.</summary>
public enum ProductionCategory : byte { Infantry = 0, Vehicle = 1, Aircraft = 2, Structure = 3, Defence = 4, Upgrade = 5 }

/// <summary>The factions (design 05 §4, M3; Neutral Q-37). Every type has one (default from its id prefix); every player has one
/// (<see cref="Core.SimSetup.Factions"/>). M6-02: LeftBehind = 3 is appended after Neutral (Neutral keeps 2). M7-14: Expedition = 4 (The Overseas Expedition) is playable and in <see cref="RuleCounts.PlayableOrder"/>. Neutral is the passive owner of ruins and map features: never a playable faction
/// (<see cref="RuleCounts.PlayableFactions"/>), no start kit, no AI. Append only: values are persisted.</summary>
public enum Faction : byte { Authority = 0, Directorate = 1, Neutral = 2, LeftBehind = 3, Expedition = 4 }

/// <summary>Tunnel size class of a mobile type, or the largest size a building entrance admits (design 05 §1).
/// S costs <see cref="TunnelRules.PointsS"/>, M <see cref="TunnelRules.PointsM"/>; L never uses the network.
/// Ordered so that "fits" is <c>unit &lt;= entrance</c> (with None meaning "cannot use / not an entrance"). Append only.</summary>
public enum TunnelSize : byte { None = 0, S = 1, M = 2, L = 3 }

/// <summary>What a weapon may target (design 05 §4, air). M7-02 (D-124): <c>Ground</c> means land units, structures <b>and surface ships</b>
/// (existing weapons hit boats, no data change); <c>Naval</c> hits surface ships only, <c>Submerged</c> hits submerged vessels only,
/// <c>NavalAndSubmerged</c> both. Append only.</summary>
public enum WeaponTargets : byte { Ground = 0, Air = 1, Both = 2, Naval = 3, Submerged = 4, NavalAndSubmerged = 5 }

/// <summary>Counts for sizing tables.</summary>
public static class RuleCounts
{
    public const int Locomotions = 7;
    public const int ArmorClasses = 8;
    public const int LandTypes = 9;
    /// <summary>Number of <see cref="ProductionCategory"/> values (M3: Upgrade appended).</summary>
    public const int ProductionCategories = 6;
    /// <summary>Number of <see cref="Faction"/> values.</summary>
    public const int Factions = 5;
    /// <summary>How many factions the skirmish setup offers (M6-09: Authority, Directorate, Left Behind). A COUNT, not a bound on the enum:
    /// <see cref="Faction.Neutral"/> sits between Directorate and Left Behind, so test a faction with <see cref="IsPlayable"/> and walk
    /// <see cref="PlayableOrder"/>, never <c>(int)f &lt; PlayableFactions</c>.</summary>
    public const int PlayableFactions = 4;   // M7-14: Expedition joins Authority, Directorate, Left Behind

    /// <summary>The factions a player slot may hold, in the order the skirmish setup cycles them (M6-09).</summary>
    public static readonly IReadOnlyList<Faction> PlayableOrder = new[] { Faction.Authority, Faction.Directorate, Faction.LeftBehind, Faction.Expedition };

    /// <summary>True for every faction a player slot may hold: all except <see cref="Faction.Neutral"/> (M6-02). M7-14: Expedition is playable.</summary>
    public static bool IsPlayable(Faction f) => f != Faction.Neutral && (int)f < Factions;
    /// <summary>Number of <see cref="TunnelSize"/> values.</summary>
    public const int TunnelSizes = 4;
    /// <summary>Most upgrades a rules set may define: the per-player mask is a <c>ulong</c>.</summary>
    public const int MaxUpgrades = 64;
}
