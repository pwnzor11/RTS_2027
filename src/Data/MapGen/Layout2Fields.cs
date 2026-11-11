// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
namespace Rts.Data.MapGen;

/// <summary>
/// Layout 2 (half-turn skeleton) field placement of one biome (v6, Q-69): how far from the start the main fields lie and in how many patches. Not rules; maps are
/// identified by their hash. The harvest travel distance these produce is reported by <c>map report</c> and pinned by a test.
/// </summary>
/// <param name="MainDist">Minimum Chebyshev distance of a main-field cell from the start (the main is 13 to 16 cells east of the start, 7 to 8 west).</param>
/// <param name="MainShare">Richness of a main-field cell against a cell of any other field (4 = equal, 8 = twice, the v5 value). Lower moves money out of the main to the natural, the yards and the bar, which are further from the refinery.</param>
/// <param name="MainCellsPct">Target size of each main-field patch in percent of the v5 size (fewer cells near the refinery: the harvesters empty them sooner and go further).</param>
/// <param name="NaturalPct">Percent of seeds with a natural expansion yard (field 18 to 28 path cells from the start); the layout check wants 100 cells within 30 path cells, so fewer main cells need the natural more often.</param>
/// <param name="MainBands">Number of main-field patches: 1 (one strip along the east wall) or 2 (a north and a south strip).</param>
public sealed record Layout2Fields(int MainDist, int MainBands, int MainShare = 8, int MainCellsPct = 100, int NaturalPct = 40);
