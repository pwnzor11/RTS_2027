// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Microsoft.Xna.Framework;

namespace Rts.Game.Sprites;

/// <summary>
/// House colours by player slot. Sprites carry their house paint as a greyscale mask (see <see cref="HouseSplit"/>),
/// and the GPU multiplies that mask by the colour returned here, so the lightest shade of paint shows the colour
/// exactly and darker shades are darker versions of it. Values are mid-bright on purpose: a dark base would make
/// every shade of the ramp muddy. Debug-view values, not final art.
/// </summary>
public static class PlayerColours
{
    /// <summary>Number of steps in a house ramp. Must match the length of a sprite manifest's house ramp.</summary>
    public const int RampSteps = 8;

    /// <summary>Number of distinct house colours. Slots 0 to 7 are all distinct; slot 8 and above wrap to slot % 8.</summary>
    public const int Count = 8;

    private static readonly Color[] Bases =
    {
        new(0x4F, 0xA0, 0x4A), // 0 Authority green
        new(0xC0, 0x46, 0x2B), // 1 Directorate rust red
        new(0x2F, 0x76, 0xC0), // 2 blue
        new(0xE3, 0xC2, 0x3A), // 3 yellow
        new(0x93, 0x60, 0xC8), // 4 purple
        new(0x2F, 0xB8, 0xC4), // 5 cyan
        new(0xE8, 0x82, 0x2A), // 6 orange
        new(0xD8, 0xD8, 0xD2), // 7 white-grey
    };

    /// <summary>
    /// The house colour of a player slot. Slots 0 to 7 are distinct. Slots 8 and above wrap around (slot % 8), so
    /// a LAN game with nine or more players would share colours; the game does not allow that yet.
    /// </summary>
    /// <param name="player">Player slot, zero based.</param>
    public static Color Base(int player)
    {
        if (player < 0) throw new ArgumentOutOfRangeException(nameof(player), "Player slots start at 0.");
        return Bases[player % Count];
    }
}
