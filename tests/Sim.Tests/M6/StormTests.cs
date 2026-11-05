// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Sim.Core;
using Rts.Sim.Effects;
using Rts.Sim.Map;
using Rts.Sim.Rules;
using Rts.Sim.Tests.M3.Superweapon;
using static Rts.Sim.Tests.M3.Superweapon.SuperweaponFixture;

namespace Rts.Sim.Tests.M6;

/// <summary>M6-07: The Second Drift storm: placement, timers, Blackout and capped contaminate, speed permille and slot replacement.</summary>
public sealed class StormTests
{
    private const int StormTicks = 100, StormRadius = 3;

    private static SimRules StormRules(bool storm)
    {
        SimRules r = SuperweaponFixture.Rules();
        WarheadType w = new("sw_blast", new[] { 100, 100, 100, 100, 100 }, 1, false);
        if (storm) w = w with { StormRadius = StormRadius, StormTicks = StormTicks, StormContaminateStep = 20, StormContaminateCap = 64, StormSpeedPermille = 500 };
        return r with { Warheads = new[] { w } };
    }

    // players: 0 Authority owner, 1 Authority enemy, 2 Left Behind enemy, 3 Authority ally of 0
    private static (Simulation Sim, EntityId Sun) Make(bool storm = true)
    {
        var setup = new SimSetup(1, MapData.CreateFlat(48, 48), 4, StormRules(storm), 0x3607UL)
        {
            AllowDebug = true,
            Factions = new[] { Faction.Authority, Faction.Authority, Faction.LeftBehind, Faction.Authority },
            Teams = new byte[] { 0, 1, 2, 0 },
        };
        var sim = new Simulation(setup);
        sim.PlaceBuilding(0, Plant, new CellCoord(10, 10));
        EntityId sun = sim.PlaceBuilding(0, Sun, new CellCoord(14, 10));
        sim.PlaceBuilding(0, Hatch, new CellCoord(20, 10));
        sim.PlaceBuilding(1, Hut, new CellCoord(44, 44));
        sim.PlaceBuilding(2, Hut, new CellCoord(44, 4));
        sim.PlaceBuilding(3, Hut, new CellCoord(4, 44));
        sim.Effects.BuildingCharge[sun.Index] = Total;
        return (sim, sun);
    }

    private static void FireAt(Simulation sim, EntityId sun, int x, int y) => Run(sim, 1, Fire(sim, 0, sun, x, y));

    private static LeptonCoord Centre(int x, int y) => new((x << 8) + 128, (y << 8) + 128);

    [Fact]
    public void Fire_PlacesStorm_ResetsCharge_NoBlastDamage()
    {
        var (sim, sun) = Make();
        FireAt(sim, sun, 24, 10);
        Assert.Equal((24, 10, StormRadius, 0), (sim.Effects.StormX[0], sim.Effects.StormY[0], sim.Effects.StormRadius[0], (int)sim.Effects.StormOwner[0]));
        Assert.True(sim.Effects.StormTicks[0] > 0);
        Assert.Equal(1, sim.Effects.BuildingCharge[sun.Index]);
        Assert.Equal(0, sim.Effects.StormTicks[1]);
    }

    [Fact]
    public void Storm_TicksDown_AndExpires()
    {
        var (sim, sun) = Make();
        FireAt(sim, sun, 24, 10);
        int left = sim.Effects.StormTicks[0];
        Run(sim, 10);
        Assert.Equal(left - 10, sim.Effects.StormTicks[0]);
        Run(sim, StormTicks);
        Assert.Equal((0, 0, 0, 0), (sim.Effects.StormTicks[0], sim.Effects.StormX[0], sim.Effects.StormRadius[0], (int)sim.Effects.StormOwner[0]));
    }

