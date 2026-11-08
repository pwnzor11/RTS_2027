// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Microsoft.Xna.Framework;
using Rts.Game.Render;
using Xunit;

namespace Rts.Game.Tests.Ui2;

public sealed class OrderFeedbackTests
{
    [Fact]
    public void Ring_grows_and_fades()
    {
        Assert.Equal(7f, OrderFeedback.Radius(0f, 28f), 3);
        Assert.Equal(28f, OrderFeedback.Radius(OrderFeedback.DurationSeconds, 28f), 3);
        Assert.True(OrderFeedback.Radius(0.1f, 28f) > OrderFeedback.Radius(0.05f, 28f));
        Assert.Equal(1f, OrderFeedback.Alpha(0f));
        Assert.Equal(0f, OrderFeedback.Alpha(5f));
    }

    [Fact]
    public void Marks_expire_after_the_duration()
    {
        var f = new OrderFeedback();
        f.Add(Vector2.Zero, OrderMarkKind.Move);
        f.Add(Vector2.One, OrderMarkKind.Attack);
        f.Advance(0.3f);
        Assert.Equal(2, f.Count);
        f.Advance(0.2f);
        Assert.Equal(0, f.Count);
    }
}
