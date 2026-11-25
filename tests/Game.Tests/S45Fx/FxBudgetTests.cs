// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Microsoft.Xna.Framework;
using Rts.Game.Render;
using Rts.Sim.Core;

namespace Rts.Game.Tests.S45Fx;

/// <summary>S45 V-04: the effect budget policy (what is dropped first, what is never dropped).</summary>
public sealed class FxBudgetTests
{
    private static FxCandidate Boom(int size = 1, int off = 0, bool prot = false) => new(FxClass.Explosion, size, off, prot);

    [Fact]
    public void Everything_starts_when_there_is_room()
    {
        var list = new[] { Boom(), new FxCandidate(FxClass.Smoke, 0, 0), new FxCandidate(FxClass.Debris, 0, 3) };
        Assert.All(FxBudget.Admit(list, alive: 0), k => Assert.True(k));
    }

    [Fact]
    public void Frame_cap_limits_how_many_start_and_the_best_survive()
    {
        var list = new List<FxCandidate>();
        for (int i = 0; i < 10; i++) list.Add(new FxCandidate(FxClass.Smoke, 0, 0));   // ten small smokes first
        list.Add(Boom(2));                                                              // then a large explosion
        bool[] keep = FxBudget.Admit(list, alive: 0, frameCap: 3, aliveCap: 1000);
        Assert.Equal(3, keep.Count(k => k));
        Assert.True(keep[10]);   // the explosion wins over the smoke that came first
        Assert.True(keep[0] && keep[1]);   // ties go in list order
        Assert.False(keep[2]);
    }

    [Fact]
    public void Alive_cap_leaves_only_the_room_that_is_left()
    {
        var list = new[] { Boom(), Boom(), Boom() };
        Assert.Equal(2, FxBudget.Admit(list, alive: 8, frameCap: 40, aliveCap: 10).Count(k => k));
        Assert.Equal(0, FxBudget.Admit(list, alive: 10, frameCap: 40, aliveCap: 10).Count(k => k));
        Assert.Equal(0, FxBudget.Admit(list, alive: 500, frameCap: 40, aliveCap: 10).Count(k => k));
    }

    [Fact]
    public void Far_off_screen_goes_before_on_screen_and_small_before_large()
    {
        var list = new[]
        {
            Boom(size: 2, off: 40),                                  // large but far away
            Boom(size: 0, off: 0),                                   // small, on screen
            Boom(size: 2, off: 0),                                   // large, on screen
            new FxCandidate(FxClass.Smoke, 2, 0),                    // smoke on screen
        };
        bool[] keep = FxBudget.Admit(list, alive: 0, frameCap: 2, aliveCap: 100);
        Assert.False(keep[0]);
        Assert.True(keep[2]);   // large explosion on screen
        Assert.True(keep[1]);   // beats smoke
        Assert.False(keep[3]);
    }

    [Fact]
    public void Smoke_is_dropped_before_explosions_and_debris_before_explosions()
    {
        Assert.True(FxBudget.Score(Boom(0)) > FxBudget.Score(new FxCandidate(FxClass.Debris, 2, 0)));
        Assert.True(FxBudget.Score(new FxCandidate(FxClass.Debris, 0, 0)) > FxBudget.Score(new FxCandidate(FxClass.Smoke, 3, 0)));
        Assert.True(FxBudget.Score(new FxCandidate(FxClass.Smoke, 0, 0)) > FxBudget.Score(Boom(3, FxBudget.NearCells + 1)));   // seen beats unseen
    }

    [Fact]
    public void Protected_effects_are_kept_even_when_full_and_even_far_away()
    {
        var list = new List<FxCandidate>();
        for (int i = 0; i < 50; i++) list.Add(Boom(2));
        list.Add(new FxCandidate(FxClass.Superweapon, 3, 90, true));
        list.Add(Boom(0, 90, prot: true));   // a selected unit's death, off screen and small
        bool[] keep = FxBudget.Admit(list, alive: 9999, frameCap: 5, aliveCap: 100);
        Assert.True(keep[50]);
        Assert.True(keep[51]);
        Assert.Equal(2, keep.Count(k => k));   // nothing else gets room
        Assert.Equal(int.MaxValue, FxBudget.Score(list[50]));
    }

    [Fact]
    public void Protected_effects_use_up_room_before_the_rest()
    {
        var list = new[] { Boom(2), Boom(2), Boom(0, 0, prot: true) };
        bool[] keep = FxBudget.Admit(list, alive: 0, frameCap: 2, aliveCap: 100);
        Assert.True(keep[2]);
        Assert.Equal(2, keep.Count(k => k));
        Assert.True(keep[0]);
        Assert.False(keep[1]);
    }

    [Fact]
    public void Same_input_gives_the_same_answer()
    {
        var list = new List<FxCandidate>();
        for (int i = 0; i < 60; i++) list.Add(new FxCandidate((FxClass)(i % 4), i % 3, i % 12));
        Assert.Equal(FxBudget.Admit(list, 30, 17, 80), FxBudget.Admit(list, 30, 17, 80));
    }

    [Fact]
    public void Size_class_of_scale()
    {
        Assert.Equal(0, FxBudget.SizeClassOfScale(1f));
        Assert.Equal(1, FxBudget.SizeClassOfScale(1.5f));
        Assert.Equal(2, FxBudget.SizeClassOfScale(2f));
    }

    [Fact]
    public void Off_screen_distance_is_zero_inside_and_counts_cells_outside()
    {
        var view = new Rectangle(0, 0, 1000, 600);
        Assert.Equal(0, FxBudget.OffScreenCells(view, new Vector2(500, 300), 64f));
        Assert.Equal(0, FxBudget.OffScreenCells(view, new Vector2(1000, 600), 64f));
        Assert.Equal(1, FxBudget.OffScreenCells(view, new Vector2(1010, 300), 64f));
        Assert.Equal(2, FxBudget.OffScreenCells(view, new Vector2(500, -128), 64f));
        Assert.Equal(10, FxBudget.OffScreenCells(view, new Vector2(-640, -10), 64f));
    }

    [Fact]
    public void Near_any_finds_a_selected_unit_within_the_radius()
    {
        var points = new[] { new LeptonCoord(1000, 1000), new LeptonCoord(5000, 5000) };
        Assert.True(FxBudget.NearAny(new LeptonCoord(1100, 900), points, 384));
        Assert.True(FxBudget.NearAny(new LeptonCoord(5384, 5000), points, 384));
        Assert.False(FxBudget.NearAny(new LeptonCoord(3000, 3000), points, 384));
        Assert.False(FxBudget.NearAny(new LeptonCoord(0, 0), Array.Empty<LeptonCoord>(), 384));
    }

    [Fact]
    public void Budget_toggle_defaults_on_and_round_trips()
    {
        Assert.True(Settings.Default.EffectBudget);
        Assert.False(Settings.Parse((Settings.Default with { EffectBudget = false }).ToToml()).EffectBudget);
        Assert.True(Settings.Parse("version = 2\n").EffectBudget);
    }
}
