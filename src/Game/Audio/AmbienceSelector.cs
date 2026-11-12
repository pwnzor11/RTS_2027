// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Sim.Core;
using Rts.Sim.Rules;

namespace Rts.Game.Audio;

/// <summary>
/// S22: which ambience loop belongs to a place. The map's theater picks the biome loop; when most of the cells around the camera are water,
/// the open-sea loop plays instead (with a hysteresis so the camera at a shoreline does not flip back and forth).
/// </summary>
public static class AmbienceSelector
{
    /// <summary>The open-sea ambience key.</summary>
    public const string OpenSea = "open_sea";

    /// <summary>The ambience key of a map without a known theater.</summary>
    public const string DefaultBiome = "rust_belt";

    /// <summary>Share of water cells around the camera at which open_sea takes over.</summary>
    public const double SeaEnter = 0.6;

    /// <summary>Share of water below which open_sea gives way again.</summary>
    public const double SeaLeave = 0.4;

    /// <summary>Half the side, in cells, of the square around the camera that is checked for water.</summary>
    public const int WaterRadiusCells = 20;

    /// <summary>The biome key for a theater id as the maps write it (<c>rustbelt</c>, <c>rust_belt</c>, <c>glasscoast</c> ...); unknown or empty gives <see cref="DefaultBiome"/>.</summary>
    public static string BiomeOf(string? theaterId)
    {
        if (string.IsNullOrEmpty(theaterId)) return DefaultBiome;
        var sb = new System.Text.StringBuilder(theaterId.Length);
        foreach (char c in theaterId)
        {
            if (char.IsLetterOrDigit(c)) sb.Append(char.ToLowerInvariant(c));
        }

        return sb.ToString() switch
        {
            "rustbelt" => "rust_belt",
            "glasscoast" => "glass_coast",
            "ashplains" => "ash_plains",
            "mountains" => "mountains",
            _ => DefaultBiome,
        };
    }

    /// <summary>The ambience key now: open_sea when <paramref name="waterFraction"/> is high (<see cref="SeaEnter"/>, or above <see cref="SeaLeave"/> if the sea already plays), else the biome of the theater.</summary>
    public static string Choose(string? theaterId, double waterFraction, string? previous)
    {
        double threshold = previous == OpenSea ? SeaLeave : SeaEnter;
        return waterFraction >= threshold ? OpenSea : BiomeOf(theaterId);
    }

    /// <summary>The share (0..1) of water cells in the square of <see cref="WaterRadiusCells"/> around <paramref name="centre"/> (the map centre when null). 0 for a snapshot with no land grid.</summary>
    public static double WaterFraction(SimSnapshot snapshot, LeptonCoord? centre)
    {
        int w = snapshot.MapWidth;
        int h = snapshot.MapHeight;
        if (w <= 0 || h <= 0 || snapshot.Land.Count != w * h) return 0;
        CellCoord c = centre?.ToCell() ?? new CellCoord(w / 2, h / 2);
        int x0 = Math.Max(0, c.X - WaterRadiusCells);
        int x1 = Math.Min(w - 1, c.X + WaterRadiusCells);
        int y0 = Math.Max(0, c.Y - WaterRadiusCells);
        int y1 = Math.Min(h - 1, c.Y + WaterRadiusCells);
        if (x1 < x0 || y1 < y0) return 0;
        int water = 0;
        int total = 0;
        for (int y = y0; y <= y1; y += 2)
        {
            for (int x = x0; x <= x1; x += 2)
            {
                total++;
                if (snapshot.Land[(y * w) + x] == LandType.Water) water++;
            }
        }

        return total == 0 ? 0 : (double)water / total;
    }
}
