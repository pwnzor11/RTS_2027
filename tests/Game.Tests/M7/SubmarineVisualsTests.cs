// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Game.Render;
using Rts.Game.Sprites;
using Rts.Sim.Core;

namespace Rts.Game.Tests.M7;

/// <summary>M7-17: a dived submarine draws its dived frames; only the owner's and allies' dived ships are semi-transparent.</summary>
public sealed class SubmarineVisualsTests
{
    private static UnitView Sub(bool underground = false) =>
        new(new EntityId(3, 1), UnitKind.Vehicle, 0, 4, default, 64, 100) { Underground = underground };

    [Fact]
    public void Own_and_allied_dived_ships_are_faded()
    {
        Assert.Equal(UnitVisuals.SubmergedAlpha, UnitVisuals.Alpha(Sub(), submerged: true, ownOrAllied: true));
        Assert.True(UnitVisuals.SubmergedAlpha < 1f);
    }

    [Fact]
    public void An_enemy_dived_ship_found_by_sonar_is_drawn_opaque()
    {
        Assert.Equal(1f, UnitVisuals.Alpha(Sub(), submerged: true, ownOrAllied: false));
    }

    [Fact]
    public void A_surfaced_ship_is_opaque_whoever_owns_it()
    {
        Assert.Equal(1f, UnitVisuals.Alpha(Sub(), submerged: false, ownOrAllied: true));
        Assert.Equal(1f, UnitVisuals.Alpha(Sub(), submerged: false, ownOrAllied: false));
    }

    [Fact]
    public void A_dived_own_ship_keeps_the_faintest_of_its_other_states()
    {
        Assert.Equal(UnitVisuals.UndergroundAlpha, UnitVisuals.Alpha(Sub(underground: true), submerged: true, ownOrAllied: true));
    }

    [Fact]
    public void The_missile_submarine_manifest_has_16_dived_facings_after_the_surfaced_frames()
    {
        SpriteManifest m = SpriteManifest.Load(RepoFile("data/sprites/vehicles/exp_missile_sub.sprite.toml"));
        Assert.True(SpriteStates.HasState(m, UnitVisuals.DivedState));
        SpriteState dived = m.State(UnitVisuals.DivedState);
        Assert.Equal(16, dived.Facings);
        Assert.Equal(16, dived.First);
        Assert.Equal(16, m.FrameIndex(UnitVisuals.DivedState, 0, 0));
        Assert.Equal(20, m.FrameIndex(UnitVisuals.DivedState, 64, 0));   // byte 64 is facing 4 of 16 (east)
        Assert.Equal(4, m.FrameIndex("idle", 64, 0));                    // the surfaced body keeps frames 0..15
    }

    [Fact]
    public void A_dived_ship_draws_the_dived_frame_for_its_facing_and_a_surfaced_one_does_not()
    {
        SpriteManifest m = SpriteManifest.Load(RepoFile("data/sprites/vehicles/exp_missile_sub.sprite.toml"));
        Assert.Equal(20, UnitVisuals.DivedBodyFrame(m, 64, tick: 0, submerged: true));
        Assert.Equal(-1, UnitVisuals.DivedBodyFrame(m, 64, tick: 0, submerged: false));
    }

    [Fact]
    public void A_sprite_without_a_dived_state_keeps_its_surfaced_body_even_when_dived()
    {
        SpriteManifest minelayer = SpriteManifest.Load(RepoFile("data/sprites/vehicles/exp_minelayer.sprite.toml"));
        Assert.False(SpriteStates.HasState(minelayer, UnitVisuals.DivedState));
        Assert.Equal(-1, UnitVisuals.DivedBodyFrame(minelayer, 64, tick: 0, submerged: true));
    }

    private static string RepoFile(string relative)
    {
        string? dir = AppContext.BaseDirectory;
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir, "RTS_2027.slnx"))) return Path.Combine(dir, relative);
            dir = Path.GetDirectoryName(dir);
        }

        throw new FileNotFoundException("RTS_2027.slnx not found");
    }
}
