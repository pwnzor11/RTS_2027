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

    /// <summary>M3-21: superweapon buildings the viewer may know the charge of: its own (undefeated owner, live), with charge, ready
    /// flag and blast radius; all players' when Viewer = -1. Nobody else's charge is told (fog rule of Simulation.Superweapon.cs). Never null.</summary>
    public IReadOnlyList<SuperweaponView> Superweapons { get; init; } = Array.Empty<SuperweaponView>();
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

    /// <summary>M3-21: a gate that is open right now: a unit of the owner's team stands in its footprint (the sim has no stored
    /// open/closed state; D-076 gates are passable for the owner's team and a wall for everyone else). False for any non-gate and on ghosts.</summary>
    public bool GateOpen { get; init; }
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

    /// <summary>M3-21: facing of the turret (0..255, 0 = north, clockwise) for a vehicle type that has a turret, else null (draw the body
    /// facing). A turret that has not aimed yet reads the hull facing.</summary>
    public byte? TurretFacing { get; init; }

    /// <summary>M3-21: the unit is in the dive animation of a burrower (<see cref="ActorLayer.Diving"/>: going under or still on the
    /// surface). It is on the surface, so enemies see it like any visible unit.</summary>
    public bool Diving { get; init; }

    /// <summary>S19 D-205: the unit is carried (<see cref="ActorLayers.IsCarried"/>: a garrisoned soldier or a vehicle passenger / docked
    /// aircraft). It obeys no unit order, so a client must not offer it for selection; it leaves only when its carrier unloads.</summary>
    public bool Carried { get; init; }
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

    /// <summary>M4-17 (D-173): the skirmish defeat rule. When on, a player with no production structure, no unit that can found one and
    /// no harvester for <see cref="SimConstants.NoRebuildDefeatTicks"/> ticks is defeated (on top of the old "no building" rule). Default off
    /// (campaign missions and raw test setups keep their own rules); skirmish and AI-match setups turn it on. Part of the game's identity:
    /// saved in the header (format 13), written into the production part when on.</summary>
    public bool NoRebuildDefeat { get; init; }

    /// <summary>S19 D-205 (M13-15): per-player handicap in percent, one entry per player slot; null = every player at <see cref="DefaultHandicap"/>.
    /// A handicap of h percent makes the player's items build in 100/h of the normal time (production speed) and scales what the player's
    /// harvesters and salvagers deliver to h percent (income). Battle damage and health are untouched. Part of the game's identity: saved in the
    /// header (written only when some player is off 100, so other games keep their bytes) and written into the "players" part when on.</summary>
    public IReadOnlyList<int>? Handicap { get; init; }

    /// <summary>S19 D-205 (M13-15): starting credits per player slot; null = every player gets <c>Rules.Economy.StartingCash</c>. An entry below 0
    /// means "the rules' amount" for that slot. Part of the game's identity (saved in the header with <see cref="Handicap"/>); the credits
    /// themselves are ordinary state in the "players" part.</summary>
    public IReadOnlyList<int>? StartingCash { get; init; }

    /// <summary>The neutral handicap, in percent.</summary>
    public const int DefaultHandicap = 100;

    /// <summary>Lowest and highest handicap percent a setup may carry.</summary>
    public const int MinHandicap = 10, MaxHandicap = 200;

    /// <summary>Handicap percent of a player slot (100 when <see cref="Handicap"/> is null or too short).</summary>
    public int HandicapOf(byte player) => Handicap != null && player < Handicap.Count ? Handicap[player] : DefaultHandicap;

    /// <summary>Starting credits of a player slot: its <see cref="StartingCash"/> entry when that is 0 or more, else the rules' amount.</summary>
    public int StartingCashOf(byte player) =>
        StartingCash != null && player < StartingCash.Count && StartingCash[player] >= 0 ? StartingCash[player] : Rules.Economy.StartingCash;

    /// <summary>True when any player's handicap or starting credits differ from the defaults (the header then carries the slot options).</summary>
    public bool HasSlotOptions
    {
        get
        {
            for (int p = 0; p < PlayerCount; p++)
                if (HandicapOf((byte)p) != DefaultHandicap || (StartingCash != null && p < StartingCash.Count && StartingCash[p] >= 0)) return true;
            return false;
        }
    }

    /// <summary>M7-10: the map's landing markers (public map data, MapMeta.Landing), read-only for the AI through <c>PlayerView.LandingMarkers</c>.
    /// Not part of the game's identity (not saved, not hashed): the sim never reads it. Empty when the host has no map metadata.</summary>
    public IReadOnlyList<CellCoord> LandingMarkers { get; init; } = Array.Empty<CellCoord>();

    /// <summary>Optional scripting/mission extensions (M2-15, D-061), run in list order from the trigger phase of every Step.
    /// Their state parts are appended after the built-in parts, so they are saved and hashed. One extension instance
    /// serves one Simulation: build a fresh list for each Simulation you construct. Not part of the save header.</summary>
    public IReadOnlyList<ISimExtension>? Extensions { get; init; }

    /// <summary>Team id of a player slot (the slot index itself when Teams is null).</summary>
    public byte TeamOf(byte player) => Teams != null && player < Teams.Count ? Teams[player] : player;
}
