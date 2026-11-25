// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Game.Render;

namespace Rts.Game.Tests.S45Fx;

/// <summary>S45 V-04: debris arcs, fade, counts, caps and determinism.</summary>
public sealed class DebrisTests
{
    private static DebrisChunk Chunk(int born = 0, float vz = 6f) => new(100f, 200f, 1f, 0.5f, vz, born, 3, 0x808080, false);

    private static List<DebrisChunk> Plan(float x, float y, int side, bool building, int faction, int tick)
    {
        var list = new List<DebrisChunk>();
        DebrisPlan.For(x, y, side, building, faction, tick, list);
        return list;
    }

    [Fact]
    public void Chunk_rises_peaks_and_lands()
    {
        DebrisChunk c = Chunk(vz: 6f);
        int flight = DebrisPhysics.FlightTicks(c);
        DebrisPhysics.Pose(c, 0, out _, out _, out float z0);
        DebrisPhysics.Pose(c, flight / 2, out _, out _, out float zMid);
        DebrisPhysics.Pose(c, flight, out _, out _, out float zEnd);
        Assert.Equal(0f, z0);
        Assert.True(zMid > 5f);
        Assert.True(zEnd <= 0.5f);
    }

    [Fact]
    public void Chunk_stops_sliding_once_landed()
    {
        DebrisChunk c = Chunk();
        int flight = DebrisPhysics.FlightTicks(c);
        DebrisPhysics.Pose(c, flight, out float x1, out float y1, out _);
        DebrisPhysics.Pose(c, flight + 25, out float x2, out float y2, out float z2);
        Assert.Equal(x1, x2);
        Assert.Equal(y1, y2);
        Assert.Equal(0f, z2, 1);
    }

    [Fact]
    public void Alpha_is_full_until_resting_ends_then_fades_to_nothing()
    {
        DebrisChunk c = Chunk();
        int landed = DebrisPhysics.FlightTicks(c) + DebrisPhysics.RestTicks;
        Assert.Equal(0, DebrisPhysics.Alpha(c, -1));
        Assert.Equal(255, DebrisPhysics.Alpha(c, 0));
        Assert.Equal(255, DebrisPhysics.Alpha(c, landed - 1));
        Assert.InRange(DebrisPhysics.Alpha(c, landed + DebrisPhysics.FadeTicks / 2), 1, 254);
        Assert.Equal(0, DebrisPhysics.Alpha(c, DebrisPhysics.LifeTicks(c)));
    }

    [Fact]
    public void Field_caps_globally_and_drops_the_oldest()
    {
        var field = new DebrisField();
        for (int i = 0; i < DebrisField.MaxChunks + 40; i++) field.Add(Chunk(born: i));
        Assert.Equal(DebrisField.MaxChunks, field.Count);
        Assert.Equal(40, field.Items[0].Born);
    }

    [Fact]
    public void Field_prunes_finished_and_future_chunks()
    {
        var field = new DebrisField();
        DebrisChunk c = Chunk(born: 10);
        field.Add(c);
        field.Add(Chunk(born: 5000));
        field.Prune(10 + DebrisPhysics.LifeTicks(c));
        Assert.Equal(0, field.Count);   // the first is over, the second is from the future
    }

    [Fact]
    public void Counts_are_a_few_and_capped_per_explosion()
    {
        Assert.Equal(DebrisPlan.VehicleChunks, Plan(0, 0, 1, false, 0, 1).Count);
        Assert.InRange(Plan(0, 0, 2, true, 0, 1).Count, DebrisPlan.VehicleChunks, DebrisPlan.MaxPerExplosion);
        Assert.Equal(DebrisPlan.MaxPerExplosion, Plan(0, 0, 9, true, 0, 1).Count);
        Assert.True(DebrisPlan.MaxPerExplosion <= 8);
    }

    [Fact]
    public void Same_death_gives_the_same_chunks_and_another_tick_differs()
    {
        List<DebrisChunk> a = Plan(320f, 640f, 3, true, 1, 77), b = Plan(320f, 640f, 3, true, 1, 77), c = Plan(320f, 640f, 3, true, 1, 78);
        Assert.Equal(a, b);
        Assert.NotEqual(a, c);
    }

    [Fact]
    public void Chunks_are_small_tinted_and_in_the_air_for_a_short_time()
    {
        foreach (int faction in new[] { 0, 1, 3, 4 })
        {
            foreach (DebrisChunk c in Plan(50f, 50f, 2, true, faction, 9))
            {
                Assert.InRange(c.Size, 2, 5);
                Assert.InRange(DebrisPhysics.FlightTicks(c), 8, 30);
                Assert.True(DebrisPhysics.LifeTicks(c) < 100);   // gone within 5 s
                Assert.Equal(0u, c.Rgb >> 24);
            }
        }
    }

    [Fact]
    public void Palette_follows_the_faction()
    {
        uint Sum(int faction)
        {
            uint s = 0;
            foreach (DebrisChunk c in Plan(50f, 50f, 4, true, faction, 3)) s += c.Rgb;
            return s;
        }

        Assert.NotEqual(Sum(0), Sum(4));
    }

    [Fact]
    public void Debris_toggle_defaults_on_and_round_trips()
    {
        Assert.True(Settings.Default.DebrisChunks);
        Assert.False(Settings.Parse((Settings.Default with { DebrisChunks = false }).ToToml()).DebrisChunks);
        Assert.True(Settings.Parse("version = 2\n").DebrisChunks);
    }
}
