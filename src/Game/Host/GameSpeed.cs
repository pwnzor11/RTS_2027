// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
namespace Rts.Game.Host;

/// <summary>
/// The single-player game speed (D-205, M13-15): 0.5x, 0.75x, 1x, 1.5x or 2x. The sim step never changes (20 per second of sim time);
/// only how many steps <see cref="SimHost.Advance"/> runs per real second does. A networked game is locked to 1x.
/// </summary>
public static class GameSpeed
{
    /// <summary>The speeds on offer, in percent of normal.</summary>
    public static IReadOnlyList<int> Percents { get; } = new[] { 50, 75, 100, 150, 200 };

    /// <summary>Normal speed.</summary>
    public const int Normal = 100;

    /// <summary>The offered percent nearest to <paramref name="percent"/>.</summary>
    public static int Snap(int percent)
    {
        int best = Normal, bestD = int.MaxValue;
        foreach (int p in Percents)
        {
            int d = Math.Abs(p - percent);
            if (d < bestD) { best = p; bestD = d; }
        }

        return best;
    }

    /// <summary>The speed <paramref name="delta"/> places up (positive) or down in the list, stopping at the ends.</summary>
    public static int Step(int percent, int delta)
    {
        int at = Percents.ToList().IndexOf(Snap(percent));
        return Percents[Math.Clamp(at + delta, 0, Percents.Count - 1)];
    }

    /// <summary>The speed that applies: always <see cref="Normal"/> in a networked (LAN) game, else the snapped choice.</summary>
    public static int Effective(int percent, bool networked) => networked ? Normal : Snap(percent);

    /// <summary>"0.5x", "0.75x", "1x", "1.5x", "2x".</summary>
    public static string Label(int percent) => Snap(percent) switch
    {
        50 => "0.5x",
        75 => "0.75x",
        150 => "1.5x",
        200 => "2x",
        _ => "1x",
    };
}
