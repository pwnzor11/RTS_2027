// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Data;
using Rts.Game.Screens;
using Rts.Sim.Core;
using Rts.Sim.Map;
using Rts.Sim.Rules;

namespace Rts.Game.Tests.M7;

/// <summary>M7-14 (11_m7_plan §6 bullet 5): the generator always gives an Expedition a landing coast, whenever the map has any ocean edge;
/// the setup screen turns "no ocean" into a south edge for an Expedition slot, so a generated map never ends without one.</summary>
public sealed class LandingCoastGeneratorTests
{
    public static IEnumerable<object[]> Oceans()
    {
        foreach (OceanEdges o in new[] { OceanEdges.North, OceanEdges.South, OceanEdges.East, OceanEdges.West, OceanEdges.North | OceanEdges.South })
            yield return new object[] { o };
    }

    [Theory]
    [MemberData(nameof(Oceans))]
    public void EveryBiomeSizeAndSeed_WithAnOceanEdge_HasLandingMarkers(OceanEdges ocean)
    {
        foreach (Biome biome in new[] { Biome.RustBelt, Biome.GlassCoast, Biome.Mountains, Biome.AshPlains })
            foreach (MapSize size in new[] { MapSize.Small, MapSize.Medium, MapSize.Large })
                foreach (int players in new[] { 2, 4 })
                    for (ulong seed = 1; seed <= 6; seed++)
                    {
                        var gen = MapGenerator.GenerateWithMeta(new MapGenParams(biome, size, players, seed, null, ocean));
                        Assert.True(gen.Meta.Landing.Count > 0, $"no landing coast: {biome} {size} players={players} seed={seed} ocean={ocean}");
                        foreach (CellCoord c in gen.Meta.Landing)
                            Assert.Equal(LandType.Coast, gen.Map.Land[c.Y * gen.Map.Width + c.X]);
                    }
    }

    [Fact]
    public void ExpeditionOnAGeneratedMapWithNoOcean_IsGivenOne_AndThenHasLandingMarkers()
    {
        OceanEdges ocean = ExpeditionSetup.ResolveOcean(OceanEdges.None, expeditionOnGeneratedMap: true, out _);
        Assert.NotEqual(OceanEdges.None, ocean);
        foreach (MapSize size in new[] { MapSize.Small, MapSize.Medium, MapSize.Large })
            for (ulong seed = 1; seed <= 8; seed++)
                Assert.NotEmpty(MapGenerator.GenerateWithMeta(new MapGenParams(Biome.RustBelt, size, 2, seed, null, ocean)).Meta.Landing);
    }
}
