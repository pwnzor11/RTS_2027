// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Microsoft.Xna.Framework;
using Rts.Game.Ui.Skin;

namespace Rts.Game.Tests.Ui.Skin;

public sealed class NineSliceTests
{
    [Fact]
    public void CornersKeepSizeAndPatchesTileTheTarget()
    {
        var dest = new Rectangle(10, 20, 100, 60);
        var plan = NineSlice.Plan(16, 16, new SkinSlice(4, 3, 5, 2), dest);
        Assert.Equal(9, plan.Count);
        Assert.Equal(new Rectangle(10, 20, 4, 3), plan[0].Dst);      // top-left unscaled
        Assert.Equal(new Rectangle(105, 78, 5, 2), plan[8].Dst);     // bottom-right unscaled
        Assert.Equal(new Rectangle(14, 23, 91, 55), plan[4].Dst);    // centre stretches
        Assert.Equal(dest.Width * dest.Height, plan.Sum(p => p.Dst.Width * p.Dst.Height));
        Assert.Equal(16 * 16, plan.Sum(p => p.Src.Width * p.Src.Height));
    }

    [Fact]
    public void SmallTargetShrinksMarginsWithoutOverlap()
    {
        var dest = new Rectangle(0, 0, 6, 6);
        var plan = NineSlice.Plan(16, 16, new SkinSlice(4, 4, 4, 4), dest);
        Assert.Equal(36, plan.Sum(p => p.Dst.Width * p.Dst.Height));
    }

    [Fact]
    public void ZeroMarginsAndEmptyTarget()
    {
        Assert.Single(NineSlice.Plan(8, 8, new SkinSlice(0, 0, 0, 0), new Rectangle(0, 0, 20, 20)));
        Assert.Empty(NineSlice.Plan(8, 8, new SkinSlice(2, 2, 2, 2), new Rectangle(0, 0, 0, 20)));
    }
}
