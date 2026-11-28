// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Data;
using Rts.Sim.Map;

namespace Rts.Sim.Tests.Map;

/// <summary>M13-33: the mountains economy fix (field edge margin) must leave every other biome's generated maps byte-identical. These are the map
/// hashes (<see cref="MapHasher"/>) of rust_belt and glass_coast, 2 players (ash_plains takes the same start_row_shift as mountains, so it is not pinned), seeds 1 to 3 at all three sizes, plus the north+south ocean
/// variant at Medium (what an Expedition match plays on), recorded before the change. A deliberate generator change to those biomes re-pins them.</summary>
public sealed class MapGenOtherBiomePinTests
{
    [Theory]
    [InlineData(Biome.RustBelt, MapSize.Small, 1UL, false, 0x50db33679fed82bfUL)]
    [InlineData(Biome.RustBelt, MapSize.Small, 2UL, false, 0x1d2f8359bcb0ae32UL)]
    [InlineData(Biome.RustBelt, MapSize.Small, 3UL, false, 0xadcda3445dbc71e6UL)]
    [InlineData(Biome.RustBelt, MapSize.Medium, 1UL, false, 0xfe92ec9c0c814ad7UL)]
    [InlineData(Biome.RustBelt, MapSize.Medium, 2UL, false, 0xa8fd981eb7646a89UL)]
    [InlineData(Biome.RustBelt, MapSize.Medium, 3UL, false, 0xbd985eb70916e100UL)]
    [InlineData(Biome.RustBelt, MapSize.Large, 1UL, false, 0x98c2e4a244c604d3UL)]
    [InlineData(Biome.RustBelt, MapSize.Large, 2UL, false, 0xa041c400a9a43466UL)]
    [InlineData(Biome.RustBelt, MapSize.Large, 3UL, false, 0x45d2118af5b41846UL)]
    [InlineData(Biome.GlassCoast, MapSize.Small, 1UL, false, 0x3953b7b5b340c4baUL)]
    [InlineData(Biome.GlassCoast, MapSize.Small, 2UL, false, 0xba837f76fc5ccc9aUL)]
    [InlineData(Biome.GlassCoast, MapSize.Small, 3UL, false, 0x648783d805bb3ac8UL)]
    [InlineData(Biome.GlassCoast, MapSize.Medium, 1UL, false, 0xec88b4536169df69UL)]
    [InlineData(Biome.GlassCoast, MapSize.Medium, 2UL, false, 0xd67eb8d9c2c125e2UL)]
    [InlineData(Biome.GlassCoast, MapSize.Medium, 3UL, false, 0xbf391b8f67f72cafUL)]
    [InlineData(Biome.GlassCoast, MapSize.Large, 1UL, false, 0xed4da73860763f06UL)]
    [InlineData(Biome.GlassCoast, MapSize.Large, 2UL, false, 0x808d4afc7deaf8edUL)]
    [InlineData(Biome.GlassCoast, MapSize.Large, 3UL, false, 0x0033572092f8de2fUL)]
    [InlineData(Biome.RustBelt, MapSize.Medium, 1UL, true, 0x671583da41bc1ef5UL)]
    [InlineData(Biome.RustBelt, MapSize.Medium, 2UL, true, 0x0e60a83a4577676cUL)]
    [InlineData(Biome.RustBelt, MapSize.Medium, 3UL, true, 0xc40124de544db044UL)]
    [InlineData(Biome.GlassCoast, MapSize.Medium, 1UL, true, 0xd49780fbd52ac932UL)]
    [InlineData(Biome.GlassCoast, MapSize.Medium, 2UL, true, 0xd52b84ccc1420f2cUL)]
    [InlineData(Biome.GlassCoast, MapSize.Medium, 3UL, true, 0x3f0aa3b591c23ac6UL)]
    public void OtherBiomes_AreUnchanged(Biome biome, MapSize size, ulong seed, bool ocean, ulong expected)
    {
        var p = new MapGenParams(biome, size, 2, seed, null, ocean ? OceanEdges.North | OceanEdges.South : OceanEdges.None);
        Assert.Equal(expected, MapHasher.Compute(MapGenerator.Generate(p)));
    }
}
