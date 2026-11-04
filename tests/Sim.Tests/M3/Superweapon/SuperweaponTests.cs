// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Sim.Core;
using Rts.Sim.Map;
using static Rts.Sim.Tests.M3.Superweapon.SuperweaponFixture;

namespace Rts.Sim.Tests.M3.Superweapon;

/// <summary>M3-13: the Sunrise Device charges, slows on low power, fires only when charged and in entrance range, and resets.</summary>
public sealed class SuperweaponTests
{
    private static int Charge(Simulation sim, EntityId sun) => sim.Effects.BuildingCharge[sun.Index];

    [Fact]
    public void Charge_AdvancesOnePerTick_AndReadyAtTotal()
    {
        var (sim, sun) = Base();
        Run(sim, Total - 1);
        Assert.Equal(Total - 1, Charge(sim, sun));
        Assert.False(sim.HudFor(0).Superweapons[0].Ready);
        Run(sim, 1);
        Assert.Equal(Total, Charge(sim, sun));
        Run(sim, 50);
        Assert.Equal(Total, Charge(sim, sun));   // capped
        SuperweaponView v = Assert.Single(sim.HudFor(0).Superweapons);
        Assert.True(v.Ready);
        Assert.Equal((Total, Total, EntRange), (v.Charge, v.Total, v.Range));
    }

    [Fact]
    public void Charge_NoPower_Pauses()
    {
        var (sim, sun) = Base(plant: false);
        Run(sim, 80);
        Assert.True(Charge(sim, sun) <= 1);   // the first tick still sees the not-yet-computed power
        SuperweaponView v = Assert.Single(sim.HudFor(0).Superweapons);
        Assert.True(v.Paused);
    }

    [Fact]
    public void Charge_BandOne_RunsAtTwoThirds()
    {
        Simulation sim = NewSim();
        sim.PlaceBuilding(0, Plant, new CellCoord(4, 4));
        EntityId sun = sim.PlaceBuilding(0, Sun, new CellCoord(14, 10));
        for (int i = 0; i < 14; i++) sim.PlaceBuilding(0, Sun, new CellCoord(2 + (i % 7) * 4, 20 + (i / 7) * 4));  
        sim.PlaceBuilding(1, Hut, new CellCoord(44, 44));
        // 15 Suns draw 300 against 300: balanced. A 16th puts use at 320 vs 300 = 93 % (band 1).
        sim.PlaceBuilding(0, Sun, new CellCoord(30, 30));
        sim.Step(None);
        sim.Step(None);
        Assert.Equal(1, sim.PowerBand(0));
        int before = Charge(sim, sun);
        Run(sim, 90);
        int gained = Charge(sim, sun) - before;
        Assert.InRange(gained, 58, 62);
    }

    [Fact]
    public void Special_BeforeCharged_DoesNothing()
    {
        var (sim, sun) = Base();
        Run(sim, 50);
        Run(sim, 1, Fire(sim, 0, sun, 24, 10));
        Assert.Equal(51, Charge(sim, sun));
        Assert.Equal(0, sim.Ground.RadiationAt(new CellCoord(24, 10)));
        Assert.Equal(0, sim.Ground.Blackout[sim.Ground.IndexOf(new CellCoord(24, 10))]);
    }

    private static (Simulation Sim, EntityId Sun) Charged(bool hatch = true, Action<Simulation>? setup = null)
    {
        var (sim, sun) = Base(hatch);
        sim.Effects.BuildingCharge[sun.Index] = Total;
        setup?.Invoke(sim);
        sim.Step(None);   // tick 1 next: radiation damage runs on multiples of 20 only
        return (sim, sun);
    }

    [Fact]
    public void Fire_DamagesRadiatesAndBlacksOut_AndResetsCharge()
    {
        EntityId centre = default, mid = default, far = default, own = default;
        var (sim, sun) = Charged(setup: s =>
        {
            centre = s.SpawnVehicle(1, Brute, new CellCoord(26, 10));
            mid = s.SpawnVehicle(1, Brute, new CellCoord(26, 14));    // d = 4 > radius/2: half damage
            far = s.SpawnVehicle(1, Brute, new CellCoord(26, 17));    // d = 7: outside
            own = s.SpawnVehicle(0, Brute, new CellCoord(26, 11));    // friendly: spared
        });
        Run(sim, 1, Fire(sim, 0, sun, 26, 10));
        Assert.Equal(2000 - Simulation.SuperweaponDamage, sim.Units.Health[centre.Index]);
        Assert.Equal(2000 - Simulation.SuperweaponDamage / 2, sim.Units.Health[mid.Index]);
        Assert.Equal(2000, sim.Units.Health[far.Index]);
        Assert.Equal(2000, sim.Units.Health[own.Index]);
        Assert.Equal(Simulation.SuperweaponRadiation, sim.Ground.RadiationAt(new CellCoord(26, 10)));
        Assert.Equal(Simulation.SuperweaponRadiation, sim.Ground.RadiationAt(new CellCoord(21, 15)));
        Assert.Equal(0, sim.Ground.RadiationAt(new CellCoord(26, 17)));
        Assert.True(sim.Ground.Blackout[sim.Ground.IndexOf(new CellCoord(26, 10))] >= Simulation.SuperweaponBlackoutTicks - 2);
        Assert.Equal(0, sim.Ground.Blackout[sim.Ground.IndexOf(new CellCoord(26, 17))]);
        Assert.InRange(Charge(sim, sun), 0, 1);   // reset by Special, then the same tick's charge step adds one
        Assert.Contains(sim.FxEvents, e => e.Kind == FxKind.Superweapon && e.Size == Simulation.SuperweaponBlastRadius);
        SuperweaponView v = Assert.Single(sim.HudFor(0).Superweapons);
        Assert.False(v.Ready);
    }

