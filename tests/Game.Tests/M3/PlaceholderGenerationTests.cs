// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Game.Render;

namespace Rts.Game.Tests.M3;

/// <summary>M3-08: a type with no sprite file (a Directorate or new Authority id) still gets a recognisable, house-coloured placeholder.</summary>
public sealed class PlaceholderGenerationTests
{
    [Theory]
    [InlineData(SpriteKind.Vehicle)]
    [InlineData(SpriteKind.Infantry)]
    [InlineData(SpriteKind.Building)]
    public void An_unknown_id_resolves_to_a_placeholder_with_house_paint(SpriteKind kind)
    {
        var resolver = new SpriteResolver(null);
        ResolvedSprite sprite = resolver.Resolve(kind, "dir_not_in_any_file", 2, 2);

        Assert.True(sprite.IsPlaceholder);
        Assert.NotNull(sprite.Placeholder);
        PlaceholderPixels p = sprite.Placeholder!;
        Assert.Equal("dir_not_in_any_file", p.Id);
        Assert.Contains(Enumerable.Range(0, p.Mask.Length / 4), i => p.Mask[i * 4 + 3] > 0);   // house paint present
        Assert.Contains(Enumerable.Range(0, p.Base.Length / 4), i => p.Base[i * 4 + 3] > 0);   // body present
    }

    [Fact]
    public void Placeholder_is_cached_per_id()
    {
        var resolver = new SpriteResolver(null);
        Assert.Same(resolver.Resolve(SpriteKind.Vehicle, "dir_bore"), resolver.Resolve(SpriteKind.Vehicle, "dir_bore"));
    }

    [Fact]
    public void Unit_placeholders_carry_the_id_label_so_two_types_do_not_look_the_same()
    {
        PlaceholderPixels bore = PlaceholderArt.Unit("dir_bore", infantry: false);
        PlaceholderPixels hauler = PlaceholderArt.Unit("dir_hauler", infantry: false);
        Assert.Equal(bore.Base.Length, hauler.Base.Length);
        Assert.NotEqual(bore.Base, hauler.Base);

        PlaceholderPixels a = PlaceholderArt.Unit("dir_informant", infantry: true);
        PlaceholderPixels b = PlaceholderArt.Unit("dir_sapper", infantry: true);
        Assert.NotEqual(a.Base, b.Base);
    }

    [Fact]
    public void The_same_id_always_gives_the_same_pixels()
    {
        Assert.Equal(PlaceholderArt.Unit("dir_bore", false).Base, PlaceholderArt.Unit("dir_bore", false).Base);
        Assert.Equal(PlaceholderArt.Building("dir_hatch", 1, 1).Base, PlaceholderArt.Building("dir_hatch", 1, 1).Base);
    }

    [Theory]
    [InlineData("dir_bore", "BORE")]
    [InlineData("dir_command_shaft", "SHAF")]
    [InlineData("auth_decon_truck", "TRUC")]
    public void Labels_come_from_the_last_id_segment(string id, string label) => Assert.Equal(label, PlaceholderArt.ShortName(id));
}
