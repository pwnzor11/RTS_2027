// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Microsoft.Xna.Framework;
using Rts.Game.Controls;
using Rts.Game.Screens;
using Rts.Game.Ui;
using Rts.Game.Ui.Alerts;
using Rts.Game.Ui.Card;
using Rts.Game.Ui.Sidebar;

namespace Rts.Game.Tests.Ui3;

/// <summary>M11-10h: the one UI scale rule, and layouts at the extremes of window size and setting.</summary>
public sealed class UiScaleRuleTests
{
    public static IEnumerable<object[]> Sizes() => new[]
    {
        new object[] { 1280, 720 }, new object[] { 1366, 768 }, new object[] { 1600, 900 },
        new object[] { 1920, 1080 }, new object[] { 2560, 1440 },
    };

    [Theory]
    [InlineData(1280, 720, 100, 1f)]
    [InlineData(1280, 720, 75, 0.75f)]
    [InlineData(1280, 720, 150, 1.5f)]
    [InlineData(1920, 1080, 100, 1.5f)]
    [InlineData(1920, 1080, 150, 2.25f)]
    [InlineData(2560, 1440, 100, 2f)]
    public void Effective_scale_is_auto_times_setting(int w, int h, int percent, float expected)
    {
        UiScaleResult r = UiScale.Resolve(w, h, percent);
        Assert.Equal(expected, r.Scale, 3);
        Assert.False(r.Limited);
    }

    [Fact]
    public void A_small_window_clamps_the_scale_and_says_so()
    {
        UiScaleResult r = UiScale.Resolve(1280, 720, 200);
        Assert.True(r.Limited);
        Assert.Equal(2f, r.Requested, 3);
        Assert.Equal(1.6f, r.Scale, 3);
        Assert.True(r.Scale <= UiScale.MaxFit(1280, 720));
        Assert.Equal("UI scale: 200% (limited by window size)", SettingsScreen.UiScaleLine(Settings.Default with { UiScalePercent = 200 }, r));
        Assert.Equal("UI scale: 100%", SettingsScreen.UiScaleLine(Settings.Default, UiScale.Resolve(1280, 720, 100)));
    }

    [Fact]
    public void Steps_stop_at_the_ends_and_snap()
    {
        Assert.Equal(75, UiScale.StepPercent(75, -1));
        Assert.Equal(200, UiScale.StepPercent(200, 1));
        Assert.Equal(125, UiScale.StepPercent(100, 1));
        Assert.Equal(150, UiScale.SnapPercent(140));
    }

    [Fact]
    public void Text_size_scales_only_fonts_and_the_nearest_atlas_size_is_used()
    {
        float before = UiScale.TextFactor;
        try
        {
            UiScale.TextFactor = UiScale.FactorOf(TextSize.Normal);
            int normal = BitmapFont.SizeForUiScale(1f);
            UiScale.TextFactor = UiScale.FactorOf(TextSize.Large);
            int large = BitmapFont.SizeForUiScale(1f);
            Assert.Equal(12, normal);
            Assert.Equal(14, large);   // 12 x 1.25 = 15, nearest atlas size
            Assert.True(UiScale.Resolve(1280, 720, 100).Scale == 1f);   // the scale itself does not move
        }
        finally
        {
            UiScale.TextFactor = before;
        }
    }

    [Fact]
    public void Unit_arithmetic_rounds_to_whole_pixels()
    {
        UiUnit u = UiUnit.FromScale(0.75f);   // 1.5 pixels per font pixel
        Assert.Equal(8, 5 * u);
        Assert.Equal(1, u / 2);
        Assert.Equal(2, u.Whole);
        Assert.Equal(98, 100 - u);
    }

    public static IEnumerable<object[]> Extremes()
    {
        foreach (object[] size in Sizes())
            foreach (int percent in UiScale.Percents)
                foreach (float tf in new[] { 1f, 1.5f })
                    yield return new object[] { size[0], size[1], percent, tf };
    }

