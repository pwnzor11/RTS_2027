// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using System.Globalization;
using Rts.Data;
using Rts.Data.MapGen;
using Rts.Sim.Core;
using Rts.Sim.Map;
using Rts.Sim.Rules;

namespace Rts.Scripting.Tests.S48;

/// <summary>M16-C-12: the rustbelt_mill story map (D-341 Q6) loads, validates, carries the zones the missions use (read from the
/// "# zone name x0 y0 x1 y1" header lines the generator script writes) and every zone is reachable from home_sw by ground.</summary>
public class S48MapMillTests
{
    private static string Root()
    {
        for (var d = new DirectoryInfo(AppContext.BaseDirectory); d is not null; d = d.Parent)
            if (File.Exists(Path.Combine(d.FullName, "RTS_2027.slnx"))) return d.FullName;
        throw new InvalidOperationException("repo root not found");
    }

    private static string MapPath() => Path.Combine(Root(), "data", "maps", "rustbelt_mill.map.toml");

    private static Dictionary<string, (int X0, int Y0, int X1, int Y1)> Zones()
    {
        var z = new Dictionary<string, (int, int, int, int)>();
        foreach (string line in File.ReadLines(MapPath()))
        {
            if (!line.StartsWith("# zone ", StringComparison.Ordinal) || line.StartsWith("# zone <", StringComparison.Ordinal)) continue;
            string[] p = line.Split(' ');
            int V(int i) => int.Parse(p[i], CultureInfo.InvariantCulture);
            z[p[2]] = (V(3), V(4), V(5), V(6));
        }
        return z;
    }

    private static bool Reached(int[] dist, int width, (int X0, int Y0, int X1, int Y1) r)
    {
        for (int y = r.Y0; y <= r.Y1; y++)
            for (int x = r.X0; x <= r.X1; x++)
                if (dist[y * width + x] != MapAnalysis.Unreachable) return true;
        return false;
    }

    [Fact]
    public void Loads_64x64_rustbelt_with_two_starts()
    {
        (MapData map, _) = MapFile.LoadWithMeta(MapPath());
        Assert.Equal("rustbelt_mill", map.Name);
        Assert.Equal("rustbelt", map.Theater);
        Assert.Equal(64, map.Width);
        Assert.Equal(64, map.Height);
        Assert.True(map.StartPositions.Count >= 2);
    }

    [Fact]
    public void Validates_with_no_errors()
    {
        (MapData map, MapMeta meta) = MapFile.LoadWithMeta(MapPath());
        Assert.DoesNotContain(MapValidator.Validate(map, meta, null), i => i.Severity == MapIssueSeverity.Error);
    }

    [Theory]
    [InlineData("home_sw"), InlineData("home_ne"), InlineData("mill_town"), InlineData("rail_w"), InlineData("rail_e"),
     InlineData("train_wreck"), InlineData("bore_site"), InlineData("survey_camp"), InlineData("shaft_ground"),
     InlineData("spawn_n"), InlineData("spawn_e"), InlineData("spawn_s"), InlineData("spawn_w")]
    public void Zone_is_present_and_on_the_map(string name)
    {
        var zones = Zones();
        Assert.True(zones.ContainsKey(name), name);
        var (x0, y0, x1, y1) = zones[name];
        Assert.True(0 <= x0 && x0 <= x1 && x1 < 64 && 0 <= y0 && y0 <= y1 && y1 < 64, name);
    }

    [Fact]
    public void Every_zone_is_reachable_from_home_sw_by_ground()
    {
        (MapData map, _) = MapFile.LoadWithMeta(MapPath());
        var zones = Zones();
        Assert.True(zones.Count >= 13);
        CellCoord start = map.StartPositions[0];
        var home = zones["home_sw"];
        Assert.True(start.X >= home.X0 && start.X <= home.X1 && start.Y >= home.Y0 && start.Y <= home.Y1);
        foreach (Locomotion loco in new[] { Locomotion.Foot, Locomotion.Tracked })
        {
            int[] dist = MapAnalysis.Distances(map, start, loco, null);
            foreach (var (name, r) in zones)
            {
                Assert.True(Reached(dist, map.Width, r), $"{name} not reachable by {loco}");
            }
        }
    }

    [Fact]
    public void Both_homes_reach_the_town_and_the_rail()
    {
        (MapData map, _) = MapFile.LoadWithMeta(MapPath());
        var zones = Zones();
        foreach (CellCoord start in map.StartPositions)
        {
            int[] dist = MapAnalysis.Distances(map, start, Locomotion.Tracked, null);
            foreach (string name in new[] { "bore_site", "survey_camp", "train_wreck", "rail_w", "rail_e" })
            {
                Assert.True(Reached(dist, map.Width, zones[name]), name);
            }
        }
    }
}
