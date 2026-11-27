// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Data;
using Rts.Data.MapGen;
using Rts.Sim.Map;
using Rts.Sim.Rules;
using Xunit.Abstractions;

namespace Rts.Sim.Tests.Map;

/// <summary>S47 A-06: the optional <c>[biome.&lt;key&gt;.layout2]</c> keys of biomes.toml (field placement and shell mix of layout 2). Defaults must not move any map.</summary>
public sealed class MapGenLayout2KeysTests
{
    private readonly ITestOutputHelper _out;

    /// <summary>Creates the test class.</summary>
    public MapGenLayout2KeysTests(ITestOutputHelper output) => _out = output;

    private static string ShippedText() => File.ReadAllText(BiomeCatalog.FindFile(AppContext.BaseDirectory)!);

    private static MapGenParams P(Biome biome, ulong seed, int layout) => new(biome, MapSize.Medium, 2, seed) { Layout = layout };

    private static readonly Biome[] All = [Biome.RustBelt, Biome.GlassCoast, Biome.Mountains, Biome.AshPlains];

    [Fact]
    public void ShippedFile_HasNoLayout2Table_AndTheDefaultsAreTheV6Values()
    {
        foreach (BiomeParams b in BiomeCatalog.Default) Assert.Null(b.Layout2);
        Assert.Equal(new Layout2Fields(7, 2, 2, 100, 40, 8, 2), BiomeCatalog.Layout2FieldsFor("rust_belt"));
        Assert.Equal(new Layout2Fields(7, 2, 2, 100, 40, 8, 2), BiomeCatalog.Layout2FieldsFor("glass_coast"));
        Assert.Equal(new Layout2Fields(7, 2, 2, 100, 40, 8, 2), BiomeCatalog.Layout2FieldsFor("ash_plains"));
        Assert.Equal(new Layout2Fields(7, 2, 2, 100, 40, 0, 7), BiomeCatalog.Layout2FieldsFor("mountains"));
    }

    [Fact]
    public void Table_OverridesOnlyTheKeysItNames()
    {
        string text = ShippedText() + "\n[biome.rust_belt.layout2]\nshell_soft = 12\nshell_gates2 = 0\nmain_share = 3\n";
        BiomeParams rb = BiomeCatalog.Parse(text, "test.toml")[(int)Biome.RustBelt];
        Assert.Equal(new Layout2Fields(7, 2, 3, 100, 40, 12, 0), rb.Layout2);
        Assert.Null(BiomeCatalog.Parse(text, "test.toml")[(int)Biome.GlassCoast].Layout2);
    }

    [Theory]
    [InlineData("shell_soft = 9\nshell_gates2 = 4", "must not exceed 12")]
    [InlineData("shell_soft = 13", "0..12")]
    [InlineData("main_bands = 3", "1..2")]
    [InlineData("bogus = 1", "unknown key")]
    public void Table_BadValues_AreListed(string body, string expected)
    {
        string text = ShippedText() + "\n[biome.glass_coast.layout2]\n" + body + "\n";
        RulesException e = Assert.Throws<RulesException>(() => BiomeCatalog.Parse(text, "test.toml"));
        Assert.Contains(expected, e.Message);
    }

    [Fact]
    public void Mountains_CannotHaveASoftShell()
    {
        string text = ShippedText() + "\n[biome.mountains.layout2]\nshell_soft = 2\n";
        Assert.Throws<RulesException>(() => BiomeCatalog.Parse(text, "test.toml"));
    }

    // Maps of the shipped defaults (no layout2 table): layout 1 and layout 2 hashes, Medium 2 players, seeds 1..3. Captured on the build before the keys existed.
    private static readonly Dictionary<(Biome, ulong, int), ulong> Pins = new()
    {
        [(Biome.RustBelt, 1UL, 1)] = 0x5c6d7443fc81826dUL,
        [(Biome.RustBelt, 1UL, 2)] = 0x4017fce729ea5ce1UL,
        [(Biome.RustBelt, 2UL, 1)] = 0xb73c9d16e0fc6c0fUL,
        [(Biome.RustBelt, 2UL, 2)] = 0x41bd2be9c0883f57UL,
        [(Biome.RustBelt, 3UL, 1)] = 0x0cc0e9e8bba31844UL,
        [(Biome.RustBelt, 3UL, 2)] = 0x845072feb12f97ccUL,
        [(Biome.GlassCoast, 1UL, 1)] = 0xec88b4536169df69UL,
        [(Biome.GlassCoast, 1UL, 2)] = 0x4ef0ec4581a06608UL,
        [(Biome.GlassCoast, 2UL, 1)] = 0xd67eb8d9c2c125e2UL,
        [(Biome.GlassCoast, 2UL, 2)] = 0xe7ed5a22f19cd507UL,
        [(Biome.GlassCoast, 3UL, 1)] = 0xbf391b8f67f72cafUL,
        [(Biome.GlassCoast, 3UL, 2)] = 0xf52319829a967822UL,
        [(Biome.Mountains, 1UL, 1)] = 0x0a869ef4b9a595b8UL,
        [(Biome.Mountains, 1UL, 2)] = 0x27eb00e3bf5eb38aUL,
        [(Biome.Mountains, 2UL, 1)] = 0x506df3d697f05091UL,
        [(Biome.Mountains, 2UL, 2)] = 0xa6d013ba59fdd2acUL,
        [(Biome.Mountains, 3UL, 1)] = 0xee2497e936f9c14bUL,
        [(Biome.Mountains, 3UL, 2)] = 0x020b764349df90daUL,
        [(Biome.AshPlains, 1UL, 1)] = 0x9c8d5a61c87e9a24UL,
        [(Biome.AshPlains, 1UL, 2)] = 0xa18a905be08ca5b7UL,
        [(Biome.AshPlains, 2UL, 1)] = 0x6f2fcf64cfa64d73UL,
        [(Biome.AshPlains, 2UL, 2)] = 0x0c0a96f2f5049053UL,
        [(Biome.AshPlains, 3UL, 1)] = 0xc80739dbe1783374UL,
        [(Biome.AshPlains, 3UL, 2)] = 0xe624cea6d574ab8bUL,
    };

    [Fact]
    public void Defaults_LeaveEveryLayout1AndLayout2MapUnchanged()
    {
        foreach (Biome b in All)
            for (ulong seed = 1; seed <= 3; seed++)
                foreach (int layout in new[] { 1, 2 })
                {
                    ulong h = MapHasher.Compute(MapGenerator.Generate(P(b, seed, layout)));
                    if (Pins.Count == 0) _out.WriteLine($"        [(Biome.{b}, {seed}UL, {layout})] = 0x{h:x16}UL,");
                    else Assert.Equal(Pins[(b, seed, layout)], h);
                }
        Assert.NotEmpty(Pins);
    }
}
