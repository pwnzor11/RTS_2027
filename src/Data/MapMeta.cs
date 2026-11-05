// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
namespace Rts.Data;

/// <summary>
/// Map metadata beyond <see cref="Rts.Sim.Map.MapData"/> (map format v2, M4): biome key ("" = none), declared symmetry, the most players
/// the map supports, generator provenance (informational, never re-run on load) and neutral features. Not part of the map hash.
/// </summary>
public sealed record MapMeta(string Biome, MapSymmetry Symmetry, int MaxPlayers, MapGenParams? Generator,
    IReadOnlyList<MapFeature> Features)
{
    /// <summary>The metadata a version-1 file (or a map without metadata) gets: no biome, no symmetry, one player per start.</summary>
    public static MapMeta DefaultFor(Rts.Sim.Map.MapData map) =>
        new("", MapSymmetry.None, map.StartPositions.Count, null, Array.Empty<MapFeature>());

    /// <summary>Map format 3 (M7-02): cells on the map edge where Expedition supply tenders sail in (TOML <c>[map] convoy_edge</c>, a list of
    /// <c>[x, y]</c>). Empty = none declared. Not part of the map hash.</summary>
    public IReadOnlyList<Rts.Sim.Core.CellCoord> ConvoyEdge { get; init; } = Array.Empty<Rts.Sim.Core.CellCoord>();

    /// <summary>Map format 3 (M7-02): marker cells of landing beaches (TOML <c>[map] landing</c>, a list of <c>[x, y]</c>), the places an
    /// Expedition Command Ship may deploy and landing craft unload. Empty = none declared. Not part of the map hash.</summary>
    public IReadOnlyList<Rts.Sim.Core.CellCoord> Landing { get; init; } = Array.Empty<Rts.Sim.Core.CellCoord>();
}
