// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Game.Ui;
using Rts.Game.Ui.Sidebar;
using Rts.Sim.Core;
using Rts.Sim.Rules;

namespace Rts.Game.Tests.S19;

/// <summary>M13-15 item 4 (D-205): repeat production on a cameo re-queues when the item has finished.</summary>
public sealed class RepeatProductionTests
{
    private const ProductionCategory Inf = ProductionCategory.Infantry;

    private static PlayerHud Hud(QueueBlockReason blocked = QueueBlockReason.None, bool offered = true, params int[] queuedTypes)
    {
        var options = offered ? new[] { new BuildOption(Inf, 3, true, blocked, 100) } : Array.Empty<BuildOption>();
        var items = queuedTypes.Select(t => new QueueItemView(t, 100, false)).ToList();
        return new PlayerHud(500, 0, 5000, 100, 0, new[] { new QueueView(Inf, items, -1) }, Array.Empty<(ProductionCategory, int)>(), false, options);
    }

    [Fact]
    public void Only_unit_lanes_can_loop()
    {
        var r = new RepeatProduction();
        Assert.True(r.Toggle(Inf, 3));
        Assert.False(r.Toggle(ProductionCategory.Structure, 1));
        Assert.False(r.Toggle(ProductionCategory.Upgrade, 0));
        Assert.True(r.IsOn(Inf, 3));
        Assert.False(r.Toggle(Inf, 3));   // a second Alt+click turns it off
        Assert.False(r.IsOn(Inf, 3));
    }

    [Fact]
    public void A_loop_queues_one_when_none_is_left_and_not_again_until_that_one_is_gone()
    {
        var r = new RepeatProduction();
        r.Toggle(Inf, 3);
        Command first = Assert.Single(r.Due(0, Hud(), tick: 100));
        Assert.Equal((CommandKind.Build, (int)Inf, 3), (first.Kind, first.Arg, first.Arg2));
        Assert.Empty(r.Due(0, Hud(), tick: 101));                            // the Build has not reached the lane yet
        Assert.Empty(r.Due(0, Hud(queuedTypes: 3), tick: 200));              // one is building: nothing to add
        Assert.Single(r.Due(0, Hud(), tick: 300));                           // it finished: the loop queues the next
    }

    [Fact]
    public void A_blocked_cameo_waits_and_a_cameo_that_is_no_longer_offered_ends_the_loop()
    {
        var r = new RepeatProduction();
        r.Toggle(Inf, 3);
        Assert.Empty(r.Due(0, Hud(QueueBlockReason.QueueFull), tick: 100));
        Assert.True(r.IsOn(Inf, 3));                                          // still looping, just waiting
        Assert.Single(r.Due(0, Hud(), tick: 101));
        Assert.Empty(r.Due(0, Hud(offered: false), tick: 300));
        Assert.False(r.IsOn(Inf, 3));
    }

    [Fact]
    public void Stop_ends_a_loop_and_clear_ends_all()
    {
        var r = new RepeatProduction();
        r.Toggle(Inf, 3);
        r.Toggle(ProductionCategory.Vehicle, 1);
        r.Stop(Inf, 3);
        Assert.False(r.IsOn(Inf, 3));
        r.Clear();
        Assert.Empty(r.Active);
    }

    [Fact]
    public void The_tooltip_and_marker_texts_exist_and_the_loop_is_front_end_only()
    {
        Assert.Equal("Alt+click: repeat production", UiText.Format(UiText.RepeatHint, "key", "Alt"));
        Assert.Equal("LOOP", UiText.Get(UiText.RepeatMarker));
        // The loop is a stream of ordinary Build commands: no new CommandKind, so saves and replays are unchanged.
        Assert.Equal(CommandKind.Build, new RepeatProduction().ToggleAndDue().Kind);
    }
}

internal static class RepeatTestHelpers
{
    public static Command ToggleAndDue(this RepeatProduction r)
    {
        r.Toggle(ProductionCategory.Infantry, 3);
        var options = new[] { new BuildOption(ProductionCategory.Infantry, 3, true, QueueBlockReason.None, 100) };
        var hud = new PlayerHud(0, 0, 0, 0, 0, Array.Empty<QueueView>(), Array.Empty<(ProductionCategory, int)>(), false, options);
        return r.Due(0, hud, 1000)[0];
    }
}
