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
/// <param name="Faction">The player's faction id (s12, optional <c>faction</c> key on <c>[[players]]</c>); "" = the mission's faction for a human
/// player, Authority otherwise. Passed to the sim so faction rules (Expedition beachhead, Left Behind supply) apply in missions.</param>
public sealed record MissionPlayer(int Slot, string Name, string Role, byte Team, int Cash, IReadOnlyList<string>? Tech, string Faction = "");

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

    /// <summary>Mission file schema: 1 (pilot, auth_01) or 2 (M8, plan 3.5).</summary>
    public int Schema { get; init; } = 1;

    /// <summary>Schema 2: campaign act 1..4 (0 = none).</summary>
    public int Act { get; init; }

    /// <summary>Schema 2: faction id ("authority", "directorate", "left_behind", "expedition"), or empty.</summary>
    public string Faction { get; init; } = "";

    /// <summary>Schema 2: successor mission ids (empty = last mission; two entries = a choice point, plan 3.6).</summary>
    public IReadOnlyList<string> Next { get; init; } = Array.Empty<string>();

    /// <summary>Schema 2: locale file beside mission.toml (text by key, plan 3.2), or empty.</summary>
    public string LocaleFile { get; init; } = "";

    /// <summary>Schema 2: the mission's text by locale key (locale.toml, plan 3.2); empty when the mission has none.</summary>
    public MissionLocale Locale { get; init; } = MissionLocale.Empty;

    /// <summary>Schema 2: type ids always available in this mission (<c>[starts_with] tech</c>); the allow-list is these plus the campaign tech set (plan 3.3).</summary>
    public IReadOnlyList<string> StartsWith { get; init; } = Array.Empty<string>();

    /// <summary>Schema 2: type ids added to the campaign tech set when the mission is won (<c>[unlocks_on_win] tech</c>).</summary>
    public IReadOnlyList<string> UnlocksOnWin { get; init; } = Array.Empty<string>();

    /// <summary>The mission's folder (<c>data/missions/&lt;id&gt;</c>) as loaded; empty for a hand-made definition. S19 MIS-12 uses it to find the data folder's Archive.</summary>
    public string Folder { get; init; } = "";

    /// <summary>The zone with this name, or null.</summary>
    public MissionZone? ZoneNamed(string name)
    {
        foreach (var z in Zones) if (z.Key == name) return z.Value;
        return null;
    }
}
