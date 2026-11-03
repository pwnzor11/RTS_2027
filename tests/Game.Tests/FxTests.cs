// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Game.Render;
using Rts.Game.Sprites;

namespace Rts.Game.Tests;

/// <summary>Building [[fx]] anchors in the manifest, and the frame choice of effect sprites. No GPU needed.</summary>
public sealed class FxTests
{
    private const string Building = """
        file = "b.png"
        frame_w = 192
        frame_h = 224
        origin_x = 0
        origin_y = 32

        [[state]]
        name = "idle"
        facings = 1
        frames = 4

        [[state]]
        name = "damaged"
        facings = 1
        frames = 4
        first = 4
        """;

    // Raw string literals keep the file's line endings (CRLF on a Windows checkout); tests search for "\n", so normalise.
    private static string Lf(string s) => s.Replace("\r\n", "\n", StringComparison.Ordinal);
    private static SpriteManifest Parse(string text, string id = "b") => SpriteManifest.Parse(Lf(text), id, "test.toml");

    private static SpriteManifest Effect(int frames, int tpf, bool loop) => Parse($"""
        file = "e.png"
        frame_w = 16
        frame_h = 16
        origin_x = 8
        origin_y = 8

        [[state]]
        name = "play"
        facings = 1
        frames = {frames}
        ticks_per_frame = {tpf}
        loop = {(loop ? "true" : "false")}
        """, "e");

    [Fact]
    public void Fx_tables_parse_with_a_default_phase()
    {
        SpriteManifest m = Parse(Building + """

            [[fx]]
            state = "damaged"
            kind = "fire_large"
            x = 120
            y = 60
            phase = 3

            [[fx]]
            state = "damaged"
            kind = "smoke_plume"
            x = 100
            y = 50
            """);

        Assert.Equal(2, m.Fx.Count);
        Assert.Equal(new FxAnchor("damaged", "fire_large", 120, 60, 3), m.Fx[0]);
        Assert.Equal(0, m.Fx[1].Phase);
        Assert.Equal(2, m.FxFor("damaged").Count());
        Assert.Empty(m.FxFor("idle"));
    }

    [Fact]
    public void A_manifest_without_fx_has_none()
    {
        Assert.Empty(Parse(Building).Fx);
    }

    [Theory]
    [InlineData("state = \"nope\"\nkind = \"fire_small\"\nx = 1\ny = 1", "unknown state")]
    [InlineData("state = \"damaged\"\nx = 1\ny = 1", "missing 'kind'")]
    [InlineData("state = \"damaged\"\nkind = \"fire_small\"\ny = 1", "missing 'x'")]
    [InlineData("state = \"damaged\"\nkind = \"fire_small\"\nx = 1\ny = 1\nspeed = 2", "unknown key 'speed'")]
    [InlineData("state = \"damaged\"\nkind = \"fire_small\"\nx = 500\ny = 1", "inside the 192x224 frame")]
    [InlineData("state = \"damaged\"\nkind = \"fire_small\"\nx = 1\ny = 1\nphase = -1", "'phase'")]
    public void Bad_fx_tables_are_errors(string body, string expected)
    {
        var ex = Assert.Throws<InvalidDataException>(() => Parse(Building + "\n[[fx]]\n" + body + "\n"));
        Assert.Contains(expected, ex.Message);
    }

    [Fact]
    public void Anchored_frames_follow_tick_and_phase()
    {
        SpriteManifest fx = Effect(8, 3, loop: true);

        Assert.Equal(0, FxFrames.Anchored(fx, 0, 0));
        Assert.Equal(0, FxFrames.Anchored(fx, 2, 0));
        Assert.Equal(1, FxFrames.Anchored(fx, 3, 0));
        Assert.Equal(3, FxFrames.Anchored(fx, 0, 3));
        Assert.Equal(4, FxFrames.Anchored(fx, 3, 3));
        Assert.Equal(0, FxFrames.Anchored(fx, 24, 0));      // wraps after 8 frames * 3 ticks
        Assert.Equal(1, FxFrames.Anchored(fx, 24, 1));
    }

    [Fact]
    public void Two_phases_do_not_flicker_in_sync()
    {
        SpriteManifest fx = Effect(8, 2, loop: true);
        for (int tick = 0; tick < 40; tick++)
        {
            Assert.NotEqual(FxFrames.Anchored(fx, tick, 0), FxFrames.Anchored(fx, tick, 3));
        }
    }

    [Fact]
    public void A_looping_effect_never_finishes_and_a_one_shot_does()
    {
        SpriteManifest loop = Effect(4, 2, loop: true);
        SpriteManifest once = Effect(4, 2, loop: false);

        Assert.Equal(0, FxFrames.Spawned(loop, 10, 10 + 8));         // wrapped
        Assert.Equal(1, FxFrames.Spawned(loop, 10, 10 + 10));

        Assert.Equal(0, FxFrames.Spawned(once, 10, 10));
        Assert.Equal(3, FxFrames.Spawned(once, 10, 10 + 7));         // last frame
        Assert.Equal(-1, FxFrames.Spawned(once, 10, 10 + 8));        // done: caller drops it
        Assert.Equal(-1, FxFrames.Spawned(once, 10, 9));             // not started yet
    }

    [Fact]
    public void A_one_shot_anchor_holds_its_last_frame()
    {
        SpriteManifest once = Effect(4, 2, loop: false);

        Assert.Equal(3, FxFrames.Anchored(once, 1000, 0));
        Assert.Equal(3, FxFrames.Anchored(once, 0, 9));
    }

    [Fact]
    public void The_state_of_a_frame_picks_the_anchors()
    {
        SpriteManifest m = Parse(Building);

        Assert.Equal("idle", FxFrames.StateOf(m, 2));
        Assert.Equal("damaged", FxFrames.StateOf(m, 4));
        Assert.Null(FxFrames.StateOf(m, 99));
    }

    [Fact]
    public void Loop_must_be_a_boolean_and_one_shot_manifest_holds_the_last_frame()
    {
        Assert.Throws<InvalidDataException>(() => Parse(Lf(Building).Replace("frames = 4\n\n[[state]]", "frames = 4\nloop = 3\n\n[[state]]")));
        SpriteManifest once = Effect(4, 2, loop: false);
        Assert.False(once.State("play").Loop);
        Assert.Equal(3, once.FrameIndex("play", 0, 500));
    }

    [Fact]
    public void The_shipped_refinery_and_effect_manifests_parse()
    {
        string root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "data", "sprites"));
        if (!Directory.Exists(root)) return;
        using SpriteLibrary lib = SpriteLibrary.Scan(root);
        foreach (string id in new[] { "fire_small", "fire_large", "smoke_plume", "sparks", "explosion_small", "explosion_large" })
        {
            Assert.Contains(id, lib.Ids);
        }
        foreach (FxAnchor a in lib.Manifest("auth_refinery").Fx)
        {
            Assert.Contains(a.Kind, lib.Ids);
        }
        Assert.False(lib.Manifest("explosion_large").State("play").Loop);
        Assert.True(lib.Manifest("fire_large").State("play").Loop);
    }
}
