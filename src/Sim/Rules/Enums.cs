// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
namespace Rts.Sim.Rules;

/// <summary>Movement classes (D-023, D-035). Append only: values are persisted.</summary>
public enum Locomotion : byte { Foot = 0, Wheeled = 1, Tracked = 2, Hover = 3, Air = 4, Burrow = 5 }

/// <summary>Five armour classes for the warhead × armour table (design 02 §4). Append only.</summary>
public enum ArmorClass : byte { Unarmored = 0, Light = 1, Heavy = 2, Structure = 3, Fortified = 4 }

/// <summary>Land types; the terrain table maps land × locomotion to speed %. Append only.</summary>
public enum LandType : byte { Clear = 0, Road = 1, Rough = 2, Ash = 3, Water = 4, Rock = 5, Cliff = 6 }

/// <summary>What a map cell's overlay holds. Append only.</summary>
public enum OverlayKind : byte { None = 0, Ashfall = 1, Pitchblende = 2, Wall = 3 }

/// <summary>What a production queue builds (M2). Append only: values are persisted.</summary>
public enum ProductionCategory : byte { Infantry = 0, Vehicle = 1, Aircraft = 2, Structure = 3, Defence = 4 }

/// <summary>Counts for sizing tables.</summary>
public static class RuleCounts
{
    public const int Locomotions = 6;
    public const int ArmorClasses = 5;
    public const int LandTypes = 7;
}
