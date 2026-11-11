// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Microsoft.Xna.Framework;
using Rts.Game.Menu;
using Rts.Game.Ui;

namespace Rts.Game.Tests.S18;

/// <summary>S18 UX-LM-01: the main menu must fit a 1280x720 window at every UI scale and text size.</summary>
[Collection("UiStatics")]
public sealed class MenuMainLayoutTests
{
    private const int W = 1280, H = 720;
    private const string LongCaption = "Glass Coast - grey ash dunes, fused sand and drowned streets";

    public static IEnumerable<object[]> Cases()
    {
        foreach (TextSize size in Enum.GetValues<TextSize>())
            foreach (int percent in new[] { 100, 150, 200 })
                yield return new object[] { size, percent };
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public void Every_item_is_inside_the_window_and_clear_of_the_footer(TextSize size, int percent)
    {
        float before = UiScale.TextFactor;
        try
        {
            UiScale.TextFactor = UiScale.FactorOf(size);
            UiUnit unit = UiUnit.FromScale(UiScale.Resolve(W, H, percent).Scale);
            foreach (bool hasSave in new[] { false, true })
            {
                IReadOnlyList<MainMenuEntry> entries = MainMenuModel.Build(hasSave);
                LobbyLayout layout = LobbyMenuView.Measure(W, H, unit, entries);
                LobbyFooter footer = LobbyMenuView.Footer(layout, LongCaption, "0.1.5+e059f0f", W, H);
                var window = new Rectangle(0, 0, W, H);

                Assert.True(layout.TitleY >= 0, $"title off the top ({size}, {percent}%)");
                for (int i = 0; i < layout.Rows.Length; i++)
                {
                    Rectangle r = layout.Rows[i];
                    Assert.True(window.Contains(r), $"row {i} {entries[i].Label} {r} outside the window ({size}, {percent}%)");
                    Assert.True(r.Bottom <= footer.Caption.Y, $"row {i} {entries[i].Label} overlaps the caption ({size}, {percent}%)");
                    Assert.True(r.Bottom <= footer.Version.Y, $"row {i} overlaps the version line ({size}, {percent}%)");
                }
                Assert.True(window.Contains(footer.Version), $"version line outside the window ({size}, {percent}%)");
                if (footer.Caption.Width > 0)
                {
                    Assert.True(window.Contains(footer.Caption), $"caption outside the window ({size}, {percent}%)");
                    Assert.False(footer.Caption.Intersects(footer.Version), $"caption overprints the version ({size}, {percent}%)");
                }
            }
        }
        finally
        {
            UiScale.TextFactor = before;
        }
    }
}
