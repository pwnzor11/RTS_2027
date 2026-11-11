// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Data;
using Rts.Sim.Map;
using Rts.Sim.Rules;

namespace Rts.Game.Screens;

/// <summary>
/// Runs <see cref="MapValidator"/> on a hand-made map (a file, not a generated map) before a skirmish is built on it (S18 DATA-TOOLS-03):
/// a map with a start that cannot reach the others, or two starts on top of each other, now says so by name instead of showing up later as
/// an AI that does nothing. Only errors count; warnings (few resource fields, symmetry) never stop a game.
/// </summary>
public static class MapCheck
{
    /// <summary>The validator's errors for the map file at <paramref name="path"/>, one line each ("start_space (12, 40): ..."); empty when the map is fine.
    /// <paramref name="terrain"/> null = the validator's built-in passability rule.</summary>
    /// <exception cref="MapFileException">The file cannot be read as a map.</exception>
    public static IReadOnlyList<string> Errors(string path, TerrainTable? terrain = null)
    {
        (MapData map, MapMeta meta) = MapFile.LoadWithMeta(path);
        var lines = new List<string>();
        foreach (MapIssue issue in MapValidator.Validate(map, meta, terrain))
        {
            if (issue.Severity != MapIssueSeverity.Error) continue;
            string at = issue.Cell is { } c ? $" ({c.X}, {c.Y})" : "";
            lines.Add($"{issue.Code}{at}: {issue.Message}");
        }
        return lines;
    }

    /// <summary>As <see cref="Errors(string, TerrainTable?)"/> for a map named the way a <see cref="MapChoice"/> names it, with the terrain of the rules in
    /// <paramref name="rulesDir"/> (the built-in rule when the rules cannot be loaded: starting the game then reports that itself).</summary>
    public static IReadOnlyList<string> Errors(string rulesDir, string file)
    {
        TerrainTable? terrain = null;
        try { terrain = RulesLoader.Load(rulesDir).Sim.Terrain; }
        catch (RulesException) { }
        return Errors(MapPicker.ResolveFile(rulesDir, file), terrain);
    }
}
