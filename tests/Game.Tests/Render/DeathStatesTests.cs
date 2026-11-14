// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Game.Render;
using Rts.Game.Sprites;
using Rts.Sim.Rules;

namespace Rts.Game.Tests.Render;

/// <summary>M10-14b: infantry death animation by cause, with fallback to "die".</summary>
public sealed class DeathStatesTests
{
    private static SpriteManifest Sheet(params string[] states)
    {
        var lines = new List<string> { "file = \"t.png\"", "frame_w = 32", "frame_h = 32", "origin_x = 16", "origin_y = 24" };
        int first = 0;
        foreach (string s in states)
        {
            lines.AddRange(new[] { "[[state]]", $"name = \"{s}\"", "facings = 1", "frames = 4", $"first = {first}", "ticks_per_frame = 2", "loop = false" });
            first += 4;
        }
        return SpriteManifest.Parse(string.Join("\n", lines), "inf", "p");
    }

    [Theory]
    [InlineData(DeathCause.Blast, "die_blast")]
    [InlineData(DeathCause.Burn, "die_burn")]
    [InlineData(DeathCause.Rad, "die_rad")]
    [InlineData(DeathCause.Unknown, "die")]
    public void Sheet_with_every_state_plays_the_matching_one(DeathCause cause, string expected) =>
        Assert.Equal(expected, DeathStates.Pick(Sheet("idle", "die", "die_blast", "die_burn", "die_rad"), cause));

    [Theory]
    [InlineData(DeathCause.Blast)]
    [InlineData(DeathCause.Burn)]
    [InlineData(DeathCause.Rad)]
    public void Sheet_without_the_cause_states_falls_back_to_die(DeathCause cause) =>
        Assert.Equal("die", DeathStates.Pick(Sheet("idle", "die"), cause));

    [Fact]
    public void Partial_sheet_only_swaps_what_it_has()
    {
        SpriteManifest m = Sheet("idle", "die", "die_burn");
        Assert.Equal("die_burn", DeathStates.Pick(m, DeathCause.Burn));
        Assert.Equal("die", DeathStates.Pick(m, DeathCause.Blast));
    }

    [Fact]
    public void Sheet_without_any_die_state_gives_null() => Assert.Null(DeathStates.Pick(Sheet("idle"), DeathCause.Blast));

    [Theory]
    [InlineData("auth_he", 1, 0, DeathCause.Blast)]
    [InlineData("lb_flame", 0, 0, DeathCause.Burn)]
    [InlineData("dir_ash", 1, 0, DeathCause.Rad)]
    [InlineData("dir_rad_shell", 1, 0, DeathCause.Rad)]
    [InlineData("dir_sow", 0, 40, DeathCause.Rad)]
    [InlineData("dir_rocket", 1, 0, DeathCause.Blast)]
    [InlineData("auth_small_arms", 0, 0, DeathCause.Unknown)]
    public void Warhead_maps_to_a_cause(string id, int splash, int contaminate, DeathCause expected)
    {
        var w = new WarheadType(id, new[] { 100 }, splash, false) { Contaminate = contaminate };
        Assert.Equal(expected, DeathStates.CauseOf(w));
    }

    [Fact]
    public void No_warhead_is_unknown() => Assert.Equal(DeathCause.Unknown, DeathStates.CauseOf(null));

    [Theory]
    [InlineData(0, false)]
    [InlineData(64, false)]
    [InlineData(128, false)]
    [InlineData(129, true)]
    [InlineData(192, true)]
    [InlineData(255, true)]
    public void West_facings_mirror_the_east_drawn_animation(byte facing, bool mirrored) => Assert.Equal(mirrored, DeathStates.MirrorWest(facing));
}