    [Theory]
    [MemberData(nameof(Extremes))]
    public void Sidebar_card_and_alerts_never_overlap_or_leave_the_window(int w, int h, int percent, float tf)
    {
        float s = UiScale.Resolve(w, h, percent).Scale;
        var window = new Rectangle(0, 0, w, h);
        SidebarLayout l = SidebarLayout.Compute(w, h, s, 6, supplyRow: true, shipmentRow: true, mapWidth: 64, mapHeight: 64, textFactor: tf);
        Assert.True(window.Contains(l.Panel));
        Assert.Equal(l.Panel.Left, l.MapView.Right);

        // Everything inside the sidebar stays inside it and the rows do not collide.
        Rectangle[] stack = { l.Credits, l.SupplyRow, l.ShipmentRow, l.PowerRow };
        for (int i = 1; i < stack.Length; i++) Assert.True(stack[i].Top >= stack[i - 1].Bottom, $"{w}x{h} {percent}% row {i}");
        foreach (Rectangle r in new[] { l.Credits, l.PowerRow, l.SellButton, l.RepairButton, l.Grid, l.Minimap, l.PowerBar }.Concat(l.Tabs))
            Assert.True(l.Panel.Contains(r), $"{w}x{h} {percent}%: {r} outside {l.Panel}");
        Assert.True(l.Tabs[0].Top >= l.SellButton.Bottom);
        Assert.True(l.Grid.Top >= l.Tabs[0].Bottom);
        Assert.True(l.Grid.Bottom <= l.Minimap.Top);
        Assert.True(l.Rows >= 1, $"{w}x{h} {percent}%: no cameo row fits");

        // The card lives in the map view, so the sidebar cannot cover it, and it is on screen.
        float cs = CardLayout.ScaleFor(l.MapView.Width, s);
        int lh = (int)MathF.Round(14f * cs * tf);
        foreach (CardKind kind in new[] { CardKind.Unit, CardKind.Building, CardKind.Multi })
        {
            CardLayout c = CardLayout.Compute(l.MapView, cs, lh, kind, 4, 30, CardLayout.TextRows);
            Assert.True(l.MapView.Contains(c.Card), $"{w}x{h} {percent}% {kind}: {c.Card} outside {l.MapView}");
            Assert.False(c.Card.Intersects(l.Panel));
            foreach (Rectangle b in c.Buttons) Assert.True(c.Card.Contains(b));

            // The alert stack drops below anything it would touch and stays in the map view.
            Rectangle alerts = AlertLayout.Place(l.MapView, new Point((int)(220 * s), (int)(70 * s)), 8, 8, new[] { new Rectangle(l.MapView.Right - 300, 8, 300, 30) });
            Assert.False(alerts.Intersects(l.Panel));
            Assert.True(alerts.Right <= l.MapView.Right);
        }
    }

    [Fact]
    public void No_sidebar_row_is_zero_height_at_the_clamped_extremes()
    {
        UiScaleResult r = UiScale.Resolve(1280, 720, 200);
        SidebarLayout l = SidebarLayout.Compute(1280, 720, r.Scale, 6, true, true, 64, 64);
        Assert.True(l.Credits.Height > 0 && l.SellButton.Height > 0 && l.Minimap.Height > 0 && l.CameoSize.Y > 0);
    }
}

public sealed class TierPlacementTests
{
    [Fact]
    public void A_heading_is_never_placed_without_the_row_under_it()
    {
        var cell = System.Array.Empty<Rts.Game.Ui.Sidebar.TierEntry>();
        var rows = new[]
        {
            new Rts.Game.Ui.Sidebar.TierRow(true, 1, false, "", cell), new Rts.Game.Ui.Sidebar.TierRow(false, 1, false, "", cell),
            new Rts.Game.Ui.Sidebar.TierRow(true, 2, false, "", cell), new Rts.Game.Ui.Sidebar.TierRow(false, 2, false, "", cell),
        };
        // heading 20, cameo 50, gap 4: heading + row = 74; two groups need 152. A grid of 120 fits the first group and the second heading only.
        var placed = Rts.Game.Ui.Sidebar.TierGrouping.Place(rows, 0, 120, 50, 20, 4);
        Assert.Equal(new[] { 0, 1 }, placed.Select(p => p.Row).ToArray());
        int last = Rts.Game.Ui.Sidebar.TierGrouping.MaxFirstRow(rows, 120, 50, 20, 4);
        Assert.Contains(3, Rts.Game.Ui.Sidebar.TierGrouping.Place(rows, last, 120, 50, 20, 4).Select(p => p.Row));
    }
}
