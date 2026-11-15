// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Game.Ui.Skin;

namespace Rts.Game.Tests.Ui.Skin;

/// <summary>M10-15 (s27): every shipped faction skin has all ten pieces, the same 9-slice margins as the approved Authority skin, PNGs at
/// the size the margins need, and a name and text colour.</summary>
public sealed class ShippedSkinsTests
{
    private static readonly string[] Pieces =
        ["panel", "button", "button_hover", "button_pressed", "button_disabled", "tab", "tab_active", "sidebar_back", "minimap_frame", "card_frame"];

    private static string SkinsRoot()
    {
        string rules = Rts.Data.RulesLocator.FindRulesDir(AppContext.BaseDirectory) ?? throw new InvalidOperationException("data/rules not found");
        return Path.Combine(Path.GetDirectoryName(Path.GetFullPath(rules))!, "ui", "skins");
    }

    [Theory]
    [InlineData("directorate")]
    [InlineData("left_behind")]
    [InlineData("expedition")]
    public void Faction_skin_is_complete_and_matches_the_authority_layout(string folder)
    {
        string root = SkinsRoot();
        UiSkin auth = UiSkin.Parse(File.ReadAllText(Path.Combine(root, "authority", "skin.toml")));
        UiSkin s = UiSkin.Parse(File.ReadAllText(Path.Combine(root, folder, "skin.toml")));
        Assert.False(string.IsNullOrWhiteSpace(s.Name));
        Assert.NotNull(s.TextColor);
        foreach (string key in Pieces)
        {
            SkinPiece? p = s.Piece(key);
            Assert.True(p is not null, $"{folder}: piece {key} missing");
            Assert.Equal(auth.Piece(key)!.Slice, p!.Slice);
            string png = Path.Combine(root, folder, p.File);
            Assert.True(File.Exists(png), $"{folder}: {p.File} missing");
            byte[] head = File.ReadAllBytes(png).AsSpan(0, 24).ToArray();
            Assert.Equal(0x89, head[0]);   // a real PNG, not an LFS pointer
            int w = (head[16] << 24) | (head[17] << 16) | (head[18] << 8) | head[19];
            int h = (head[20] << 24) | (head[21] << 16) | (head[22] << 8) | head[23];
            if (p.Slice is SkinSlice sl)
            {
                Assert.True(w > sl.Left + sl.Right && h > sl.Top + sl.Bottom, $"{folder}: {key} {w}x{h} too small for its slice");
            }
        }
    }
}