    [Fact]
    public void Fire_RadiationKeepsHurting()
    {
        EntityId e = default;
        var (sim, sun) = Charged(setup: s => e = s.SpawnVehicle(1, Brute, new CellCoord(26, 10)));
        Run(sim, 1, Fire(sim, 0, sun, 26, 10));
        int hp = sim.Units.Health[e.Index];
        Run(sim, 60);
        Assert.True(sim.Units.Health[e.Index] < hp);
    }

    [Fact]
    public void Fire_Twice_SecondIsRefused_UntilRecharged()
    {
        var (sim, sun) = Charged();
        Run(sim, 1, Fire(sim, 0, sun, 26, 10));
        int rad = sim.Ground.RadiationAt(new CellCoord(26, 10));
        Run(sim, 1, Fire(sim, 0, sun, 26, 10));
        Assert.True(sim.Ground.RadiationAt(new CellCoord(26, 10)) <= rad);
        Run(sim, Total + 1);
        Assert.Equal(Total, Charge(sim, sun));
        Assert.True(sim.CanFireSuperweapon(0, sun, new CellCoord(26, 10)));
    }

    [Theory]
    [InlineData(28, 10, true)]    // hatch footprint x 20..21: 28 - 21 = 7 cells
    [InlineData(29, 10, true)]    // exactly 8
    [InlineData(30, 10, false)]   // 9
    [InlineData(26, 18, true)]    // dy 8 from the footprint's row 10..11? 18 - 11 = 7
    [InlineData(26, 20, false)]
    public void EntranceRange_IsMeasuredFromTheFootprint(int x, int y, bool allowed)
    {
        var (sim, sun) = Charged();
        Assert.Equal(allowed, sim.CanFireSuperweapon(0, sun, new CellCoord(x, y)));
        Run(sim, 1, Fire(sim, 0, sun, x, y));
        Assert.Equal(allowed, Charge(sim, sun) <= 1);
    }

    [Fact]
    public void EntranceRange_NoOwnEntrance_Refuses()
    {
        var (sim, sun) = Charged(hatch: false, setup: s => s.PlaceBuilding(1, Hatch, new CellCoord(26, 10)));   // an enemy entrance does not count
        Run(sim, 1, Fire(sim, 0, sun, 26, 10));
        Assert.Equal(Total, Charge(sim, sun));
        Assert.Equal(0, sim.Ground.RadiationAt(new CellCoord(26, 10)));
    }

    [Fact]
    public void Fire_ByAnotherPlayer_OrOffMap_Refused()
    {
        var (sim, sun) = Charged();
        Run(sim, 1, Fire(sim, 1, sun, 26, 10));
        Assert.Equal(Total, Charge(sim, sun));
        Assert.False(sim.CanFireSuperweapon(0, sun, new CellCoord(-1, 10)));
        Assert.False(sim.CanFireSuperweapon(0, default, new CellCoord(26, 10)));
    }

    [Fact]
    public void SaveLoad_MidCharge_ContinuesIdentically()
    {
        var (sim, sun) = Base();
        Run(sim, 40);
        Simulation loaded = Roundtrip(sim);
        Assert.Equal(sim.ComputeStateHash(), loaded.ComputeStateHash());
        Assert.Equal(40, loaded.Effects.BuildingCharge[sun.Index]);
        for (int i = 0; i < 70; i++)
        {
            Command[] cmds = i == 65 ? new[] { Fire(sim, 0, sun, 26, 10) } : None;
            Command[] copy = i == 65 ? new[] { Fire(loaded, 0, sun, 26, 10) } : None;
            sim.Step(cmds);
            loaded.Step(copy);
        }
        Assert.Equal(sim.ComputeStateHash(), loaded.ComputeStateHash());
        Assert.InRange(loaded.Effects.BuildingCharge[sun.Index], 1, Total - 1);   // fired at 100 -> reset, recharging since
    }

    [Fact]
    public void Determinism_SameScript_SameHash()
    {
        ulong Play()
        {
            var (sim, sun) = Base();
            sim.SpawnVehicle(1, Brute, new CellCoord(26, 10));
            for (int i = 0; i < 160; i++) sim.Step(i == 120 ? new[] { Fire(sim, 0, sun, 26, 10) } : None);
            return sim.ComputeStateHash();
        }
        Assert.Equal(Play(), Play());
    }

    [Fact]
    public void DestroyedBuilding_LosesItsCharge()
    {
        var (sim, sun) = Base();
        Run(sim, 30);
        sim.Buildings.Health[sun.Index] = 0;
        Run(sim, 2);
        Assert.Equal(0, sim.Effects.BuildingCharge[sun.Index]);
        Assert.Empty(sim.HudFor(0).Superweapons);
    }
}
