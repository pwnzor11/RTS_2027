// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Sim.Rules;

namespace Rts.Sim.Core;

/// <summary>Read-only view of sim state for the renderer, taken after each Step. Never fed back into the sim.
/// Land is row-major (y * MapWidth + x). StateHash is the latest full hash (0 until the first one).
/// Viewer = the player whose view this is (-1 = omniscient debug view). Vision is per cell, row-major:
/// 0 = shroud (never seen), 1 = fog (seen before), 2 = visible now; empty when Viewer = -1.
/// With a viewer, enemy units outside Vision 2 are omitted and enemy buildings outside it are last-seen ghosts.
/// FxEvents = detonations and deaths of the last Step (null or empty when none; with a viewer only those on visible cells).</summary>
public sealed record SimSnapshot(int Tick, int MapWidth, int MapHeight, IReadOnlyList<UnitView> Units,
    IReadOnlyList<LandType> Land, ulong StateHash, IReadOnlyList<BuildingView> Buildings, int Viewer,
    IReadOnlyList<byte> Vision, IReadOnlyList<FxEvent>? FxEvents = null)
{
    /// <summary>M3: surfacing telegraphs the viewer may see (all of them when Viewer = -1). Never null.</summary>
    public IReadOnlyList<TelegraphView> Telegraphs { get; init; } = Array.Empty<TelegraphView>();

    /// <summary>M3: per-cell ground layers (row-major, <see cref="GroundLayers"/>) as the viewer may know them; empty when the
    /// whole map is clear. See <see cref="Simulation.FillGroundLayers"/> for the fog rules.</summary>
    public IReadOnlyList<byte> Ground { get; init; } = Array.Empty<byte>();
}

/// <summary>
/// Bit layout of one byte of <see cref="SimSnapshot.Ground"/> (M3). Radiation is quantised to 4 bits (level / 16), enough for a
/// tint; exact levels are on <see cref="View.PlayerView.RadiationAt"/>.
/// </summary>
public static class GroundLayers
{
    /// <summary>Mask of the radiation tint (level / 16, 0..15).</summary>
    public const byte RadiationMask = 0x0F;
    /// <summary>A Blackout cloud covers the cell.</summary>
    public const byte Blackout = 0x10;
    /// <summary>The cell is jammed on this viewer's minimap (enemy Static Broadcast).</summary>
    public const byte Jammed = 0x20;
    /// <summary>The cell is hardened by any team (known to the viewer).</summary>
    public const byte Hardened = 0x40;
}

/// <summary>One building as the renderer sees it. Cell = top-left of its footprint. Ghost = last-seen
/// image of an enemy building now under fog (Health is the last-seen value).</summary>
public readonly record struct BuildingView(EntityId Id, byte Player, int TypeIndex, CellCoord Cell, int FootprintW,
    int FootprintH, int Health, bool Ghost)
{
    /// <summary>M4: soldiers garrisoned inside. Exact for own and allied buildings (and the omniscient view); for an enemy building
    /// only while it is visible now; always 0 on a ghost.</summary>
    public int Occupants { get; init; }
}

/// <summary>What kind of actor a UnitView is.</summary>
public enum UnitKind : byte { Vehicle = 0, Infantry = 1 }

/// <summary>One unit as the renderer sees it. Facing is 0..255 (0 = north, clockwise). TypeIndex indexes
/// the list for its Kind in SimRules.</summary>
public readonly record struct UnitView(EntityId Id, UnitKind Kind, byte Player, int TypeIndex, LeptonCoord Position,
    byte Facing, int Health)
{
    /// <summary>M3: underground (draw dimmed / as a tunnel marker for the owner; enemies see it only via a Seismograph).</summary>
    public bool Underground { get; init; }

    /// <summary>M3: stealthed (draw translucent for the owner; enemies see it only when revealed).</summary>
    public bool Stealthed { get; init; }
}

/// <summary>Everything needed to start a deterministic game. RulesHash covers the rules; the map hash is
/// computed by the sim from Map. Init-only extras (M1.5): Teams = one team id per player slot (null = every
/// player on its own team; <see cref="Core.Teams.Neutral"/> marks a neutral slot); Limits = store capacities;
/// AllowDebug = whether Debug commands take effect. All three are part of the game's identity (saved, hashed
/// into the "players" part, written to replay headers).</summary>
public sealed record SimSetup(ulong Seed, Map.MapData Map, int PlayerCount, SimRules Rules, ulong RulesHash)
{
    public IReadOnlyList<byte>? Teams { get; init; }

    /// <summary>Faction per player slot (M3-01); null = every player Authority (the pre-M3 behaviour). Part of the game's
    /// identity (save/replay header, "players" part). The skirmish start kit per faction comes from SimRules.StartKits.</summary>
    public IReadOnlyList<Faction>? Factions { get; init; }

    /// <summary>Faction of a player slot (Authority when <see cref="Factions"/> is null or too short).</summary>
    public Faction FactionOf(byte player) => Factions != null && player < Factions.Count ? Factions[player] : Faction.Authority;
    public SimLimits Limits { get; init; } = SimLimits.Default;
    public bool AllowDebug { get; init; }

    /// <summary>Optional scripting/mission extensions (M2-15, D-061), run in list order from the trigger phase of every Step.
    /// Their state parts are appended after the built-in parts, so they are saved and hashed. One extension instance
    /// serves one Simulation: build a fresh list for each Simulation you construct. Not part of the save header.</summary>
    public IReadOnlyList<ISimExtension>? Extensions { get; init; }

    /// <summary>Team id of a player slot (the slot index itself when Teams is null).</summary>
    public byte TeamOf(byte player) => Teams != null && player < Teams.Count ? Teams[player] : player;
}
