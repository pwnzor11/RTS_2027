// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Microsoft.Xna.Framework;
using Rts.Game.Ui;
using Rts.Game.Ui.Sidebar;
using Rts.Sim.Core;

namespace Rts.Game.Tests.Ui;

/// <summary>SUP-4: the sidebar supply counter's visibility, colour bands, text and layout row.</summary>
public sealed class SupplyCounterTests
{
    [Theory]
    [InlineData(0, 60, SupplyTone.Normal)]
    [InlineData(55, 60, SupplyTone.Normal)]
    [InlineData(56, 60, SupplyTone.Amber)]
    [InlineData(59, 60, SupplyTone.Amber)]
    [InlineData(60, 60, SupplyTone.Red)]
    [InlineData(61, 60, SupplyTone.Red)]
    public void ToneChangesAtCapMinusFourAndAtCap(int used, int cap, SupplyTone tone) =>
        Assert.Equal(tone, SupplyReadout.ToneOf(used, cap));

    [Fact]
    public void AmberMarginIsFour()
    {
        Assert.Equal(4, SupplyReadout.AmberMargin);
        Assert.Equal(SupplyTone.Normal, SupplyReadout.ToneOf(195, 200));
        Assert.Equal(SupplyTone.Amber, SupplyReadout.ToneOf(196, 200));
    }

    [Fact]
    public void HiddenWhenRulesAreOffOrNoCap()
    {
        Assert.False(SupplyReadout.Shown(false, new SupplyView(10, 60, 200)));
        Assert.False(SupplyReadout.Shown(true, new SupplyView(10, 0, 200)));
        Assert.True(SupplyReadout.Shown(true, new SupplyView(10, 60, 200)));
    }

    [Fact]
    public void TextIsUsedSlashCap()
    {
        Assert.Equal("34/60", SupplyReadout.Text(new SupplyView(34, 60, 200)));
        Assert.Equal("0/10", SupplyReadout.Text(new SupplyView(0, 10, 200)));
    }

    [Fact]
    public void LayoutReservesARowUnderTheCreditsOnlyWhenAsked()
    {
        var without = SidebarLayout.Compute(1280, 720, 1f);
        Assert.Equal(Rectangle.Empty, without.SupplyRow);

        var with = SidebarLayout.Compute(1280, 720, 1f, supplyRow: true);
        Assert.True(with.SupplyRow.Height > 0);
        Assert.Equal(with.Credits.Bottom + with.Gap, with.SupplyRow.Y);
        Assert.True(with.PowerBar.Y >= with.SupplyRow.Bottom);
        Assert.True(with.SellButton.Y >= with.SupplyRow.Bottom);
        Assert.True(with.PowerBar.Y > without.PowerBar.Y);
    }

    [Theory]
    [InlineData(true, 50, 60, 11, true)]
    [InlineData(true, 50, 60, 10, false)]
    [InlineData(true, 60, 60, 1, true)]
    [InlineData(true, 50, 60, 0, false)]
    [InlineData(false, 50, 60, 11, false)]
    public void HeadWaitsOnlyWhenItDoesNotFit(bool enabled, int used, int cap, int head, bool waits) =>
        Assert.Equal(waits, SupplyReadout.HeadWaitsForSupply(enabled, used, cap, head));
}
