// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using System.Globalization;
using Rts.Data;
using Rts.Sim.Core;
using Rts.Sim.Map;
using Rts.Sim.Rules;

namespace Rts.Tools;

/// <summary>`Tools map` subcommands: <c>new</c> and <c>info</c>. Exit codes: 0 ok, 1 error, 2 usage.</summary>
public static class MapCommands
{
    private const string Usage =
        "usage: map new --size <n> --seed <s> --out <path>\n" +
        "       map info <path>";

    public static int Run(string[] args)
    {
        if (args.Length == 0) { Console.Error.WriteLine(Usage); return 2; }
        switch (args[0])
        {
            case "new": return New(args[1..]);
            case "info": return Info(args[1..]);
            default:
                Console.Error.WriteLine($"error: unknown map command '{args[0]}'");
                Console.Error.WriteLine(Usage);
                return 2;
        }
    }

    private static int New(string[] args)
    {
        int? size = null; ulong? seed = null; string? outPath = null;
        for (int i = 0; i < args.Length; i++)
        {
            string a = args[i];
            if (i + 1 >= args.Length || a is not ("--size" or "--seed" or "--out"))
            {
                Console.Error.WriteLine(Usage);
                return 2;
            }
            string v = args[++i];
            switch (a)
            {
                case "--size":
                    if (!int.TryParse(v, NumberStyles.None, CultureInfo.InvariantCulture, out int sz)) { Console.Error.WriteLine(Usage); return 2; }
                    size = sz; break;
                case "--seed":
                    if (!ulong.TryParse(v, NumberStyles.None, CultureInfo.InvariantCulture, out ulong sd)) { Console.Error.WriteLine(Usage); return 2; }
                    seed = sd; break;
                default: outPath = v; break;
            }
        }
        if (size is null || seed is null || outPath is null) { Console.Error.WriteLine(Usage); return 2; }

        try
        {
            string name = Path.GetFileName(outPath);
            const string suffix = ".map.toml";
            name = name.EndsWith(suffix, StringComparison.OrdinalIgnoreCase) ? name[..^suffix.Length] : Path.GetFileNameWithoutExtension(name);
            MapData map = MapGenerator.Generate(size.Value, seed.Value, name);
            MapFile.Save(map, outPath);
            Console.WriteLine($"wrote {outPath} {map.Width}x{map.Height} map hash 0x{MapHasher.Compute(map):x16}");
            return 0;
        }
        catch (Exception e) when (e is ArgumentException or InvalidOperationException or IOException or UnauthorizedAccessException)
        {
            Console.Error.WriteLine($"error: {e.Message}");
            return 1;
        }
    }

    private static int Info(string[] args)
    {
        if (args.Length != 1) { Console.Error.WriteLine(Usage); return 2; }
        try
        {
            MapData map = MapFile.Load(args[0]);
            Console.WriteLine($"name: {map.Name}");
            Console.WriteLine($"size: {map.Width}x{map.Height}");
            Console.WriteLine($"theater: {map.Theater}");
            Console.WriteLine("start positions: " + string.Join(" ", map.StartPositions.Select(p => $"({p.X},{p.Y})")));
            var land = new int[RuleCounts.LandTypes];
            var overlay = new int[Enum.GetValues<OverlayKind>().Length];
            for (int i = 0; i < map.Land.Count; i++) { land[(int)map.Land[i]]++; overlay[(int)map.Overlay[i]]++; }
            Console.WriteLine("land: " + string.Join(", ", Enum.GetValues<LandType>().Select(l => $"{l}={land[(int)l]}")));
            Console.WriteLine("overlay: " + string.Join(", ", Enum.GetValues<OverlayKind>().Select(o => $"{o}={overlay[(int)o]}")));
            Console.WriteLine($"map hash: 0x{MapHasher.Compute(map):x16}");
            return 0;
        }
        catch (MapFileException e)
        {
            Console.Error.WriteLine($"error: {e.Message}");
            return 1;
        }
    }
}
