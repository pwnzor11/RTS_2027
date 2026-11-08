// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Game.Render;

namespace Rts.Game.Tests.Render;

/// <summary>The data-backed 0 A.D. prop atlas, the layering over the procedural art, and the licence paperwork that must travel with it.</summary>
public sealed class ZeroAdPropAtlasTests
{
    private static readonly string[] Theaters = { "rustbelt", "glass_coast", "mountains", "ash_plains" };
    private static readonly PropKind[] Covered = { PropKind.Boulder, PropKind.Rubble, PropKind.ConcreteBlock, PropKind.Snag };

    public static IEnumerable<object[]> TheaterIds() => Theaters.Select(t => new object[] { t });

    private static string Dir() => ZeroAdPropAtlas.FindDirectory() ?? throw new InvalidOperationException("data/sprites/props/zeroad not found");

    [Theory]
    [MemberData(nameof(TheaterIds))]
    public void EveryCoveredFrameHasBodyOneBitAlphaAndStaysInsideItsCell(string theater)
    {
        ZeroAdPropAtlas atlas = ZeroAdPropAtlas.Load(theater, Dir());
        foreach (PropKind kind in Covered)
        {
            Assert.True(atlas.Covers(kind), kind.ToString());
            Assert.True(atlas.VariantCount(kind) >= 3);
            for (int v = 0; v < atlas.VariantCount(kind); v++)
            {
                PropRect f = atlas.Frame(kind, v);
                int solid = 0, border = 0;
                for (int y = 0; y < f.Height; y++)
                    for (int x = 0; x < f.Width; x++)
                    {
                        byte a = atlas.Rgba[((f.Y + y) * atlas.Width + f.X + x) * 4 + 3];
                        Assert.True(a == 0 || a == 255, $"{theater}/{kind}/{v}: partial alpha {a}");
                        if (a == 255) solid++;
                        if (a != 0 && (x == 0 || y == 0 || x == f.Width - 1 || y == f.Height - 1)) border++;
                    }

                Assert.True(solid >= 80, $"{theater}/{kind}/{v}: {solid} solid pixels");
                Assert.Equal(0, border);
                Assert.InRange(f.OriginX, 0, f.Width);
                Assert.InRange(f.OriginY, 0, f.Height);
            }
        }
    }

    [Fact]
    public void KindsWithoutZeroAdArtAreNotCovered()
    {
        ZeroAdPropAtlas atlas = ZeroAdPropAtlas.Load("rustbelt", Dir());
        foreach (PropKind kind in new[] { PropKind.GlassCrest, PropKind.IceSlab, PropKind.DeadTree, PropKind.WreckHulk, PropKind.BrokenPylon })
            Assert.False(atlas.Covers(kind), kind.ToString());
    }

    [Theory]
    [MemberData(nameof(TheaterIds))]
    public void LayeredAtlasTakesCoveredKindsFromZeroAdAndTheRestFromProcedural(string theater)
    {
        TerrainPalette palette = TerrainPalette.Load(theater);
        ZeroAdPropAtlas zero = ZeroAdPropAtlas.Load(theater, Dir());
        var procedural = new ProceduralPropAtlas(palette);
        var layered = new LayeredPropAtlas(new IPropAtlas[] { zero, procedural });
        foreach (PropKind kind in Enum.GetValues<PropKind>())
        {
            bool fromZero = zero.Covers(kind);
            Assert.Equal(fromZero ? 0 : 1, layered.SourceOf(kind));
            IPropAtlas src = fromZero ? zero : procedural;
            Assert.Equal(src.VariantCount(kind), layered.VariantCount(kind));
            for (int v = 0; v < layered.VariantCount(kind); v++)
            {
                PropRect a = layered.Frame(kind, v), b = src.Frame(kind, v);
                Assert.Equal((b.Width, b.Height, b.OriginX, b.OriginY), (a.Width, a.Height, a.OriginX, a.OriginY));
                for (int y = 0; y < a.Height; y++)
                    Assert.True(layered.Rgba.AsSpan(((a.Y + y) * layered.Width + a.X) * 4, a.Width * 4)
                        .SequenceEqual(src.Rgba.AsSpan(((b.Y + y) * src.Width + b.X) * 4, b.Width * 4)), $"{theater}/{kind}/{v} row {y}");
            }
        }
    }

    [Fact]
    public void ABadDirectoryOrUnknownTheaterFallsBackToProceduralOnly()
    {
        Assert.Null(ZeroAdPropAtlas.TryLoad("rustbelt", Path.Combine(Path.GetTempPath(), "no_such_zeroad_dir")));
        Assert.Null(ZeroAdPropAtlas.TryLoad("no_such_theater", Dir()));
        IPropAtlas atlas = PropAtlases.Create(TerrainPalette.Load("rustbelt"), "no_such_theater");
        Assert.IsType<ProceduralPropAtlas>(atlas);
    }

    [Fact]
    public void CreateUsesZeroAdWhereItHasArt()
    {
        IPropAtlas atlas = PropAtlases.Create(TerrainPalette.Load("rustbelt"), "rustbelt");
        if (Environment.GetEnvironmentVariable(PropAtlases.ProceduralOnlyVariable) == "1") return;
        var layered = Assert.IsType<LayeredPropAtlas>(atlas);
        Assert.Equal(0, layered.SourceOf(PropKind.Boulder));
        Assert.Equal(1, layered.SourceOf(PropKind.GlassCrest));
    }

    [Fact]
    public void LicenceNoticesTravelWithTheArt()
    {
        string dir = Dir();
        string lic = File.ReadAllText(Path.Combine(dir, "LICENSE.txt")).Replace("\r\n", "\n");
        Assert.Contains("CC BY-SA", lic.Replace("Creative Commons Attribution-ShareAlike 3.0 Unported", "CC BY-SA"));
        Assert.Contains("0 A.D.", lic);
        string root = dir;
        while (root is not null && !File.Exists(Path.Combine(root, "THIRD_PARTY_NOTICES.md"))) root = Path.GetDirectoryName(root)!;
        Assert.NotNull(root);
        string notices = File.ReadAllText(Path.Combine(root, "THIRD_PARTY_NOTICES.md")).Replace("\r\n", "\n");
        Assert.Contains("0 A.D.", notices);
        Assert.Contains("ShareAlike 3.0", notices);
    }
}
