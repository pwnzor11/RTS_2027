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
    [InlineData(Biome.RustBelt, MapSize.Small, 1UL, false, 0x16cf8da9d18a7ff5UL)]
    [InlineData(Biome.RustBelt, MapSize.Small, 2UL, false, 0xda2cced6c179232cUL)]
    [InlineData(Biome.RustBelt, MapSize.Small, 3UL, false, 0x5db5582e7f89d8b2UL)]
    [InlineData(Biome.RustBelt, MapSize.Medium, 1UL, false, 0x5c6d7443fc81826dUL)]
    [InlineData(Biome.RustBelt, MapSize.Medium, 2UL, false, 0xb73c9d16e0fc6c0fUL)]
    [InlineData(Biome.RustBelt, MapSize.Medium, 3UL, false, 0x0cc0e9e8bba31844UL)]
    [InlineData(Biome.RustBelt, MapSize.Large, 1UL, false, 0xd4f83fdfdcd4ae40UL)]
    [InlineData(Biome.RustBelt, MapSize.Large, 2UL, false, 0xd4febb2c25b03a98UL)]
    [InlineData(Biome.RustBelt, MapSize.Large, 3UL, false, 0x59e587949bdefbddUL)]
    [InlineData(Biome.GlassCoast, MapSize.Small, 1UL, false, 0x3953b7b5b340c4baUL)]
    [InlineData(Biome.GlassCoast, MapSize.Small, 2UL, false, 0xba837f76fc5ccc9aUL)]
    [InlineData(Biome.GlassCoast, MapSize.Small, 3UL, false, 0x648783d805bb3ac8UL)]
    [InlineData(Biome.GlassCoast, MapSize.Medium, 1UL, false, 0xec88b4536169df69UL)]
    [InlineData(Biome.GlassCoast, MapSize.Medium, 2UL, false, 0xd67eb8d9c2c125e2UL)]
    [InlineData(Biome.GlassCoast, MapSize.Medium, 3UL, false, 0xbf391b8f67f72cafUL)]
    [InlineData(Biome.GlassCoast, MapSize.Large, 1UL, false, 0xed4da73860763f06UL)]
    [InlineData(Biome.GlassCoast, MapSize.Large, 2UL, false, 0x808d4afc7deaf8edUL)]
    [InlineData(Biome.GlassCoast, MapSize.Large, 3UL, false, 0x0033572092f8de2fUL)]
    [InlineData(Biome.RustBelt, MapSize.Medium, 1UL, true, 0x0f03c109e97292e7UL)]
    [InlineData(Biome.RustBelt, MapSize.Medium, 2UL, true, 0xe536b265cccf79f1UL)]
    [InlineData(Biome.RustBelt, MapSize.Medium, 3UL, true, 0xef3ffd9de7b2370bUL)]
    [InlineData(Biome.GlassCoast, MapSize.Medium, 1UL, true, 0xd49780fbd52ac932UL)]
    [InlineData(Biome.GlassCoast, MapSize.Medium, 2UL, true, 0xd52b84ccc1420f2cUL)]
    [InlineData(Biome.GlassCoast, MapSize.Medium, 3UL, true, 0x3f0aa3b591c23ac6UL)]
    public void OtherBiomes_AreUnchanged(Biome biome, MapSize size, ulong seed, bool ocean, ulong expected)
    {
        var p = new MapGenParams(biome, size, 2, seed, null, ocean ? OceanEdges.North | OceanEdges.South : OceanEdges.None);
        Assert.Equal(expected, MapHasher.Compute(MapGenerator.Generate(p)));
    }
}
