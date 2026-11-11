// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Data;
using Xunit.Abstractions;

namespace AI.Tests.S18;

/// <summary>S18 audit DATA-TOOLS-06: when no layout-2 attempt passes every layout check in <see cref="MapGenerator.MaxZonedAttempts"/>
/// tries, the generator returns the attempt with the fewest failures and reports it only through the thread-static
/// <see cref="MapGenerator.FailedChecks"/>; nothing in the game shows it.</summary>
public class Layout2RetryAuditTests(ITestOutputHelper output)
{
    // S18 audit: pins current behaviour of DATA-TOOLS-06; flip the assertion when fixed.
    [Theory]
    [InlineData(Biome.GlassCoast, MapSize.Medium, 3, 10UL)]
    [InlineData(Biome.GlassCoast, MapSize.Medium, 4, 10UL)]
    [InlineData(Biome.Mountains, MapSize.Small, 3, 7UL)]
    public void Audit_Layout2_ReturnsAMapThatFailsItsOwnChecks_WithoutAnError(Biome biome, MapSize size, int players, ulong seed)
    {
        var (map, _) = MapGenerator.GenerateWithMeta(new MapGenParams(biome, size, players, seed, null, OceanEdges.None, 2));
        int fails = MapGenerator.FailedChecks;
        output.WriteLine($"{biome} {size} {players}p seed {seed}: {map.Width}x{map.Height}, layout checks still failed {fails}");
        Assert.True(fails > 0, "the map shipped although some layout checks failed");
    }
}
