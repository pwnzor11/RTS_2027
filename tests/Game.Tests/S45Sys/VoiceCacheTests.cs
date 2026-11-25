// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Game.Audio;

namespace Rts.Game.Tests.S45Sys;

/// <summary>V-06 (GAMESYS-06): decoded voice clips are cached (bounded, least recently used) and at most two voice lines play at once.</summary>
public sealed class VoiceCacheTests
{
    [Fact]
    public void A_cached_clip_is_created_once_and_a_failed_clip_is_not_retried()
    {
        var cache = new BoundedCache<string, string?>(4);
        int creates = 0;

        Assert.Equal("clip", cache.GetOrAdd("m1.ogg", _ => { creates++; return "clip"; }));
        Assert.Equal("clip", cache.GetOrAdd("m1.ogg", _ => { creates++; return "clip"; }));
        Assert.Null(cache.GetOrAdd("bad.ogg", _ => { creates++; return null; }));
        Assert.Null(cache.GetOrAdd("bad.ogg", _ => { creates++; return "never"; }));

        Assert.Equal(2, creates);
        Assert.Equal(2, cache.Count);
    }

    [Fact]
    public void The_least_recently_used_clip_is_dropped_when_the_cache_is_full()
    {
        var cache = new BoundedCache<string, string?>(2);
        cache.GetOrAdd("a", k => k);
        cache.GetOrAdd("b", k => k);
        cache.GetOrAdd("a", k => k);   // a is used again, so b is now the oldest
        cache.GetOrAdd("c", k => k);

        Assert.True(cache.Contains("a"));
        Assert.False(cache.Contains("b"));
        Assert.True(cache.Contains("c"));
        Assert.Equal(2, cache.Count);
    }

    [Fact]
    public void The_voice_cache_holds_32_clips()
    {
        Assert.Equal(32, MonoGameAudio.VoiceCacheSize);
        Assert.Equal(32, new BoundedCache<string, int>(MonoGameAudio.VoiceCacheSize).Capacity);
    }

    [Fact]
    public void A_cache_needs_room_for_at_least_one_entry()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new BoundedCache<string, int>(0));
    }

    [Fact]
    public void Two_voice_lines_play_and_a_third_is_dropped_until_one_ends()
    {
        var slots = new VoiceSlots();   // two by default
        Assert.Equal(2, VoiceSlots.DefaultMax);

        Assert.True(slots.TryTake(0, 1000));
        Assert.True(slots.TryTake(100, 500));
        Assert.False(slots.TryTake(200, 1000));   // both still playing: this line is dropped
        Assert.Equal(2, slots.Active(200));

        Assert.True(slots.TryTake(600, 1000));    // the 100 ms line ended at 600
        Assert.Equal(2, slots.Active(700));
    }

    [Fact]
    public void A_dropped_line_does_not_use_up_a_slot()
    {
        var slots = new VoiceSlots(1);
        Assert.True(slots.TryTake(0, 100));
        Assert.False(slots.TryTake(50, 100));
        Assert.True(slots.TryTake(100, 100));
    }
}
