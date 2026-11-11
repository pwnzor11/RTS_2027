// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Data;
using Rts.Game.Tests.Archive;
using Rts.Game.Ui;

namespace Rts.Game.Tests.Ui;

/// <summary>S19 naming pass: players read map and mission names, not ids.</summary>
public sealed class DisplayNamesTests
{
    [Fact]
    public void Shipped_maps_and_missions_have_display_names()
    {
        string dataDir = Path.GetDirectoryName(ArchiveTestData.RulesDir)!;
        string? before = DisplayNames.DataDir;
        DisplayNames.DataDir = dataDir;
        try
        {
            foreach (string file in Directory.EnumerateFiles(Path.Combine(dataDir, "maps"), "*.map.toml"))
            {
                (Rts.Sim.Map.MapData map, MapMeta meta) = MapFile.LoadWithMeta(file);
                if (meta.Dev) continue;
                string shown = DisplayNames.Map(map.Name);
                Assert.True(shown != map.Name && !shown.Contains('_'), $"{Path.GetFileName(file)}: shows '{shown}'");
            }
            foreach (string dir in Directory.EnumerateDirectories(Path.Combine(dataDir, "missions")))
            {
                string id = Path.GetFileName(dir);
                string shown = DisplayNames.Mission(id);
                Assert.True(shown != id && !shown.Contains('_'), $"{id}: shows '{shown}'");
            }
        }
        finally { DisplayNames.DataDir = before; }
    }

    [Fact]
    public void A_players_own_map_name_and_an_unknown_mission_are_left_alone()
    {
        Assert.Equal("My Map", DisplayNames.Map("My Map"));
        Assert.Equal("nope_99", DisplayNames.Mission("nope_99"));
    }
}
