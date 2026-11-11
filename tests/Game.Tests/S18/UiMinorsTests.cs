// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Game.Controls;
using Rts.Game.Input;
using Rts.Game.Sandbox;
using Rts.Sim.Core;

namespace Rts.Game.Tests.S18;

/// <summary>S18 GAMEUI-07 (Ctrl+n with nothing selected keeps the group) and GAMEUI-11 (held Backspace repeats).</summary>
public sealed class UiMinorsTests
{
    private static UnitKey Key(int i) => new(ActorKind.Vehicle, new EntityId((ushort)i, 1));

    [Fact]
    public void Setting_a_group_from_an_empty_selection_keeps_the_old_members()
    {
        var groups = new ControlGroups();
        Assert.True(groups.TrySet(1, new[] { Key(1), Key(2) }));
        Assert.False(groups.TrySet(1, Array.Empty<UnitKey>()));
        Assert.Equal(2, groups.Members(1).Count);
        Assert.True(groups.TrySet(1, new[] { Key(3) }));
        Assert.Equal(new[] { Key(3) }, groups.Members(1).ToArray());
        Assert.False(groups.TrySet(0, new[] { Key(4) }));
    }

    [Fact]
    public void A_held_key_fires_once_then_waits_then_repeats()
    {
        var repeat = new KeyRepeat();
        var fired = new List<int>();
        for (int frame = 1; frame <= 40; frame++)
            if (repeat.Step(true)) fired.Add(frame);
        Assert.Equal(1, fired[0]);
        Assert.DoesNotContain(fired, f => f > 1 && f <= KeyRepeat.DelayFrames);
        Assert.Equal(KeyRepeat.DelayFrames + 1, fired[1]);
        Assert.Equal(KeyRepeat.IntervalFrames, fired[2] - fired[1]);
        Assert.True(fired.Count > 5);
    }

    [Fact]
    public void Releasing_the_key_starts_the_cycle_again()
    {
        var repeat = new KeyRepeat();
        Assert.True(repeat.Step(true));
        Assert.False(repeat.Step(true));
        Assert.False(repeat.Step(false));
        Assert.True(repeat.Step(true));
    }
}
