// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Sim.Core;

namespace Rts.Scripting;

/// <summary>One player slot of a mission (from <c>[[players]]</c> in mission.toml).</summary>
/// <param name="Slot">Player slot index.</param>
/// <param name="Name">Display name.</param>
/// <param name="Role">"human", "ai" or "neutral".</param>
/// <param name="Team">Team id (255 = neutral).</param>
/// <param name="Cash">Starting cash.</param>
/// <param name="Tech">Allowed type ids, or null when the player is not tech-restricted.</param>
public sealed record MissionPlayer(int Slot, string Name, string Role, byte Team, int Cash, IReadOnlyList<string>? Tech);

/// <summary>A pre-placed actor (from <c>[[actors]]</c>): placed at setup, so replays see it in the setup log.</summary>
/// <param name="Type">Type id (vehicle, infantry or building).</param>
/// <param name="Player">Owner slot.</param>
/// <param name="Cell">Cell (top-left for buildings).</param>
/// <param name="Tag">Script tag, or empty.</param>
public sealed record MissionActor(string Type, int Player, CellCoord Cell, string Tag);

/// <summary>An inclusive rectangle of cells (<c>[zones.name]</c> rect = [x0, y0, x1, y1]).</summary>
public readonly record struct MissionZone(int X0, int Y0, int X1, int Y1)
{
    /// <summary>True if the cell lies inside (edges included).</summary>
    public bool Contains(CellCoord c) => c.X >= X0 && c.X <= X1 && c.Y >= Y0 && c.Y <= Y1;
}

/// <summary>Everything mission.toml says (D-061): map, players, zones, pre-placed actors. The script and briefing are separate files.</summary>
public sealed class MissionDef
{
    /// <summary>Mission id, the folder name.</summary>
    public required string Id { get; init; }

    /// <summary>Title for menus.</summary>
    public required string Title { get; init; }

    /// <summary>Map file name inside data/maps.</summary>
    public required string MapFile { get; init; }

    /// <summary>Lua script file name beside mission.toml.</summary>
    public required string ScriptFile { get; init; }

    /// <summary>Briefing text file name beside mission.toml.</summary>
    public required string BriefingFile { get; init; }

    /// <summary>Simulation seed.</summary>
    public required ulong Seed { get; init; }

    /// <summary>Players in slot order (slots are 0..n-1).</summary>
    public required IReadOnlyList<MissionPlayer> Players { get; init; }

    /// <summary>Named zones, in name order.</summary>
    public required IReadOnlyList<KeyValuePair<string, MissionZone>> Zones { get; init; }

    /// <summary>Pre-placed actors in file order.</summary>
    public required IReadOnlyList<MissionActor> Actors { get; init; }

    /// <summary>The zone with this name, or null.</summary>
    public MissionZone? ZoneNamed(string name)
    {
        foreach (var z in Zones) if (z.Key == name) return z.Value;
        return null;
    }
}
