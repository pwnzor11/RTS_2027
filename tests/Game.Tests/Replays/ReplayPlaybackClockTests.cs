// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Game.Host;
using Rts.Game.Menu;
using Rts.Sim.Core;

namespace Rts.Game.Tests.Replays;

public sealed class ReplayPlaybackClockTests
{
    private const long Tick = SimHost.TickMicroseconds;

    [Fact]
    public void Runs_one_tick_per_interval_at_1x_and_keeps_the_remainder()
    {
        var clock = new ReplayPlaybackClock(1000);
        Assert.Equal(0, clock.Advance(Tick - 1));
        Assert.Equal(1, clock.Advance(1));
        Assert.Equal(3, clock.Advance(3 * Tick));
        Assert.Equal(4, clock.Tick);
    }

    [Theory]
    [InlineData(1, 1)]
    [InlineData(2, 2)]
    [InlineData(3, 2)]   // between steps: the nearest lower one
    [InlineData(4, 4)]
    [InlineData(8, 8)]
    [InlineData(100, 8)]
    [InlineData(0, 1)]
    public void SetSpeed_picks_a_known_speed(int asked, int expected)
    {
        var clock = new ReplayPlaybackClock(1000);
        clock.SetSpeed(asked);
        Assert.Equal(expected, clock.Speed);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(4)]
    [InlineData(8)]
    public void Each_interval_runs_Speed_ticks(int speed)
    {
        var clock = new ReplayPlaybackClock(10_000);
        clock.SetSpeed(speed);
        Assert.Equal(speed * 5, clock.Advance(5 * Tick));
    }

    [Fact]
    public void ChangeSpeed_steps_and_stops_at_the_ends()
    {
        var clock = new ReplayPlaybackClock(1000);
        for (int i = 0; i < 6; i++) clock.ChangeSpeed(1);
        Assert.Equal(8, clock.Speed);
        for (int i = 0; i < 6; i++) clock.ChangeSpeed(-1);
        Assert.Equal(1, clock.Speed);
    }

    [Fact]
    public void Paused_clock_runs_nothing_and_drops_time_but_still_single_steps()
    {
        var clock = new ReplayPlaybackClock(1000);
        clock.Advance(Tick / 2);
        clock.TogglePause();
        Assert.Equal(0, clock.Advance(10 * Tick));
        Assert.Equal(1, clock.StepOne());
        Assert.Equal(1, clock.Tick);
        Assert.Equal(1f, clock.Alpha);
        clock.TogglePause();
        Assert.Equal(0, clock.Advance(Tick / 2));   // the time from before and during the pause is gone
    }

    [Fact]
    public void A_stall_is_capped_like_the_game_clock()
    {
        var clock = new ReplayPlaybackClock(100_000);
        Assert.Equal((int)(SimHost.MaxAccumulatedMicroseconds / Tick), clock.Advance(60_000_000));
    }

    [Fact]
    public void Never_runs_past_the_end_and_reports_it()
    {
        var clock = new ReplayPlaybackClock(5);
        clock.SetSpeed(8);
        Assert.Equal(5, clock.Advance(10 * Tick));
        Assert.True(clock.Ended);
        Assert.Equal(1f, clock.Progress);
        Assert.Equal(0, clock.Advance(10 * Tick));
        Assert.Equal(0, clock.StepOne());
        Assert.Equal(0, clock.ScrubForward());
        Assert.Equal(5, clock.Tick);
    }

    [Fact]
    public void Scrub_jumps_ten_seconds_and_stops_at_the_end()
    {
        var clock = new ReplayPlaybackClock(500);
        Assert.Equal(ReplayPlaybackClock.ScrubSeconds * SimConstants.TicksPerSecond, clock.ScrubForward());
        Assert.Equal(200, clock.Tick);
        Assert.Equal(200, clock.ScrubForward());
        Assert.Equal(100, clock.ScrubForward());   // only 100 ticks were left
        Assert.True(clock.Ended);
    }

    [Fact]
    public void SeekForward_never_goes_backwards_or_past_the_end()
    {
        var clock = new ReplayPlaybackClock(300);
        Assert.Equal(100, clock.SeekForward(100));
        Assert.Equal(0, clock.SeekForward(50));
        Assert.Equal(100, clock.Tick);
        Assert.Equal(200, clock.SeekForward(9999));
        Assert.Equal(300, clock.Tick);
    }

    [Fact]
    public void A_replay_of_zero_ticks_starts_ended()
    {
        var clock = new ReplayPlaybackClock(0);
        Assert.True(clock.Ended);
        Assert.Equal(1f, clock.Progress);
        Assert.Equal(0, clock.Advance(5 * Tick));
    }
}
