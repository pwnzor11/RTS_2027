// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Game.Ui.Sidebar;
using Xunit;

namespace Rts.Game.Tests.Ui2;

public sealed class MinimapFitTests
{
    [Fact]
    public void Unknown_map_gives_a_square() => Assert.Equal((100, 100), SidebarLayout.MinimapSize(100, 100, 0, 0));

    [Fact]
    public void Wide_map_is_full_width_and_shorter() => Assert.Equal((120, 60), SidebarLayout.MinimapSize(120, 120, 64, 32));

    [Fact]
    public void Tall_map_is_full_height_and_narrower() => Assert.Equal((60, 120), SidebarLayout.MinimapSize(120, 120, 32, 64));

    [Fact]
    public void Layout_minimap_keeps_the_map_aspect_and_the_grid_grows()
    {
        SidebarLayout square = SidebarLayout.Compute(1600, 900, 1f);
        SidebarLayout wide = SidebarLayout.Compute(1600, 900, 1f, mapWidth: 64, mapHeight: 32);
        Assert.Equal(wide.Minimap.Width / 2, wide.Minimap.Height);
        Assert.True(wide.Grid.Height > square.Grid.Height);
        Assert.True(wide.Grid.Bottom <= wide.Minimap.Y);
    }
}
