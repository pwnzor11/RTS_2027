// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Data;

namespace Rts.Game.Screens;

/// <summary>
/// The "Map features" choice of the skirmish setup and LAN lobby screens for generated maps (M12-13b): Off, or Stamps (the generator's
/// stamp pass places ruins, groves and craters in the neutral ground, <see cref="MapGenParams.Stamps"/>). Pure, so it is unit-tested without
/// graphics.
/// </summary>
public static class StampChoice
{
    /// <summary>Row text: "Map features: Off" or "Map features: Stamps (ruins, groves, craters)".</summary>
    public static string Label(bool stamps) => stamps ? "Map features: Stamps (ruins, groves, craters)" : "Map features: Off";

    /// <summary>The other choice (there are only two, so any non-zero <paramref name="delta"/> flips it; 0 keeps it).</summary>
    public static bool Step(bool stamps, int delta) => delta == 0 ? stamps : !stamps;

    /// <summary>Generator parameters for a lobby choice: the defaults of a plain generated map plus the stamp switch.</summary>
    public static MapGenParams Params(Biome biome, MapSize size, int players, ulong seed, bool stamps) =>
        new(biome, size, players, seed, Stamps: stamps);
}
