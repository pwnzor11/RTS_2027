// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Game.Render;

namespace Rts.Game.Tests.Render;

/// <summary>The Blender-rendered prop atlas (S29): manifest parse, a frame for every kind and variant, hard alpha, and its place at the front of <see cref="PropAtlases"/>.</summary>
public sealed class BlenderPropAtlasTests
{
    private static readonly string[] Theaters = { "rustbelt", "glass_coast", "mountains", "ash_plains" };
    private static readonly PropKind[] Covered = { PropKind.GlassCrest, PropKind.IceSlab, PropKind.WreckHulk, PropKind.BrokenPylon };

    public static IEnumerable<object[]> TheaterIds() => Theaters.Select(t => new object[] { t });

    private static string Dir() => BlenderPropAtlas.FindDirectory() ?? throw new InvalidOperationException("data/sprites/props/blender not found");

    [Theory]
    [MemberData(nameof(TheaterIds))]
    public void EveryKindAndVariantHasARectWithBodyHardAlphaAndNoEdgeContact(string theater)
    {
        BlenderPropAtlas atlas = BlenderPropAtlas.Load(theater, Dir());
        foreach (PropKind kind in Covered)
        {
            Assert.True(atlas.Covers(kind), kind.ToString());
            Assert.Equal(3, atlas.VariantCount(kind));
            for (int v = 0; v < 3; v++)
            {
                PropRect f = atlas.Frame(kind, v);
                Assert.Equal((64, 64, 32, 40), (f.Width, f.Height, f.OriginX, f.OriginY));
                int solid = 0, border = 0;
                for (int y = 0; y < f.Height; y++)
                    for (int x = 0; x < f.Width; x++)
                    {
                        byte a = atlas.Rgba[((f.Y + y) * atlas.Width + f.X + x) * 4 + 3];
                        Assert.True(a == 0 || a == 255, $"{theater}/{kind}/{v}: partial alpha {a}");
                        if (a == 255) solid++;
                        if (a != 0 && (x == 0 || y == 0 || x == f.Width - 1 || y == f.Height - 1)) border++;
                    }

                Assert.True(solid >= 150, $"{theater}/{kind}/{v}: {solid} solid pixels");
                Assert.Equal(0, border);
            }
        }
    }

    [Fact]
    public void OtherKindsAreNotCovered()
    {
        BlenderPropAtlas atlas = BlenderPropAtlas.Load("rustbelt", Dir());
        foreach (PropKind kind in Enum.GetValues<PropKind>().Except(Covered))
            Assert.False(atlas.Covers(kind), kind.ToString());
    }

    [Fact]
    public void ABadDirectoryOrUnknownTheaterReturnsNull()
    {
        Assert.Null(BlenderPropAtlas.TryLoad("rustbelt", Path.Combine(Path.GetTempPath(), "no_such_blender_dir")));
        Assert.Null(BlenderPropAtlas.TryLoad("no_such_theater", Dir()));
    }

    [Theory]
    [MemberData(nameof(TheaterIds))]
    public void CreateTakesTheFourKindsFromBlenderAndEveryKindHasAFrame(string theater)
    {
        IPropAtlas atlas = PropAtlases.Create(TerrainPalette.Load(theater), theater);
        if (Environment.GetEnvironmentVariable(PropAtlases.ProceduralOnlyVariable) == "1") return;
        var layered = Assert.IsType<LayeredPropAtlas>(atlas);
        foreach (PropKind kind in Enum.GetValues<PropKind>())
        {
            Assert.True(layered.VariantCount(kind) >= 1);
            PropRect r = layered.Frame(kind, 2);
            Assert.True(r.X + r.Width <= layered.Width && r.Y + r.Height <= layered.Height, kind.ToString());
        }

        foreach (PropKind kind in Covered) Assert.Equal(0, layered.SourceOf(kind));
    }

    [Fact]
    public void ManifestListsExactlyTheFourKinds()
    {
        string text = File.ReadAllText(Path.Combine(Dir(), "props.toml"));
        foreach (PropKind kind in Covered) Assert.Contains($"name = \"{kind}\"", text);
        Assert.Equal(4, text.Split("[[kind]]").Length - 1);
    }
}
