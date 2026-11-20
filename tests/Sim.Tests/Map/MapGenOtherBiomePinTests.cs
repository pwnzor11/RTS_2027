// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Data;
using Rts.Sim.Map;

namespace Rts.Sim.Tests.Map;

/// <summary>M13-33: the mountains economy fix (field edge margin) must leave every other biome's generated maps byte-identical. These are the map
/// hashes (<see cref="MapHasher"/>) of rust_belt, glass_coast and ash_plains, 2 players, seeds 1 to 3 at all three sizes, plus the north+south ocean
/// variant at Medium (what an Expedition match plays on), recorded before the change. A deliberate generator change to those biomes re-pins them.</summary>
public sealed class MapGenOtherBiomePinTests
{
    [Theory]
    [InlineData(Biome.RustBelt, MapSize.Small, 1UL, false, 0x9a5aff818bcb3479UL)]
    [InlineData(Biome.RustBelt, MapSize.Small, 2UL, false, 0x30d6a9ded8f5acb3UL)]
    [InlineData(Biome.RustBelt, MapSize.Small, 3UL, false, 0xc590a56760b54e41UL)]
    [InlineData(Biome.RustBelt, MapSize.Medium, 1UL, false, 0x09378d3102f55d9dUL)]
    [InlineData(Biome.RustBelt, MapSize.Medium, 2UL, false, 0x258c2bb02406cb8cUL)]
    [InlineData(Biome.RustBelt, MapSize.Medium, 3UL, false, 0x34db30b3c5b3dac2UL)]
    [InlineData(Biome.RustBelt, MapSize.Large, 1UL, false, 0x79f81b7b024d1c72UL)]
    [InlineData(Biome.RustBelt, MapSize.Large, 2UL, false, 0x56b93b100e921c4aUL)]
    [InlineData(Biome.RustBelt, MapSize.Large, 3UL, false, 0xd3225a22c095821bUL)]
    [InlineData(Biome.GlassCoast, MapSize.Small, 1UL, false, 0xe6604890cc6143c1UL)]
    [InlineData(Biome.GlassCoast, MapSize.Small, 2UL, false, 0x64dcbba460a74639UL)]
    [InlineData(Biome.GlassCoast, MapSize.Small, 3UL, false, 0xbe8009b96defcdbbUL)]
    [InlineData(Biome.GlassCoast, MapSize.Medium, 1UL, false, 0xf1241fd4cdaa5a6bUL)]
    [InlineData(Biome.GlassCoast, MapSize.Medium, 2UL, false, 0x65a2ffc10a6da8eeUL)]
    [InlineData(Biome.GlassCoast, MapSize.Medium, 3UL, false, 0xccf936f67449763fUL)]
    [InlineData(Biome.GlassCoast, MapSize.Large, 1UL, false, 0xb2f6ff606355c882UL)]
    [InlineData(Biome.GlassCoast, MapSize.Large, 2UL, false, 0x6cb0569e7ec9d1abUL)]
    [InlineData(Biome.GlassCoast, MapSize.Large, 3UL, false, 0x1d7df72109d447ceUL)]
    [InlineData(Biome.AshPlains, MapSize.Small, 1UL, false, 0xd3b8916ac079faadUL)]
    [InlineData(Biome.AshPlains, MapSize.Small, 2UL, false, 0x7fd550640d54fd00UL)]
    [InlineData(Biome.AshPlains, MapSize.Small, 3UL, false, 0x207ef6655260d674UL)]
    [InlineData(Biome.AshPlains, MapSize.Medium, 1UL, false, 0x4156797b42c99c51UL)]
    [InlineData(Biome.AshPlains, MapSize.Medium, 2UL, false, 0x06eabad1cf03d1d3UL)]
    [InlineData(Biome.AshPlains, MapSize.Medium, 3UL, false, 0x22e468ff94397c38UL)]
    [InlineData(Biome.AshPlains, MapSize.Large, 1UL, false, 0x4730b3ce36ce47f1UL)]
    [InlineData(Biome.AshPlains, MapSize.Large, 2UL, false, 0x088af0cb0f0af201UL)]
    [InlineData(Biome.AshPlains, MapSize.Large, 3UL, false, 0x2f9287f3ddcf62eaUL)]
    [InlineData(Biome.RustBelt, MapSize.Medium, 1UL, true, 0xd7578d1e887ae932UL)]
    [InlineData(Biome.RustBelt, MapSize.Medium, 2UL, true, 0x2173baebd7934c97UL)]
    [InlineData(Biome.RustBelt, MapSize.Medium, 3UL, true, 0x9453eee319ccb3c9UL)]
    [InlineData(Biome.GlassCoast, MapSize.Medium, 1UL, true, 0x8abc9109937eefebUL)]
    [InlineData(Biome.GlassCoast, MapSize.Medium, 2UL, true, 0x10ef53dadbdea303UL)]
    [InlineData(Biome.GlassCoast, MapSize.Medium, 3UL, true, 0x0acde92bebda7bb2UL)]
    [InlineData(Biome.AshPlains, MapSize.Medium, 1UL, true, 0x548e9fbdc54955baUL)]
    [InlineData(Biome.AshPlains, MapSize.Medium, 2UL, true, 0xc02a41047070d930UL)]
    [InlineData(Biome.AshPlains, MapSize.Medium, 3UL, true, 0x9d1eedee9616be5eUL)]
    public void OtherBiomes_AreUnchanged(Biome biome, MapSize size, ulong seed, bool ocean, ulong expected)
    {
        var p = new MapGenParams(biome, size, 2, seed, null, ocean ? OceanEdges.North | OceanEdges.South : OceanEdges.None);
        Assert.Equal(expected, MapHasher.Compute(MapGenerator.Generate(p)));
    }
}
