// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).

namespace Rts.Game.Render;

/// <summary>
/// One line of a theater's decal table (<c>[[decor.decal]]</c>): what is drawn, on which land types, how often and how big.
/// </summary>
/// <param name="Name">Label, for tests and the contact sheet.</param>
/// <param name="Shape">Outline.</param>
/// <param name="LandMask">Bit <c>1 &lt;&lt; (int)LandType</c> is set for each land type the decal may sit on (texel by texel, so it never spills onto water or a cliff).</param>
/// <param name="Permille">Chance per eligible cell (of 1000) that a decal of this rule is centred there. The rules that match one land type are tried in file order and their chances add up, so their sum should stay under 1000.</param>
/// <param name="Colour">Main colour.</param>
/// <param name="Alpha">Opacity 1..255.</param>
/// <param name="SizeMin">Smallest extent, in sixteenths of a cell.</param>
/// <param name="SizeMax">Largest extent, in sixteenths of a cell.</param>
public sealed record DecalRule(string Name, DecalShape Shape, int LandMask, int Permille, TerrainPalette.Rgb Colour, int Alpha, int SizeMin, int SizeMax)
{
    /// <summary>True when the rule may sit on this land type.</summary>
    public bool Allows(Rts.Sim.Rules.LandType land) => (LandMask >> (int)land & 1) != 0;
}
