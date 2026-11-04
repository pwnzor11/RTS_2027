// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Game.Render;
using Rts.Sim.Core;

namespace Rts.Game.Tests.M3_12;

/// <summary>M3-12: a building death's explosions scale with the footprint; units and detonations keep one small explosion.</summary>
public sealed class ExplosionPlanTests
{
    private static FxEvent Building(int side) => new(FxKind.BuildingDestroyed, default, side);

    [Fact]
    public void A_unit_or_a_detonation_keeps_one_small_unscaled_explosion()
    {
        ExplosionSpot unit = Assert.Single(ExplosionPlan.For(new FxEvent(FxKind.UnitDestroyed, default, 1)));
        Assert.Equal(new ExplosionSpot("explosion_small", 0f, 0f, 1f), unit);
        ExplosionSpot shell = Assert.Single(ExplosionPlan.For(new FxEvent(FxKind.Detonation, default, 4)));
        Assert.Equal(new ExplosionSpot("explosion_small", 0f, 0f, 1f), shell);
    }

    [Fact]
    public void A_one_by_one_building_gets_one_large_explosion_at_full_size()
    {
        ExplosionSpot only = Assert.Single(ExplosionPlan.For(Building(1)));
        Assert.Equal(new ExplosionSpot("explosion_large", 0f, 0f, 1f), only);
    }

    [Fact]
    public void A_three_by_three_building_gets_three_bigger_blasts_around_its_centre()
    {
        IReadOnlyList<ExplosionSpot> spots = ExplosionPlan.For(Building(3));
        Assert.Equal(3, spots.Count);
        Assert.All(spots, s => Assert.Equal(("explosion_large", 1.5f), (s.Kind, s.Scale)));
        Assert.All(spots, s => Assert.True(MathF.Sqrt(s.DxCells * s.DxCells + s.DyCells * s.DyCells) <= 0.5f + 1e-4f));
    }

    [Fact]
    public void Bigger_buildings_blow_up_more_but_are_capped()
    {
        IReadOnlyList<ExplosionSpot> big = ExplosionPlan.For(Building(6));
        Assert.Equal(ExplosionPlan.MaxBuildingBlasts, big.Count);
        Assert.All(big, s => Assert.Equal(ExplosionPlan.MaxScale, s.Scale));
        Assert.True(ExplosionPlan.For(Building(2))[0].Scale < ExplosionPlan.For(Building(3))[0].Scale);
    }
}
