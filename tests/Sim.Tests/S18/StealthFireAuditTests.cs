// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Sim.Core;
using Rts.Sim.Map;
using Rts.Sim.Rules;
using Rts.Sim.Tests.Veterancy;
using static Rts.Sim.Tests.Combat.CombatFixture;

namespace Rts.Sim.Tests.S18;

/// <summary>S18 audit (SIM-RULES-04): firing should break stealth; with a rank-shortened reload it does not.</summary>
public sealed class StealthFireAuditTests
{
    /// <summary>A stealth rifleman of the given kill value stands until hidden, then is ordered to shoot an enemy HQ: it walks into range
    /// (which unhides it) and then stands and fires for 300 ticks, far longer than the stealth delay.
    /// Returns (shots fired, shots after which the shooter was still stealthed at the end of the same tick).</summary>
    private static (int Shots, int StillHidden) Shoot(int killValue)
    {
        SimRules r = Loaded.Sim with { Veterancy = VeterancyBonusTests.Table };
        r = r with { Infantry = r.Infantry.Select(t => t.Id == "test_rifleman" ? t with { Stealth = true } : t).ToArray() };
        var sim = new Simulation(new SimSetup(1, MapData.CreateFlat(32, 32), 2, r, Loaded.RulesHash) { AllowDebug = true });
        EntityId man = sim.SpawnInfantry(0, Inf("test_rifleman"), new CellCoord(10, 10));
        sim.Veterancy.InfantryKillValue[man.Index] = killValue;
        EntityId hq = sim.PlaceBuilding(1, Bld("test_hq"), new CellCoord(16, 9));   // out of rifle range: nothing happens until the order
        Run(sim, r.Stealth.DelayTicks + 5);
        Assert.True(sim.IsStealthedNow(ActorKind.Infantry, man.Index), "standing still should have hidden it");
        int shots = 0, hidden = 0, hp = sim.Buildings.Health[hq.Index];
        Run(sim, 1, Attack(sim, 0, ActorKind.Infantry, man, ActorKind.Building, hq));
        for (int t = 0; t < 300 && sim.Buildings.Slots.IsAlive(hq); t++)
        {
            int before = sim.CombatData.At(ActorKind.Infantry, man.Index).ReloadTicks;
            sim.Step(None);
            int after = sim.CombatData.At(ActorKind.Infantry, man.Index).ReloadTicks;
            if (after > before)   // Fire ran this tick
            {
                shots++;
                if (sim.IsStealthedNow(ActorKind.Infantry, man.Index)) hidden++;
            }
        }
        Assert.True(sim.Buildings.Health[hq.Index] < hp || !sim.Buildings.Slots.IsAlive(hq), "the rifleman should have hit the HQ");
        return (shots, hidden);
    }

    [Fact]
    public void Recruit_BreaksStealthWithEveryShot()
    {
        var (shots, hidden) = Shoot(0);
        Assert.True(shots >= 3);
        Assert.Equal(0, hidden);
    }

    // S18 audit: pins current behaviour of SIM-RULES-04; flip the assertion when fixed.
    [Theory]
    [InlineData(600)]    // rank 1
    [InlineData(3200)]   // rank 3
    public void Audit_RankedShooter_StaysStealthedWhileFiring(int killValue)
    {
        var (shots, hidden) = Shoot(killValue);
        Assert.True(shots >= 3);
        Assert.True(hidden >= shots / 2, $"{hidden} of {shots} shots left it hidden");   // hidden again 60 ticks after it stopped walking, and firing on
    }

    [Theory(Skip = "S18 SIM-RULES-04: awaiting owner decision")]
    [InlineData(600)]
    [InlineData(3200)]
    public void Desired_RankedShooter_BreaksStealthWithEveryShot(int killValue)
    {
        var (shots, hidden) = Shoot(killValue);
        Assert.True(shots >= 3);
        Assert.Equal(0, hidden);
    }
}
