// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Game.Render;
using Rts.Game.Tests.Audio;
using Rts.Sim.Core;

namespace Rts.Game.Tests.S18;

/// <summary>S18 GAMEREND-01: the renderer's memory must forget the previous game, or every starting building replays its build animation.</summary>
public sealed class RenderMemoryRestartTests
{
    [Fact]
    public void A_second_game_starting_at_tick_zero_does_not_replay_the_build_animation()
    {
        var memory = new RenderMemory();
        var first = AudioTestSupport.Building(1, 0, 4, 4);
        memory.Observe(AudioTestSupport.Snap(500, buildings: new[] { first }));
        memory.Observe(AudioTestSupport.Snap(520, buildings: new[] { first }));

        // Game 2 on the same renderer: a different building id, first snapshot at tick 0.
        var second = AudioTestSupport.Building(7, 0, 4, 4);
        memory.Observe(AudioTestSupport.Snap(0, buildings: new[] { second }));
        Assert.True(memory.BuildingAge(second.Id, 0) > 1_000_000, "a starting building must be settled, not freshly built");
    }

    [Fact]
    public void A_building_that_appears_later_in_the_same_game_still_animates()
    {
        var memory = new RenderMemory();
        memory.Observe(AudioTestSupport.Snap(100, buildings: new[] { AudioTestSupport.Building(1, 0, 4, 4) }));
        var added = AudioTestSupport.Building(2, 0, 9, 9);
        memory.Observe(AudioTestSupport.Snap(140, buildings: new[] { AudioTestSupport.Building(1, 0, 4, 4), added }));
        Assert.Equal(10, memory.BuildingAge(added.Id, 150));
    }
}
