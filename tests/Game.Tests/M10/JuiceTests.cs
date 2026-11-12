// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Game.Render;
using Rts.Sim.Core;

namespace Rts.Game.Tests.M10;

/// <summary>M10-14: shake decay, blast thresholds, hit-flash timing and muzzle counting.</summary>
public sealed class JuiceTests
{
    private static UnitView Unit(ushort index, int health) =>
        new(new EntityId(index, 1), UnitKind.Vehicle, 0, 0, new LeptonCoord(256, 256), 0, health);

    [Fact]
    public void Shake_is_idle_until_kicked()
    {
        var s = new JuiceShake();
        Assert.False(s.Active);
        Assert.Equal(0f, s.Amplitude);
        Assert.Equal(Microsoft.Xna.Framework.Vector2.Zero, s.Offset);
    }

    [Fact]
    public void Shake_decays_to_zero_over_the_duration()
    {
        var s = new JuiceShake();
        s.Kick(6f);
        Assert.Equal(6f, s.Amplitude, 3);
        s.Update(JuiceShake.DurationSeconds / 2);
        Assert.Equal(3f, s.Amplitude, 3);
        s.Update(JuiceShake.DurationSeconds / 2);
        Assert.Equal(0f, s.Amplitude);
        Assert.False(s.Active);
    }

    [Fact]
    public void Shake_offset_never_exceeds_the_amplitude_and_is_repeatable()
    {
        var a = new JuiceShake();
        var b = new JuiceShake();
        a.Kick(5f);
        b.Kick(5f);
        for (int i = 0; i < 10; i++)
        {
            a.Update(0.02f);
            b.Update(0.02f);
            Assert.Equal(a.Offset, b.Offset);
            Assert.True(MathF.Abs(a.Offset.X) <= a.Amplitude + 1f && MathF.Abs(a.Offset.Y) <= a.Amplitude + 1f);
        }
    }

    [Fact]
    public void Shake_is_capped_when_blasts_stack()
    {
        var s = new JuiceShake();
        for (int i = 0; i < 20; i++) s.Kick(JuiceShake.MaxAmplitudePx);
        Assert.Equal(JuiceShake.MaxAmplitudePx, s.Amplitude, 3);
        s.Kick(1000f);
        Assert.Equal(JuiceShake.MaxAmplitudePx, s.Amplitude, 3);
        s.Clear();
        Assert.False(s.Active);
    }

    [Fact]
    public void Negative_or_zero_kicks_and_time_do_nothing()
    {
        var s = new JuiceShake();
        s.Kick(0f);
        s.Kick(-3f);
        Assert.False(s.Active);
        s.Kick(4f);
        s.Update(-1f);
        Assert.Equal(4f, s.Amplitude, 3);
    }

    [Fact]
    public void Only_big_blasts_shake()
    {
        LeptonCoord at = new(0, 0);
        Assert.Equal(0f, JuiceBlast.Amplitude(new FxEvent(FxKind.UnitDestroyed, at, 1)));
        Assert.Equal(0f, JuiceBlast.Amplitude(new FxEvent(FxKind.InstantHit, at, 1)));
        Assert.Equal(0f, JuiceBlast.Amplitude(new FxEvent(FxKind.Detonation, at, 1)));
        Assert.Equal(0f, JuiceBlast.Amplitude(new FxEvent(FxKind.BuildingDestroyed, at, 1)));
        Assert.True(JuiceBlast.Amplitude(new FxEvent(FxKind.Detonation, at, JuiceBlast.LargeSplashCells)) > 0f);
        Assert.True(JuiceBlast.Amplitude(new FxEvent(FxKind.BuildingDestroyed, at, 3)) > 0f);
        Assert.True(JuiceBlast.Amplitude(new FxEvent(FxKind.Superweapon, at, 6)) >
                    JuiceBlast.Amplitude(new FxEvent(FxKind.BuildingDestroyed, at, 3)));
    }

    [Fact]
    public void Hit_flash_lasts_two_ticks()
    {
        var f = new JuiceHitFlash();
        EntityId id = new(1, 1);
        f.Observe(10, new[] { Unit(1, 100) }, 1);
        Assert.False(f.IsFlashing(id, 10, 1));
        f.Observe(11, new[] { Unit(1, 80) }, 2);
        Assert.True(f.IsFlashing(id, 11, 2));
        f.Observe(12, new[] { Unit(1, 80) }, 3);
        Assert.True(f.IsFlashing(id, 12, 3));
        f.Observe(13, new[] { Unit(1, 80) }, 4);
        Assert.False(f.IsFlashing(id, 13, 4));
        Assert.Equal(0, f.Count);
    }

    [Fact]
    public void Hit_flash_ends_when_the_game_is_paused()
    {
        var f = new JuiceHitFlash();
        EntityId id = new(1, 1);
        f.Observe(10, new[] { Unit(1, 100) }, 1);
        f.Observe(11, new[] { Unit(1, 50) }, 2);
        for (int frame = 3; frame < 2 + JuiceHitFlash.MaxFrames; frame++) f.Observe(11, new[] { Unit(1, 50) }, frame);
        Assert.False(f.IsFlashing(id, 11, 2 + JuiceHitFlash.MaxFrames));
    }

    [Fact]
    public void Hit_flash_ignores_healing_new_units_and_other_units()
    {
        var f = new JuiceHitFlash();
        f.Observe(1, new[] { Unit(1, 50), Unit(2, 100) }, 1);
        f.Observe(2, new[] { Unit(1, 70), Unit(2, 90), Unit(3, 10) }, 2);
        Assert.False(f.IsFlashing(new EntityId(1, 1), 2, 2));
        Assert.True(f.IsFlashing(new EntityId(2, 1), 2, 2));
        Assert.False(f.IsFlashing(new EntityId(3, 1), 2, 2));
    }

    [Fact]
    public void Hit_flash_clears_when_time_goes_backwards()
    {
        var f = new JuiceHitFlash();
        f.Observe(50, new[] { Unit(1, 100) }, 1);
        f.Observe(51, new[] { Unit(1, 10) }, 2);
        f.Observe(5, new[] { Unit(1, 100) }, 3);
        Assert.Equal(0, f.Count);
    }

    [Theory]
    [InlineData(3, 5, 0, 2)]
    [InlineData(3, 3, 0, 0)]
    [InlineData(3, 3, 2, 2)]
    [InlineData(5, 3, 0, 0)]
    [InlineData(0, 40, 0, JuiceMuzzle.MaxPerTick)]
    [InlineData(0, 1, 3, 1)]
    public void Muzzle_counts_new_shells(int prev, int cur, int landed, int expected) =>
        Assert.Equal(expected, JuiceMuzzle.NewShots(prev, cur, landed));
}
