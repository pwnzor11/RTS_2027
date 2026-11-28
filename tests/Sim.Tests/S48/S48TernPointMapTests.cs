// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using System.Globalization;
using System.Text.RegularExpressions;
using Rts.Data;
using Rts.Data.MapGen;
using Rts.Sim.Core;
using Rts.Sim.Map;
using Rts.Sim.Rules;

namespace Rts.Sim.Tests.S48;

/// <summary>M16-C-12: data/maps/tern_point.map.toml (built by tools/maps/make_tern_point.py) loads, validates, names its zones in the header,
/// has a landing beach reachable from the sea and by ground from the inland base, and takes an Expedition Command Ship's Deploy.</summary>
public sealed class S48TernPointMapTests
{
    private static readonly RuleSet Rules = RulesLoader.Load(RulesLocator.FindRulesDir(AppContext.BaseDirectory)!);
    private static readonly string[] ZoneNames =
    [
        "beach_landing", "home_inland", "sea_s", "relief_wreck", "dredge_shallows", "railhead", "platform", "dunes_n", "dunes_s",
        "spawn_n", "spawn_e", "spawn_s", "spawn_w", "sea_spawn",
    ];

    private static string MapPath()
    {
        for (var d = new DirectoryInfo(AppContext.BaseDirectory); d is not null; d = d.Parent)
            if (File.Exists(Path.Combine(d.FullName, "RTS_2027.slnx"))) return Path.Combine(d.FullName, "data", "maps", "tern_point.map.toml");
        throw new InvalidOperationException("repo root not found");
    }

    private static (MapData Map, MapMeta Meta) Load() => MapFile.LoadWithMeta(MapPath());

    private static Dictionary<string, int[]> Zones()
    {
        var zones = new Dictionary<string, int[]>(StringComparer.Ordinal);
        foreach (string line in File.ReadLines(MapPath()))
        {
            Match m = Regex.Match(line, @"^#\s+(\w+)\s+\[(\d+), (\d+), (\d+), (\d+)\]\s*$");
            if (m.Success) zones[m.Groups[1].Value] = [.. m.Groups.Cast<Group>().Skip(2).Select(g => int.Parse(g.Value, CultureInfo.InvariantCulture))];
        }
        return zones;
    }

    [Fact]
    public void Loads_as_a_64x64_glass_coast_map_and_validates_clean()
    {
        (MapData map, MapMeta meta) = Load();
        Assert.Equal("tern_point", map.Name);
        Assert.Equal((64, 64), (map.Width, map.Height));
        Assert.Equal("glasscoast", map.Theater);
        Assert.Equal(2, map.StartPositions.Count);
        Assert.Empty(MapValidator.Validate(map, meta, Rules.Sim.Terrain));
    }

    [Fact]
    public void Every_zone_is_listed_in_the_header_and_lies_on_the_map()
    {
        (MapData map, _) = Load();
        Dictionary<string, int[]> zones = Zones();
        foreach (string name in ZoneNames)
        {
            Assert.True(zones.ContainsKey(name), name);
            int[] r = zones[name];
            Assert.True(r[0] <= r[2] && r[1] <= r[3] && r[0] >= 0 && r[1] >= 0 && r[2] < map.Width && r[3] < map.Height, name);
        }
        // the sea zones are sea, the beach zone holds Coast cells, the dredge zone holds Ashfall on Shallow, the wreck zone holds a Wreck prop
        int Count(string zone, Func<int, bool> f)
        {
            int[] r = zones[zone]; int n = 0;
            for (int y = r[1]; y <= r[3]; y++) for (int x = r[0]; x <= r[2]; x++) if (f(map.IndexOf(new CellCoord(x, y)))) n++;
            return n;
        }
        Assert.True(Count("sea_spawn", i => map.Land[i] == LandType.Water) > 100);
        Assert.True(Count("beach_landing", i => map.Land[i] == LandType.Coast) >= 20);
        Assert.True(Count("dredge_shallows", i => map.Land[i] == LandType.Shallow && map.Overlay[i] == OverlayKind.Ashfall) >= 20);
        Assert.True(Count("relief_wreck", i => map.Overlay[i] == OverlayKind.Wreck) >= 5);
        Assert.True(Count("platform", i => map.Land[i] == LandType.Road) >= 10);
        Assert.True(Count("railhead", i => map.Overlay[i] == OverlayKind.Wreck) >= 3);
        Assert.True(Count("dunes_n", i => map.Land[i] == LandType.Rough) >= 20);
        Assert.True(Count("dunes_s", i => map.Land[i] == LandType.Rough) >= 20);
    }

