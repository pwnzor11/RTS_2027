// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Microsoft.Xna.Framework;
using Rts.Game.Ui.Skin;

namespace Rts.Game.Tests.Ui.Skin;

public sealed class UiSkinTests
{
    private const string Good = """
        name = "Test"
        text_color = "#E2E4D8"
        text_dim = "#8a8c80"
        accent = "#D8C88A"
        [panel]
        file = "panel.png"
        slice = [4, 5, 6, 7]
        [sidebar_back]
        file = "back.png"
        [button]
        file = "../evil.png"
        slice = [1, 1, 1, 1]
        [tab]
        file = "tab.png"
        slice = [1, 1, 1]
        """;

    [Fact]
    public void ParsesColoursPiecesAndSlices()
    {
        UiSkin s = UiSkin.Parse(Good);
        Assert.Equal("Test", s.Name);
        Assert.Equal(new Color(0xE2, 0xE4, 0xD8), s.TextColor);
        Assert.Equal(new Color(0x8A, 0x8C, 0x80), s.TextDim);
        Assert.Equal(new SkinSlice(4, 5, 6, 7), s.Piece("panel")!.Slice);
        Assert.Equal("back.png", s.Piece("sidebar_back")!.File);
        Assert.Null(s.Piece("sidebar_back")!.Slice);
    }

    [Fact]
    public void BadPiecesAreDropped()
    {
        UiSkin s = UiSkin.Parse(Good);
        Assert.Null(s.Piece("button"));   // path escape
        Assert.Null(s.Piece("tab"));      // slice has 3 numbers
        Assert.Null(s.Piece("card_frame"));
    }

    [Fact]
    public void BadTextGivesEmptySkin()
    {
        UiSkin s = UiSkin.Parse("this is [[ not toml");
        Assert.Equal(0, s.PieceCount);
        Assert.Null(s.TextColor);
    }

    [Theory]
    [InlineData("#12345")]
    [InlineData("123456")]
    [InlineData("#GG0000")]
    public void BadColourIsNull(string text) => Assert.Null(UiSkin.ParseColour(text));
}

public sealed class UiSkinLoaderTests
{
    [Fact]
    public void MissingFolderAndKeyFallBack()
    {
        string root = Path.Combine(Path.GetTempPath(), "uiskin-" + Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(Path.Combine(root, "authority"));
            File.WriteAllText(Path.Combine(root, "authority", "skin.toml"), "name = \"A\"\n[panel]\nfile = \"p.png\"\nslice = [1,1,1,1]\n");
            var loader = new UiSkinLoader(root);
            Assert.Null(loader.Skin("directorate"));
            Assert.Null(loader.Skin(null));
            UiSkin? a = loader.Skin("authority");
            Assert.NotNull(a);
            Assert.Same(a, loader.Skin("authority"));   // cached
            Assert.NotNull(a!.Piece("panel"));
            Assert.Null(a.Piece("tab"));
            Assert.Null(loader.Texture("authority", "panel"));   // no device
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
    }

    [Fact]
    public void FactionFolders()
    {
        Assert.Equal("left_behind", UiSkinLoader.FolderFor(Rts.Sim.Rules.Faction.LeftBehind));
        Assert.Null(UiSkinLoader.FolderFor(Rts.Sim.Rules.Faction.Neutral));
    }
}
