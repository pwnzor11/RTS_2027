// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Data;
using Rts.Sim.Core;
using Rts.Sim.Effects;
using Rts.Sim.Map;
using Rts.Sim.Rules;
using Rts.Sim.Tests.M3.Superweapon;
using Rts.Sim.Tests.Rules;
using static Rts.Sim.Tests.M3.Superweapon.SuperweaponFixture;

namespace Rts.Sim.Tests.M7;

/// <summary>M7-12: the Salvo Array: charge, fire, refusal before charge, the missile pattern, save/load mid-charge and mid-salvo,
/// replay determinism and the old superweapon path unchanged.</summary>
public sealed class SalvoArrayTests
{
    private const int Missiles = 4, SalvoTicks = 40, Warn = 20, Damage = 100, Radius = 2, Hp = 2000;

    private static SimRules SalvoRules(bool salvo)
    {
        SimRules r = SuperweaponFixture.Rules();
        WarheadType w = new("sw_blast", new[] { 100, 100, 100, 100, 100 }, 1, false);
        if (salvo) w = w with { SalvoMissiles = Missiles, SalvoTicks = SalvoTicks, SalvoRadius = Radius, SalvoDamage = Damage, SalvoWarnTicks = Warn, SalvoSplash = 0 };
        return r with { Warheads = new[] { w } };
    }

    // player 0 owns the Array; 1 is the enemy; every cell in the 5x5 square around (25,14) holds an enemy tank (so each missile
    // hits exactly one), plus one own tank in the middle.
    private static (Simulation Sim, EntityId Sun, EntityId[] Tanks, EntityId Own) Make(bool salvo = true, ulong seed = 1)
    {
        var setup = new SimSetup(seed, MapData.CreateFlat(48, 48), 2, SalvoRules(salvo), 0x712UL) { AllowDebug = true };
        var sim = new Simulation(setup);
        sim.PlaceBuilding(0, Plant, new CellCoord(10, 10));
        EntityId sun = sim.PlaceBuilding(0, Sun, new CellCoord(14, 10));
        sim.PlaceBuilding(0, Hatch, new CellCoord(20, 10));
        sim.PlaceBuilding(1, Hut, new CellCoord(44, 44));
        var tanks = new List<EntityId>();
        for (int y = 12; y <= 16; y++)
            for (int x = 23; x <= 27; x++)
                if (x != 25 || y != 14) tanks.Add(sim.SpawnVehicle(1, Brute, new CellCoord(x, y)));
        tanks.Add(sim.SpawnVehicle(1, Brute, new CellCoord(25, 14)));
        EntityId own = sim.SpawnVehicle(0, Brute, new CellCoord(25, 15));
        return (sim, sun, tanks.ToArray(), own);
    }

    private static int Lost(Simulation sim, EntityId[] tanks) => tanks.Sum(t => Hp - sim.Units.Health[t.Index]);

    private static void Charge(Simulation sim, EntityId sun) => sim.Effects.BuildingCharge[sun.Index] = Total;

    [Fact]
    public void Charges_ToTotal_AndShowsInView()
    {
        var (sim, sun, _, _) = Make();
        Run(sim, Total - 1);
        Assert.Equal(Total - 1, sim.Effects.BuildingCharge[sun.Index]);
        Run(sim, 1);
        Assert.Equal(Total, sim.Effects.BuildingCharge[sun.Index]);
        SuperweaponView v = sim.ViewFor(0).Hud.Superweapons.Single();
        Assert.True(v.Ready);
        Assert.Equal(Radius, v.BlastRadius);
    }

    [Fact]
    public void RefusedBeforeCharged_NothingHappens()
    {
        var (sim, sun, tanks, _) = Make();
        Run(sim, 10);
        Assert.False(sim.CanFireSuperweapon(0, sun, new CellCoord(25, 14)));
        Run(sim, 1, Fire(sim, 0, sun, 25, 14));
        Run(sim, Warn + SalvoTicks + 5);
        Assert.False(sim.Effects.AnySalvo);
        Assert.Equal(0, Lost(sim, tanks));
    }

    [Fact]
    public void Fire_ResetsCharge_ThenMissilesLandAfterWarning()
    {
        var (sim, sun, tanks, _) = Make();
        Charge(sim, sun);
        Assert.True(sim.CanFireSuperweapon(0, sun, new CellCoord(25, 14)));
        Run(sim, 1, Fire(sim, 0, sun, 25, 14));
        Assert.True(sim.Effects.AnySalvo);
        Assert.True(sim.Effects.BuildingCharge[sun.Index] <= 1);
        Run(sim, Warn - 3);                       // elapsed < Warn: no impact yet
        Assert.Equal(0, Lost(sim, tanks));
        Run(sim, 4);                              // first missile lands at elapsed == Warn
        Assert.Equal(Damage, Lost(sim, tanks));
        Run(sim, SalvoTicks + 5);
        Assert.Equal(Missiles * Damage, Lost(sim, tanks));   // every missile hit exactly one tank of the full square
        Assert.False(sim.Effects.AnySalvo);
    }

    [Fact]
    public void Missiles_ArriveSpreadOverTime_AndSpareOwnUnits()
    {
        var (sim, sun, tanks, own) = Make();
        Charge(sim, sun);
        Run(sim, 1, Fire(sim, 0, sun, 25, 14));
        Run(sim, Warn + SalvoTicks / 2 - 1);
        int mid = Lost(sim, tanks);
        Assert.InRange(mid, 2 * Damage, 3 * Damage);   // missiles 0,1 (and maybe 2) are down, not all
        Run(sim, SalvoTicks);
        Assert.Equal(Missiles * Damage, Lost(sim, tanks));
        Assert.Equal(Hp, sim.Units.Health[own.Index]);
    }

