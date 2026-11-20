// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSING.md (incl. the MonoGame linking permission).
using System.Globalization;
using Rts.Data;
using Rts.Data.MapGen;
using Rts.Sim.Core;
using Rts.Sim.Map;
using Rts.Sim.Rules;

namespace Rts.Tools;

/// <summary>
/// `map econ` (M13-33): per start, how much ore each harvester locomotion can reach by path length (4-way BFS over the real terrain table,
/// blocking props included), nearest field, and the generator's landing markers. Options as for `map gen` without --out.
/// </summary>
internal static class MapEconCommand
{
    private static readonly int[] Buckets = [15, 25, 40, 60, 90];

    /// <summary>Runs the command; returns the process exit code.</summary>
    internal static int Run(string[] args)
    {
        Biome biome = Biome.Mountains; MapSize size = MapSize.Medium; int layout = 1; ulong seed = 1; OceanEdges ocean = OceanEdges.None;
        for (int i = 0; i + 1 < args.Length; i += 2)
        {
            string v = args[i + 1];
            switch (args[i])
            {
                case "--biome": biome = (Biome)Array.IndexOf(MapFile.BiomeKeys, v); break;
                case "--size": size = v switch { "s" => MapSize.Small, "l" => MapSize.Large, _ => MapSize.Medium }; break;
                case "--layout": layout = int.Parse(v, CultureInfo.InvariantCulture); break;
                case "--seed": seed = ulong.Parse(v, CultureInfo.InvariantCulture); break;
                case "--ocean": foreach (char ch in v) ocean |= (OceanEdges)(1 << "nesw".IndexOf(char.ToLowerInvariant(ch))); break;
                default: Console.Error.WriteLine("usage: map econ [--biome b] [--size s|m|l] [--layout n] [--seed n] [--ocean nesw]"); return 2;
            }
        }
        var terrain = Rts.Data.RulesLoader.Load("data/rules").Sim.Terrain;
        var gen = MapGenerator.GenerateWithMeta(new MapGenParams(biome, size, 2, seed, null, ocean, layout));
        MapData map = gen.Map;
        int n = map.Width * map.Height, totalDens = 0, totalCells = 0;
        for (int i = 0; i < n; i++)
            if (map.Overlay[i] is OverlayKind.Ashfall or OverlayKind.Pitchblende && map.Density[i] > 0) { totalCells++; totalDens += map.Density[i]; }
        Console.WriteLine($"{map.Name} seed {seed} ocean {ocean}: ore {totalCells} cells, density {totalDens}; landing markers {gen.Meta.Landing.Count}");
        for (int s = 0; s < map.StartPositions.Count; s++)
            foreach (Locomotion loco in new[] { Locomotion.Tracked, Locomotion.Hover })
            {
                int[] dist = MapAnalysis.Distances(map, map.StartPositions[s], loco, terrain);
                var cells = new int[Buckets.Length + 1]; var dens = new int[Buckets.Length + 1]; int nearest = int.MaxValue;
                for (int i = 0; i < n; i++)
                {
                    if (map.Overlay[i] is not (OverlayKind.Ashfall or OverlayKind.Pitchblende) || map.Density[i] <= 0) continue;
                    int d = dist[i];
                    if (d == MapAnalysis.Unreachable) continue;
                    nearest = Math.Min(nearest, d);
                    int b = 0; while (b < Buckets.Length && d > Buckets[b]) b++;
                    for (int k = b; k <= Buckets.Length; k++) { cells[k]++; dens[k] += map.Density[i]; }
                }
                string row = string.Join(" ", Enumerable.Range(0, Buckets.Length + 1).Select(k => $"{(k < Buckets.Length ? "<=" + Buckets[k] : "all")}:{cells[k]}c/{dens[k]}d"));
                Console.WriteLine($"  start {s} {map.StartPositions[s]} {loco,-7} nearest {(nearest == int.MaxValue ? "-" : nearest.ToString(CultureInfo.InvariantCulture))}  {row}");
            }
        return 0;
    }
}
