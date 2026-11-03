// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Sim.Rules;

namespace Rts.Sim.Core;

/// <summary>Read-only view of sim state for the renderer, taken after each Step. Never fed back into the sim.
/// Land is row-major (y * MapWidth + x). StateHash is the latest full hash (0 until the first one).
/// Viewer = the player whose view this is (-1 = omniscient debug view). Vision is per cell, row-major:
/// 0 = shroud (never seen), 1 = fog (seen before), 2 = visible now; empty when Viewer = -1.
/// With a viewer, enemy units outside Vision 2 are omitted and enemy buildings outside it are last-seen ghosts.</summary>
public sealed record SimSnapshot(int Tick, int MapWidth, int MapHeight, IReadOnlyList<UnitView> Units,
    IReadOnlyList<LandType> Land, ulong StateHash, IReadOnlyList<BuildingView> Buildings, int Viewer,
    IReadOnlyList<byte> Vision);

/// <summary>One building as the renderer sees it. Cell = top-left of its footprint. Ghost = last-seen
/// image of an enemy building now under fog (Health is the last-seen value).</summary>
public readonly record struct BuildingView(EntityId Id, byte Player, int TypeIndex, CellCoord Cell, int FootprintW,
    int FootprintH, int Health, bool Ghost);

/// <summary>What kind of actor a UnitView is.</summary>
public enum UnitKind : byte { Vehicle = 0, Infantry = 1 }

/// <summary>One unit as the renderer sees it. Facing is 0..255 (0 = north, clockwise). TypeIndex indexes
/// the list for its Kind in SimRules.</summary>
public readonly record struct UnitView(EntityId Id, UnitKind Kind, byte Player, int TypeIndex, LeptonCoord Position,
    byte Facing, int Health);

/// <summary>Everything needed to start a deterministic game. RulesHash covers the rules; the map hash is
/// computed by the sim from Map. Init-only extras (M1.5): Teams = one team id per player slot (null = every
/// player on its own team; <see cref="Core.Teams.Neutral"/> marks a neutral slot); Limits = store capacities;
/// AllowDebug = whether Debug commands take effect. All three are part of the game's identity (saved, hashed
/// into the "players" part, written to replay headers).</summary>
public sealed record SimSetup(ulong Seed, Map.MapData Map, int PlayerCount, SimRules Rules, ulong RulesHash)
{
    public IReadOnlyList<byte>? Teams { get; init; }
    public SimLimits Limits { get; init; } = SimLimits.Default;
    public bool AllowDebug { get; init; }

    /// <summary>Optional scripting/mission extensions (M2-15, D-061), run in list order from the trigger phase of every Step.
    /// Their state parts are appended after the built-in parts, so they are saved and hashed. One extension instance
    /// serves one Simulation: build a fresh list for each Simulation you construct. Not part of the save header.</summary>
    public IReadOnlyList<ISimExtension>? Extensions { get; init; }

    /// <summary>Team id of a player slot (the slot index itself when Teams is null).</summary>
    public byte TeamOf(byte player) => Teams != null && player < Teams.Count ? Teams[player] : player;
}
