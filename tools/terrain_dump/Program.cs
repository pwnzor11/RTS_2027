using System.Diagnostics;
using Rts.Data;
using Rts.Data.MapGen;
using Rts.Game.Render;
using Rts.Sim.Map;
using Rts.Sim.Rules;

// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
// Dumps the baked terrain + decor of one map as raw RGBA (int32 w, int32 h, bytes) for tools/art/ter_pass.py. Not part of the solution.
// usage: harness <out-prefix> gen <biome> <size s|m|l> <players> <seed> | map <path>   [env RTS_TERRAIN_LOOK, HARNESS_PALETTE=path]
string prefix = args[0];
MapData map; MapMeta? meta;
if (args[1] == "gen")
{
    var b = (Biome)Array.IndexOf(MapFile.BiomeKeys, args[2]);
    var sz = args[3] switch { "s" => MapSize.Small, "m" => MapSize.Medium, _ => MapSize.Large };
    var gp = new MapGenParams(b, sz, int.Parse(args[4]), ulong.Parse(args[5]), null, default, 2, true);
    (map, meta) = MapGenerator.GenerateWithMeta(gp, BiomeCatalog.For(gp.Biome), out _);
}
else (map, meta) = MapFile.LoadWithMeta(args[2]);
string? pp = Environment.GetEnvironmentVariable("HARNESS_PALETTE");
TerrainPalette pal = pp is null ? TerrainPalette.ForMap(map, meta) : TerrainPalette.Load(pp);
var sw = Stopwatch.StartNew();
var t = TerrainBake.Bake(map, pal);
long msT = sw.ElapsedMilliseconds; sw.Restart();
var d = DecorBake.Bake(map, pal, MapHasher.Compute(map));
long msD = sw.ElapsedMilliseconds;
Console.WriteLine($"map {map.Width}x{map.Height} texel {pal.TexelCells} terrain {msT} ms decor {msD} ms ({d.Count} decals)");
void Dump(string f, int w, int h, byte[] rgba) { using var o = File.Create(f); o.Write(BitConverter.GetBytes(w)); o.Write(BitConverter.GetBytes(h)); o.Write(rgba); }
Dump(prefix + ".terrain.rgba", t.Width, t.Height, t.Rgba);
Dump(prefix + ".decor.rgba", d.Width, d.Height, d.Rgba);
using (var w = new StreamWriter(prefix + ".cells.txt"))
{
    w.WriteLine($"{map.Width} {map.Height} {pal.TexelCells}");
    foreach (var s in map.StartPositions) w.WriteLine($"start {s.X} {s.Y}");
    for (int y = 0; y < map.Height; y++) { for (int x = 0; x < map.Width; x++) { int k = y * map.Width + x; w.Write($"{(int)map.Land[k]}{(int)map.Overlay[k]:x}"); w.Write(' '); } w.WriteLine(); }
}
