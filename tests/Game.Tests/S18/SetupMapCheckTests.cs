// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using System.Text.RegularExpressions;
using Rts.Data;
using Rts.Game.Screens;
using Rts.Scripting;
using Rts.Sim.Rules;

namespace Rts.Game.Tests.S18;

/// <summary>S18 DATA-TOOLS-03: the map validator runs on hand-made maps before a skirmish. First it is proved that every map the game ships
/// (the skirmish list and the mission maps) passes it, so wiring it in changes nothing that loads today.</summary>
public sealed class SetupMapCheckTests
{
    private static string RulesDir => RulesLocator.FindRulesDir(AppContext.BaseDirectory) ?? throw new InvalidOperationException("data/rules not found");

    private static string MapsDir => Path.GetFullPath(Path.Combine(RulesDir, "..", "maps"));

    [Fact]
    public void Every_shipped_map_has_no_validator_errors_with_the_built_in_rule_and_with_the_rules_terrain()
    {
        string[] maps = Directory.GetFiles(MapsDir, "*" + MapPicker.MapSuffix);
        Assert.NotEmpty(maps);
        TerrainTable terrain = RulesLoader.Load(RulesDir).Sim.Terrain;
        foreach (string path in maps)
        {
            Assert.True(MapCheck.Errors(path).Count == 0, $"{Path.GetFileName(path)} (built-in terrain): {string.Join("; ", MapCheck.Errors(path))}");
            Assert.True(MapCheck.Errors(path, terrain).Count == 0, $"{Path.GetFileName(path)} (rules terrain): {string.Join("; ", MapCheck.Errors(path, terrain))}");
        }
    }

    [Fact]
    public void Every_mission_map_is_one_of_those_maps_and_passes_too()
    {
        string dataDir = Path.GetFullPath(Path.Combine(RulesDir, ".."));
        int missions = 0;
        foreach (string folder in Directory.GetDirectories(Path.Combine(dataDir, "missions")))
        {
            MissionDef def = MissionLoader.Load(folder);
            string map = Path.Combine(dataDir, "maps", def.MapFile);
            Assert.True(File.Exists(map), $"{def.Id}: map {def.MapFile} is missing");
            Assert.True(MapCheck.Errors(map).Count == 0, $"{def.Id}: {def.MapFile}: {string.Join("; ", MapCheck.Errors(map))}");
            missions++;
        }
        Assert.True(missions >= 17);
    }

    [Fact]
    public void The_curated_map_choice_resolves_and_passes_by_name()
    {
        Assert.Empty(MapCheck.Errors(RulesDir, SkirmishSettings.CuratedFile));
    }

    [Fact]
    public void A_hand_made_map_with_starts_on_top_of_each_other_is_reported_by_code_and_cell()
    {
        string file = Path.Combine(Path.GetTempPath(), "rts_s18_badmap_" + Guid.NewGuid().ToString("N") + MapPicker.MapSuffix);
        try
        {
            string text = File.ReadAllText(Path.Combine(MapsDir, "test_32.map.toml"));
            string broken = Regex.Replace(text, @"start_positions\s*=\s*\[\[[^\n]*\]\]", "start_positions = [[4, 4], [6, 6]]");
            Assert.NotEqual(text, broken);
            File.WriteAllText(file, broken);
            IReadOnlyList<string> errors = MapCheck.Errors(file);
            Assert.Contains(errors, e => e.StartsWith("start_distance", StringComparison.Ordinal));
        }
        finally
        {
            File.Delete(file);
        }
    }

    [Fact]
    public void A_file_that_is_not_a_map_raises_MapFileException_for_the_caller_to_show()
    {
        string file = Path.Combine(Path.GetTempPath(), "rts_s18_notamap_" + Guid.NewGuid().ToString("N") + MapPicker.MapSuffix);
        try
        {
            File.WriteAllText(file, "this is not toml [[[");
            Assert.Throws<MapFileException>(() => MapCheck.Errors(file));
        }
        finally
        {
            File.Delete(file);
        }
    }

    [Fact]
    public void Maps_that_fail_to_load_are_logged_with_a_count_and_left_out_of_the_list()
    {
        string user = Path.Combine(Path.GetTempPath(), "rts_s18_maps_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(user);
        try
        {
            File.WriteAllText(Path.Combine(user, "broken" + MapPicker.MapSuffix), "garbage [[[");
            // test_32 is a dev fixture (S19 dev = true) and would be hidden: the copy is an ordinary map.
            string fixture = File.ReadAllText(Path.Combine(MapsDir, "test_32.map.toml")).Replace("dev = true", "", StringComparison.Ordinal);
            File.WriteAllText(Path.Combine(user, "good" + MapPicker.MapSuffix), fixture);
            var log = new List<string>();
            IReadOnlyList<MapEntry> list = MapPicker.List(RulesDir, user, log.Add);
            Assert.DoesNotContain(list, m => m.File is { } f && f.EndsWith("broken" + MapPicker.MapSuffix, StringComparison.Ordinal));
            Assert.Contains(list, m => m.File is { } f && f.EndsWith("good" + MapPicker.MapSuffix, StringComparison.Ordinal));
            string line = Assert.Single(log);
            Assert.Contains("1 map file(s) not offered", line);
            Assert.Contains("broken" + MapPicker.MapSuffix, line);
            Assert.DoesNotContain("good" + MapPicker.MapSuffix, line);
        }
        finally
        {
            Directory.Delete(user, recursive: true);
        }
    }

    [Fact]
    public void A_clean_map_folder_logs_nothing()
    {
        string user = Path.Combine(Path.GetTempPath(), "rts_s18_maps_" + Guid.NewGuid().ToString("N"));
        try
        {
            var log = new List<string>();
            MapPicker.List(RulesDir, user, log.Add);
            Assert.Empty(log);
        }
        finally
        {
            if (Directory.Exists(user)) Directory.Delete(user, recursive: true);
        }
    }
}
