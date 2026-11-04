// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Microsoft.Xna.Framework;
using Rts.Game.Ui.Sidebar;

namespace Rts.Game.Tests.Ui;

public sealed class SidebarLayoutTests
{
    public static IEnumerable<object[]> Windows() => new[]
    {
        new object[] { 1280, 720, 1f },
        new object[] { 1920, 1080, 1.5f },
        new object[] { 2560, 1440, 2f },
        new object[] { 1024, 600, 1f },
    };

    [Theory]
    [MemberData(nameof(Windows))]
    public void Panel_is_about_a_fifth_of_the_width_and_flush_right(int w, int h, float ui)
    {
        SidebarLayout l = SidebarLayout.Compute(w, h, ui);
        Assert.Equal(w, l.Panel.Right);
        Assert.Equal(0, l.Panel.Y);
        Assert.Equal(h, l.Panel.Height);
        Assert.InRange(l.Panel.Width, w / 6, w / 4 + 1);
        Assert.Equal(new Rectangle(0, 0, w - l.Panel.Width, h), l.MapView);
    }

    [Theory]
    [MemberData(nameof(Windows))]
    public void Every_part_is_inside_the_panel_and_in_order_top_to_bottom(int w, int h, float ui)
    {
        SidebarLayout l = SidebarLayout.Compute(w, h, ui);
        foreach (Rectangle r in new[] { l.Credits, l.PowerBar, l.SellButton, l.RepairButton, l.Grid, l.Minimap }.Concat(l.Tabs))
            Assert.True(l.Panel.Contains(r), $"{r} outside {l.Panel}");

        Assert.True(l.Credits.Bottom <= l.SellButton.Y);
        Assert.True(l.SellButton.Bottom <= l.Tabs[0].Y);
        Assert.True(l.Tabs[0].Bottom <= l.Grid.Y);
        Assert.True(l.Grid.Bottom <= l.Minimap.Y);
        Assert.True(l.PowerBar.Bottom <= l.Minimap.Y);
        Assert.True(l.PowerBar.Right <= l.SellButton.X);
        Assert.True(l.SellButton.Right <= l.RepairButton.X);
        Assert.True(h - l.Minimap.Bottom < 12 * ui);
    }

    [Theory]
    [MemberData(nameof(Windows))]
    public void Minimap_is_square_and_tabs_tile_the_content_width(int w, int h, float ui)
    {
        SidebarLayout l = SidebarLayout.Compute(w, h, ui);
        Assert.Equal(l.Minimap.Width, l.Minimap.Height);
        Assert.True(l.Minimap.Height <= h * 0.4f + 1);
        Assert.Equal(5, l.Tabs.Count);   // M3-08: the Upgrades tab is the fifth
        for (int i = 1; i < 5; i++) Assert.Equal(l.Tabs[i - 1].Right, l.Tabs[i].X);
        Assert.Equal(l.SellButton.X, l.Tabs[0].X);
        Assert.Equal(l.RepairButton.Right, l.Tabs[4].Right);
    }

    [Theory]
    [MemberData(nameof(Windows))]
    public void Cameo_slots_fit_the_grid_without_overlapping(int w, int h, float ui)
    {
        SidebarLayout l = SidebarLayout.Compute(w, h, ui);
        Assert.InRange(l.Columns, 1, 3);
        Assert.True(l.Rows >= 2, $"only {l.Rows} rows");
        var slots = Enumerable.Range(0, l.VisibleSlots).Select(l.Cameo).ToList();
        foreach (Rectangle r in slots) Assert.True(l.Grid.Contains(r));
        for (int i = 0; i < slots.Count; i++)
            for (int j = i + 1; j < slots.Count; j++)
                Assert.False(slots[i].Intersects(slots[j]));
        Assert.Equal(Rectangle.Empty, l.Cameo(-1));
        Assert.Equal(Rectangle.Empty, l.Cameo(l.VisibleSlots));
        Assert.Equal(l.CameoSize.X * 3 / 4, l.CameoSize.Y);
    }

    [Fact]
    public void Hit_tests_find_slots_and_tabs()
    {
        SidebarLayout l = SidebarLayout.Compute(1280, 720, 1f);
        Assert.Equal(0, l.SlotAt(l.Cameo(0).Center));
        Assert.Equal(l.VisibleSlots - 1, l.SlotAt(l.Cameo(l.VisibleSlots - 1).Center));
        Assert.Equal(-1, l.SlotAt(new Point(0, 0)));
        Assert.Equal(-1, l.SlotAt(new Point(l.Cameo(0).Right + 1, l.Cameo(0).Y + 1)));   // in the gap
        for (int i = 0; i < 5; i++) Assert.Equal(i, l.TabAt(l.Tabs[i].Center));
        Assert.Equal(-1, l.TabAt(l.Grid.Center));
    }

    [Fact]
    public void Larger_scale_gives_a_wider_panel_and_bigger_buttons_in_the_same_window()
    {
        SidebarLayout a = SidebarLayout.Compute(1920, 1080, 1f);
        SidebarLayout b = SidebarLayout.Compute(1920, 1080, 1.5f);
        SidebarLayout c = SidebarLayout.Compute(1920, 1080, 2f);
        Assert.True(a.Panel.Width <= b.Panel.Width && b.Panel.Width <= c.Panel.Width);
        Assert.True(a.Credits.Height < b.Credits.Height && b.Credits.Height < c.Credits.Height);
        Assert.True(a.SellButton.Height < c.SellButton.Height);
        Assert.Equal(SidebarLayout.WidthFor(1920, 1f), a.Panel.Width);
    }

    [Fact]
    public void Tiny_windows_do_not_throw_and_never_give_the_sidebar_more_than_half()
    {
        SidebarLayout l = SidebarLayout.Compute(320, 200, 2f);
        Assert.True(l.Panel.Width <= 160);
        Assert.True(l.VisibleSlots >= 0);
        Assert.True(l.Minimap.Width >= 0);
    }
}
