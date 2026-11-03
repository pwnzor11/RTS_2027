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

    // House palette (D-068): chosen to contrast faction body paint, so no green (the Authority body is grey-green).
    private static readonly Color[] Bases =
    {
        new(0xE0, 0xA0, 0x20), // 0 amber
        new(0xB8, 0x2C, 0x1C), // 1 red
        new(0x2F, 0x76, 0xC0), // 2 blue
        new(0xE6, 0xE6, 0xDE), // 3 white
        new(0x93, 0x60, 0xC8), // 4 purple
        new(0x2F, 0xB8, 0xC4), // 5 cyan
        new(0xE8, 0x6A, 0x1A), // 6 orange
        new(0xE0, 0x6A, 0xA8), // 7 pink
    };

    /// <summary>Display names of the house colours, in palette order.</summary>
    public static readonly IReadOnlyList<string> Names =
        new[] { "Amber", "Red", "Blue", "White", "Purple", "Cyan", "Orange", "Pink" };

    private static int[] _choiceBySlot = { 0, 1, 2, 3, 4, 5, 6, 7 };

    /// <summary>A palette entry by index (0..Count-1), regardless of slot assignment.</summary>
    public static Color Choice(int index) => Bases[((index % Count) + Count) % Count];

    /// <summary>
    /// Assigns palette entries to player slots for the current game (front end only; never sim state): slot s draws with
    /// palette entry <paramref name="choiceBySlot"/>[s]. Slots not listed keep their default (slot % 8).
    /// </summary>
    public static void Assign(IReadOnlyList<int> choiceBySlot)
    {
        var map = new int[Count];
        for (int i = 0; i < Count; i++) map[i] = i < choiceBySlot.Count ? ((choiceBySlot[i] % Count) + Count) % Count : i;
        _choiceBySlot = map;
    }

    /// <summary>Restores the default slot → colour assignment.</summary>
    public static void ResetAssignment() => _choiceBySlot = new[] { 0, 1, 2, 3, 4, 5, 6, 7 };

    /// <summary>
    /// The house colour of a player slot under the current assignment. Slots 8 and above wrap around (slot % 8), so
    /// a LAN game with nine or more players would share colours; the game does not allow that yet.
    /// </summary>
    /// <param name="player">Player slot, zero based.</param>
    public static Color Base(int player)
    {
        if (player < 0) throw new ArgumentOutOfRangeException(nameof(player), "Player slots start at 0.");
        return Bases[_choiceBySlot[player % Count]];
    }
}