    [Fact]
    public void SecondFire_RequiresRecharge()
    {
        var (sim, sun, _, _) = Make();
        Charge(sim, sun);
        Run(sim, 1, Fire(sim, 0, sun, 25, 14));
        Assert.False(sim.CanFireSuperweapon(0, sun, new CellCoord(25, 14)));
    }

    [Fact]
    public void AnyOnMapCellInEntranceRange_IsAllowed_AndOffMapIsNot()
    {
        var (sim, sun, _, _) = Make();
        Charge(sim, sun);
        Assert.True(sim.CanFireSuperweapon(0, sun, new CellCoord(27, 4)));
        Assert.False(sim.CanFireSuperweapon(0, sun, new CellCoord(-1, 5)));
    }

    [Fact]
    public void SaveLoadMidCharge_SameHash()
    {
        var (a, _, _, _) = Make();
        Run(a, 37);
        Simulation b = Roundtrip(a);
        Assert.Equal(a.ComputeStateHash(), b.ComputeStateHash());
        Run(a, 80); Run(b, 80);
        Assert.Equal(a.ComputeStateHash(), b.ComputeStateHash());
    }

    [Fact]
    public void SaveLoadMidSalvo_SameHashAndSameOutcome()
    {
        var (a, sun, tanks, _) = Make();
        Charge(a, sun);
        Run(a, 1, Fire(a, 0, sun, 25, 14));
        Run(a, Warn + 7);
        Assert.True(a.Effects.AnySalvo);
        Simulation b = Roundtrip(a);
        Assert.Equal(a.ComputeStateHash(), b.ComputeStateHash());
        Assert.True(b.Effects.AnySalvo);
        Run(a, SalvoTicks); Run(b, SalvoTicks);
        Assert.Equal(a.ComputeStateHash(), b.ComputeStateHash());
        Assert.False(b.Effects.AnySalvo);
        for (int i = 0; i < tanks.Length; i++) Assert.Equal(a.Units.Health[tanks[i].Index], b.Units.Health[tanks[i].Index]);
    }

    [Fact]
    public void SameSeedSameCommands_SameHash_DifferentSeedScatters()
    {
        static ulong Play(ulong seed, out int[] hp)
        {
            var (sim, sun, tanks, _) = Make(true, seed);
            Charge(sim, sun);
            Run(sim, 1, Fire(sim, 0, sun, 25, 14));
            Run(sim, Warn + SalvoTicks + 10);
            hp = tanks.Select(t => sim.Units.Health[t.Index]).ToArray();
            return sim.ComputeStateHash();
        }
        Assert.Equal(Play(5, out int[] h1), Play(5, out int[] h2));
        Assert.Equal(h1, h2);
        Play(6, out int[] h3);
        Assert.NotEqual(h1, h3);
    }

    [Fact]
    public void NonSalvoSuperweapon_Unchanged_AndEffectsStayVersion2()
    {
        var (sim, sun, tanks, _) = Make(salvo: false);
        Assert.Equal(2, EffectsVersion(sim));
        Charge(sim, sun);
        Run(sim, 1, Fire(sim, 0, sun, 25, 14));
        Assert.False(sim.Effects.AnySalvo);
        Assert.Equal(2, EffectsVersion(sim));
        Assert.True(Hp - sim.Units.Health[tanks[^1].Index] >= Simulation.SuperweaponDamage);   // the old blast hit the centre tank at once (radiation adds a little)
        var (s2, sun2, _, _) = Make(salvo: true);
        Charge(s2, sun2);
        Run(s2, 1, Fire(s2, 0, sun2, 25, 14));
        Assert.Equal(3, EffectsVersion(s2));
    }

    private static int EffectsVersion(Simulation sim)
    {
        using var ms = new MemoryStream();
        using (var w = new BinaryWriter(ms, System.Text.Encoding.UTF8, leaveOpen: true)) sim.Effects.Write(w);
        return ms.ToArray()[0];
    }

    [Fact]
    public void LoaderParsesSalvoKeys_DefaultsOff_AndEachKeyMovesTheRulesHash()
    {
        string baseWh = TempRules.Real("warheads.toml") + "\n[test_salvo]\nversus = [100, 100, 100, 100, 100]\nsplash = 0\nhits_allies = false\n";
        using var t0 = new TempRules(("warheads.toml", baseWh));
        RuleSet r0 = RulesLoader.Load(t0.Dir);
        WarheadType d = r0.Sim.Warheads.Single(x => x.Id == "test_salvo");
        Assert.Equal((0, 0, 0, 0, 0, 1), (d.SalvoMissiles, d.SalvoTicks, d.SalvoRadius, d.SalvoDamage, d.SalvoWarnTicks, d.SalvoSplash));
        var seen = new HashSet<ulong> { r0.RulesHash };
        foreach (string key in new[] { "salvo_missiles = 7", "salvo_ticks = 7", "salvo_radius = 7", "salvo_damage = 7", "salvo_warn_ticks = 7", "salvo_splash = 7" })
        {
            using var t = new TempRules(("warheads.toml", baseWh + key + "\n"));
            Assert.True(seen.Add(RulesLoader.Load(t.Dir).RulesHash), key);
        }
    }
}
