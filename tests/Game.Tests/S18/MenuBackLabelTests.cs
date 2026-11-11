// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Rts.Game.Screens;
using Rts.Game.Screens.Editor;
using Rts.Game.Ui;
using Rts.Game.Ui.Fonts;

namespace Rts.Game.Tests.S18;

/// <summary>S18 UX-LM-10 and UX-LM-14: the Back button of How to Play and About is as wide as its label needs, and the map editor's rows,
/// buttons and status line are at least one text line tall.</summary>
[Collection("UiStatics")]
public sealed class MenuBackLabelTests
{
    public static IEnumerable<object[]> Cases()
    {
        foreach (TextSize size in Enum.GetValues<TextSize>())
            foreach (int percent in new[] { 100, 150, 200 })
                yield return new object[] { size, percent };
    }

    private static void With(TextSize size, int percent, Action<UiUnit> body)
    {
        float before = UiScale.TextFactor;
        try
        {
            UiScale.TextFactor = UiScale.FactorOf(size);
            body(UiUnit.FromScale(UiScale.Resolve(1280, 720, percent).Scale));
        }
        finally
        {
            UiScale.TextFactor = before;
        }
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public void The_Back_label_fits_its_box_on_How_to_Play_and_About(TextSize size, int percent) => With(size, percent, unit =>
    {
        var vp = new Viewport(0, 0, 1280, 720);
        int label = BitmapFont.Measure("Back", unit);
        Rectangle howTo = HowToPlayScreen.BackRectFor(vp, unit);
        Rectangle about = AboutScreen.BackRect(vp, unit);
        Assert.True(howTo.Width >= label, $"How to Play: Back box {howTo.Width} px, label {label} px ({size}, {percent}%)");
        Assert.True(about.Width >= label, $"About: Back box {about.Width} px, label {label} px ({size}, {percent}%)");
        Assert.True(new Rectangle(0, 0, 1280, 720).Contains(howTo) && new Rectangle(0, 0, 1280, 720).Contains(about));
    });

    [Theory]
    [MemberData(nameof(Cases))]
    public void The_editor_bars_are_tall_enough_for_a_line_of_text(TextSize size, int percent) => With(size, percent, unit =>
    {
        EditorLayout layout = EditorLayout.Compute(1280, 720, unit);
        int line = BitmapFont.LineHeight(unit, FontWeight.Regular);
        Assert.True(layout.StatusBar.Height >= line, $"status line {layout.StatusBar.Height} px for a {line} px text ({size}, {percent}%)");
        Assert.True(layout.TopBar.Height >= line, $"top bar {layout.TopBar.Height} px for a {line} px text ({size}, {percent}%)");
        Assert.True(EditorLayout.RowHeight(unit) >= line);
        var flow = new FlowLayout(layout.TopBar, unit);
        flow.Add("New", false, () => { });
        EditorButton button = flow.Buttons[0];
        Assert.True(button.Rect.Height >= line, "a button is shorter than its text");
        Assert.True(layout.TopBar.Contains(button.Rect), "the first toolbar button leaves the top bar");
        Assert.True(layout.StatusBar.Bottom <= 720);
    });
}
