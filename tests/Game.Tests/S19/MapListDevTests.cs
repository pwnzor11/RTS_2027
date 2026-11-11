// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Data;
using Rts.Game.Screens;

namespace Rts.Game.Tests.S19;

/// <summary>S19 UI1 item 5 (D-205): maps with <c>dev = true</c> are not offered to players but still load.</summary>
public sealed class MapListDevTests
{
    private static string FindRules()
    {
        string? dir = RulesLocator.FindRulesDir(AppContext.BaseDirectory);
        return dir ?? throw new InvalidOperationException("data/rules not found");
    }

    [Fact]
    public void The_test_maps_are_not_in_the_player_list()
    {
        string rules = FindRules();
        IReadOnlyList<MapEntry> list = MapPicker.List(rules, Path.Combine(Path.GetTempPath(), "rts_s19_none_" + Guid.NewGuid().ToString("N")));
        Assert.DoesNotContain(list, m => m.File is { } f && f.StartsWith("test_", StringComparison.Ordinal));
        Assert.Contains(list, m => m.File == SkirmishSettings.CuratedFile);
    }

    [Fact]
    public void The_test_maps_still_load_and_say_they_are_dev()
    {
        string maps = Path.GetFullPath(Path.Combine(FindRules(), "..", "maps"));
        foreach (string name in new[] { "test_32.map.toml", "test_64.map.toml" })
        {
            (Rts.Sim.Map.MapData map, MapMeta meta) = MapFile.LoadWithMeta(Path.Combine(maps, name));
            Assert.True(meta.Dev, name);
            Assert.True(map.Width >= 32);
        }
        (_, MapMeta curated) = MapFile.LoadWithMeta(Path.Combine(maps, SkirmishSettings.CuratedFile));
        Assert.False(curated.Dev);
    }

    [Fact]
    public void A_dev_map_keeps_its_flag_through_save_and_load()
    {
        string maps = Path.GetFullPath(Path.Combine(FindRules(), "..", "maps"));
        (Rts.Sim.Map.MapData map, MapMeta meta) = MapFile.LoadWithMeta(Path.Combine(maps, "rustbelt_m2.map.toml"));
        string tmp = Path.Combine(Path.GetTempPath(), "rts_s19_dev_" + Guid.NewGuid().ToString("N") + ".map.toml");
        try
        {
            MapFile.Save(map, meta with { Dev = true }, tmp);
            Assert.True(MapFile.LoadWithMeta(tmp).Item2.Dev);
        }
        finally
        {
            File.Delete(tmp);
        }
    }
}