    [Fact]
    public void The_beach_is_reachable_from_the_sea_and_by_ground_from_home_inland()
    {
        (MapData map, MapMeta meta) = Load();
        Dictionary<string, int[]> zones = Zones();
        Assert.True(meta.Landing.Count >= 3);
        Assert.NotEmpty(meta.ConvoyEdge);
        int[] sea = MapAnalysis.Distances(map, meta.ConvoyEdge[0], Locomotion.Naval, Rules.Sim.Terrain);
        int[] foot = MapAnalysis.Distances(map, map.StartPositions[1], Locomotion.Foot, Rules.Sim.Terrain);
        int[] tracked = MapAnalysis.Distances(map, map.StartPositions[1], Locomotion.Tracked, Rules.Sim.Terrain);
        int[] home = zones["home_inland"], beach = zones["beach_landing"];
        Assert.True(home[0] <= map.StartPositions[1].X && map.StartPositions[1].X <= home[2] && home[1] <= map.StartPositions[1].Y && map.StartPositions[1].Y <= home[3]);
        Assert.True(beach[0] <= map.StartPositions[0].X && map.StartPositions[0].X <= beach[2] && beach[1] <= map.StartPositions[0].Y && map.StartPositions[0].Y <= beach[3]);
        foreach (CellCoord c in meta.Landing)
        {
            Assert.Equal(LandType.Coast, map.Land[map.IndexOf(c)]);
            Assert.NotEqual(MapAnalysis.Unreachable, foot[map.IndexOf(c)]);
            Assert.NotEqual(MapAnalysis.Unreachable, tracked[map.IndexOf(c)]);
            bool fromSea = false;
            foreach ((int dx, int dy) in new[] { (1, 0), (-1, 0), (0, 1), (0, -1) })
            {
                var n = new CellCoord(c.X + dx, c.Y + dy);
                if (map.Contains(n) && sea[map.IndexOf(n)] != MapAnalysis.Unreachable) fromSea = true;
            }
            Assert.True(fromSea, $"landing {c} not reachable from the sea");
        }
        // the platform and the railhead yard are reachable on foot from both bases
        foreach (string z in new[] { "platform", "railhead", "relief_wreck" })
        {
            int[] r = zones[z]; bool any = false;
            for (int y = r[1]; y <= r[3] && !any; y++) for (int x = r[0]; x <= r[2] && !any; x++) any = foot[map.IndexOf(new CellCoord(x, y))] != MapAnalysis.Unreachable;
            Assert.True(any, z);
        }
    }

    [Fact]
    public void An_Expedition_Command_Ship_deploys_into_a_landing_depot_on_the_beach_landing()
    {
        (MapData map, MapMeta meta) = Load();
        var setup = new SimSetup(5, map, 2, Rules.Sim, Rules.RulesHash) { Factions = [Faction.Expedition, Faction.Authority] };
        var sim = new Simulation(setup);
        int shipType = SimRules.IndexOf(Rules.Sim.Vehicles, "exp_command_ship", v => v.Id);
        int depotType = SimRules.IndexOf(Rules.Sim.Buildings, "exp_landing_depot", b => b.Id);
        {
            CellCoord landing = meta.Landing[1];
            // the ship waits on the water cell right off the marker (the marker's sea-side neighbour)
            CellCoord sea = default; bool found = false;
            foreach ((int dx, int dy) in new[] { (0, 1), (1, 0), (-1, 0), (0, -1) })
            {
                var n = new CellCoord(landing.X + dx, landing.Y + dy);
                if (map.Contains(n) && map.Land[map.IndexOf(n)] is LandType.Shallow or LandType.Water) { sea = n; found = true; break; }
            }
            Assert.True(found, $"no sea beside {landing}");
            EntityId ship = sim.SpawnVehicle(0, shipType, sea);
            Assert.False(ship.IsNone);
            sim.Step([new Command(sim.Tick, 0, 1, CommandKind.Deploy, ship, default, sea, 0)]);
            for (int i = 0; i < 5; i++) sim.Step(Array.Empty<Command>());
            Assert.False(sim.Units.Slots.IsAlive(ship), $"ship at {sea} did not deploy");
            int depots = 0;
            for (int i = 1; i < sim.Buildings.Capacity; i++) if (sim.Buildings.Slots.IsIndexAlive(i) && sim.Buildings.TypeIndex[i] == depotType) depots++;
            Assert.Equal(1, depots);
        }
    }
}