    [Fact]
    public void Storm_BlacksOutAndContaminatesWithinRadius_NeverAboveCap()
    {
        var (sim, sun) = Make();
        FireAt(sim, sun, 24, 10);
        Run(sim, 40);
        var inside = new CellCoord(26, 12);
        var outside = new CellCoord(28, 10);
        Assert.True(sim.Ground.Blackout[sim.Ground.IndexOf(inside)] > 0);
        Assert.Equal(0, sim.Ground.Blackout[sim.Ground.IndexOf(outside)]);
        Assert.Equal(64, sim.Ground.RadiationAt(inside));   // 20 per pass, capped
        Assert.Equal(0, sim.Ground.RadiationAt(outside));
        Run(sim, 40);
        Assert.Equal(64, sim.Ground.RadiationAt(inside));
    }

    [Fact]
    public void Storm_NeverLowersHigherRadiation()
    {
        var (sim, sun) = Make();
        var c = new CellCoord(24, 10);
        sim.Ground.Radiation[sim.Ground.IndexOf(c)] = 150;
        FireAt(sim, sun, 24, 10);
        Run(sim, 30);
        Assert.Equal(150, sim.Ground.RadiationAt(c));
    }

    [Fact]
    public void SpeedPermille_EnemySlowed_OwnAllyAndLeftBehindNot()
    {
        var (sim, sun) = Make();
        FireAt(sim, sun, 24, 10);
        LeptonCoord inside = Centre(25, 11), outside = Centre(40, 40);
        Assert.Equal(500, sim.StormSpeedPermilleAt(1, inside));   // enemy
        Assert.Equal(1000, sim.StormSpeedPermilleAt(0, inside));  // owner
        Assert.Equal(1000, sim.StormSpeedPermilleAt(3, inside));  // ally
        Assert.Equal(1000, sim.StormSpeedPermilleAt(2, inside));  // Left Behind enemy: immune
        Assert.Equal(1000, sim.StormSpeedPermilleAt(1, outside));
        Assert.True(sim.InFriendlyStorm(0, new CellCoord(25, 11)));
        Assert.False(sim.InFriendlyStorm(1, new CellCoord(25, 11)));
        Assert.False(sim.InFriendlyStorm(0, new CellCoord(40, 40)));
    }

    [Fact]
    public void NineStorms_ReplaceFewestTicksLeft()
    {
        var (sim, sun) = Make();
        for (int i = 0; i < EffectsState.MaxStorms; i++)
        {
            sim.Effects.BuildingCharge[sun.Index] = Total;
            FireAt(sim, sun, 5 + i * 4, 30);
        }
        for (int s = 0; s < EffectsState.MaxStorms; s++) Assert.Equal(5 + s * 4, sim.Effects.StormX[s]);
        // slot 0 is the oldest (fewest ticks left)
        Assert.True(sim.Effects.StormTicks[0] < sim.Effects.StormTicks[7]);
        sim.Effects.BuildingCharge[sun.Index] = Total;
        FireAt(sim, sun, 40, 20);
        Assert.Equal(40, sim.Effects.StormX[0]);
        Assert.Equal(5 + 4, sim.Effects.StormX[1]);
        Assert.Equal(StormTicks, sim.Effects.StormTicks[0] + 1);
    }

    [Fact]
    public void BlastSuperweapon_Unchanged_NoStorm()
    {
        var (sim, sun) = Make(storm: false);
        FireAt(sim, sun, 24, 10);
        for (int s = 0; s < EffectsState.MaxStorms; s++) Assert.Equal(0, sim.Effects.StormTicks[s]);
        Assert.True(sim.Ground.RadiationAt(new CellCoord(24, 10)) >= Simulation.SuperweaponRadiation);
        Assert.Equal(1000, sim.StormSpeedPermilleAt(1, Centre(24, 10)));
    }

    [Fact]
    public void SaveLoadMidStorm_ReplaysToSameHash()
    {
        var (a, sun) = Make();
        FireAt(a, sun, 24, 10);
        Run(a, 33);
        Simulation b = Roundtrip(a);
        Assert.Equal(a.ComputeStateHash(), b.ComputeStateHash());
        Run(a, 120);
        Run(b, 120);
        Assert.Equal(a.ComputeStateHash(), b.ComputeStateHash());
        Assert.Equal(0, b.Effects.StormTicks[0]);
    }
}
