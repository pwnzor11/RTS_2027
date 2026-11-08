// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Sim.Rules;

namespace Rts.Data.MapGen;

/// <summary>One blocking prop kind with its weight in a draw (M12-05).</summary>
/// <param name="Kind">The overlay kind (one of the prop kinds).</param>
/// <param name="Weight">Relative weight, at least 1.</param>
public readonly record struct PropWeight(OverlayKind Kind, int Weight);

/// <summary>
/// Decoration rules of one biome (<c>[biome.&lt;key&gt;.decor]</c> in <c>data/mapgen/biomes.toml</c>; design 16 §2.5, defaults from the
/// community-map study, design 17). Generator-only data, never in RulesHash. Starting values, all (tune).
/// </summary>
/// <param name="PropPermille">Blocking prop cells as a share of all cells, in per mille (study: 2.0 per 100 = 20, range 4 to 36).</param>
/// <param name="ClusterMin">Fewest cells of a grove, wreck field or barrier line.</param>
/// <param name="ClusterMax">Most cells of a cluster.</param>
/// <param name="SinglePermille">Share of clusters that are one lone prop.</param>
/// <param name="GapPermille">Chance per cluster cell of leaving a hole, so infantry can weave through.</param>
/// <param name="CliffPermille">Seed weight (1000 = neutral) beside cliffs or rock (study: 1.6x).</param>
/// <param name="MapEdgePermille">Seed weight within 5 cells of the map edge (study: 2x).</param>
/// <param name="WaterPermille">Seed weight beside water (study: 1.7x).</param>
/// <param name="RoadPermille">Seed weight near roads (study: 0.35x).</param>
/// <param name="YardMin">Fewest prop cells around each neutral ruin.</param>
/// <param name="YardMax">Most prop cells around each neutral ruin.</param>
/// <param name="DeadTargetPermille">Share of cells with nothing within 4 cells that the fill pass aims below (study: 1 to 3 %).</param>
/// <param name="Kinds">Prop kinds of ordinary clusters.</param>
/// <param name="YardKinds">Prop kinds of ruin yards.</param>
public sealed record DecorParams(
    int PropPermille, int ClusterMin, int ClusterMax, int SinglePermille, int GapPermille,
    int CliffPermille, int MapEdgePermille, int WaterPermille, int RoadPermille,
    int YardMin, int YardMax, int DeadTargetPermille,
    IReadOnlyList<PropWeight> Kinds, IReadOnlyList<PropWeight> YardKinds)
{
    /// <summary>No props at all (used by tests that need the bare generator).</summary>
    public static readonly DecorParams None = new(0, 3, 6, 0, 0, 1000, 1000, 1000, 1000, 0, 0, 1000,
        new[] { new PropWeight(OverlayKind.Tree, 1) }, new[] { new PropWeight(OverlayKind.Wreck, 1) });
}
