// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Sim.Core;
using Xunit.Abstractions;

namespace Rts.Sim.Tests.Balance;

/// <summary>
/// M4-16 scenario tests with the shipped <c>data/rules</c> numbers. Goals (owner, session 6): a filled Blockhouse / Redoubt
/// (building + five riflemen / conscripts) beats an equal-cost infantry squad; loses to a counter (Collapse Charge, Ashthrower,
/// Seismic Mortar through <c>garrison_damage_percent</c>) at about 1.25x its cost; and is not decisive alone, so an equal-cost
/// vehicle group still takes it down.
/// </summary>
public sealed class GarrisonBalanceTests
{
    private readonly ITestOutputHelper _out;
    public GarrisonBalanceTests(ITestOutputHelper output) => _out = output;

    private sealed record Result(bool BuildingAlive, int Occupants, int AttackersAlive, int ClearedTick, int Cost, int AttackCost);

    private Result Fight(byte defender, string building, string soldier, bool vehicles, string attacker, int count, int maxTicks = 5000)
    {
        var a = new BalanceArena();
        var (b, men) = a.PlaceGarrison(defender, building, soldier, new CellCoord(30, 48), 5);
        byte atk = (byte)(1 - defender);
        var units = vehicles ? a.SpawnVehicles(atk, attacker, count, new CellCoord(50, 46)) : a.SpawnInfantry(atk, attacker, count, new CellCoord(50, 46));
        int attackCost = a.AliveValue(units), cost = a.BuildingCost(building) + 5 * a.InfCost(soldier);
        a.FillGarrison(defender, b, men);
        a.Run(1, a.AttackBuilding(atk, units, b));
        int cleared = -1;
        for (int t = 0; t < maxTicks; t++)
        {
            a.Sim.Step(Array.Empty<Command>());
            bool alive = a.Alive(ActorKind.Building, b);
            if (cleared < 0 && (!alive || a.Sim.OccupantsIn(b) == 0)) cleared = t;
            if (!alive || a.AliveCount(units) == 0) break;
        }
        bool up = a.Alive(ActorKind.Building, b);
        var r = new Result(up, up ? a.Sim.OccupantsIn(b) : 0, a.AliveCount(units), cleared, cost, attackCost);
        _out.WriteLine($"{building} vs {count} {attacker}: cost {cost} vs {attackCost}; building {(up ? "stands" : "dead")}, occupants {r.Occupants}, attackers left {r.AttackersAlive}/{units.Count}, cleared at tick {cleared}");
        return r;
    }

    [Fact]
    public void BlockhouseBeatsEqualCostConscripts()
    {
        Result r = Fight(0, "auth_blockhouse", "auth_rifleman", false, "dir_conscript", 10);
        Assert.True(r.BuildingAlive);
        Assert.Equal(5, r.Occupants);
        Assert.Equal(0, r.AttackersAlive);
    }

    [Fact]
    public void BlockhouseBeatsEqualCostAshthrowers()
    {
        Result r = Fight(0, "auth_blockhouse", "auth_rifleman", false, "dir_ashthrower", 4);
        Assert.True(r.BuildingAlive);
        Assert.Equal(0, r.AttackersAlive);
    }

    [Fact]
    public void RedoubtBeatsEqualCostRiflemen()
    {
        Result r = Fight(1, "dir_redoubt", "dir_conscript", false, "auth_rifleman", 7);
        Assert.True(r.BuildingAlive);
        Assert.Equal(5, r.Occupants);
        Assert.Equal(0, r.AttackersAlive);
    }

    [Fact]
    public void BlockhouseLosesToSappersAtAboutOnePointTwoFiveTimesTheCost()
    {
        Result r = Fight(0, "auth_blockhouse", "auth_rifleman", false, "dir_sapper", 5);   // 1 250 vs 800
        Assert.False(r.BuildingAlive);
        Assert.True(r.AttackersAlive > 0);
        Assert.InRange(r.ClearedTick, 1, 800);
    }

    [Fact]
    public void SeismicMortarClearsAGarrisonLongBeforeItKillsTheBuilding()
    {
        Result r = Fight(1, "dir_redoubt", "dir_conscript", true, "auth_seismic_mortar", 1, 8000);
        Assert.False(r.BuildingAlive);
        Assert.Equal(1, r.AttackersAlive);
        Assert.InRange(r.ClearedTick, 1, 1000);   // occupants are shelled out through garrison_damage_percent
    }

    [Fact]
    public void GarrisonAloneIsNotDecisive_AnEqualCostVehicleGroupTakesItDown()
    {
        Result tank = Fight(1, "dir_redoubt", "dir_conscript", true, "auth_tank", 1);        // 800 vs 700
        Assert.False(tank.BuildingAlive);
        Assert.Equal(1, tank.AttackersAlive);
        Result truck = Fight(0, "auth_blockhouse", "auth_rifleman", true, "dir_rocket_truck", 1);   // 800 vs 800
        Assert.False(truck.BuildingAlive);
        Assert.Equal(1, truck.AttackersAlive);
    }
}
