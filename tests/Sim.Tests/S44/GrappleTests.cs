// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Sim.Core;
using Rts.Sim.Rules;
using Rts.Sim.Tests.Determinism;

namespace Rts.Sim.Tests.S44;

/// <summary>S44 M-06: with the Grapple Hooks upgrade a Hook Runner beside an enemy wall opens it for a while; the plunder x1.5 comes before the cap.</summary>
public sealed class GrappleTests
{
    private const int Breach = 60, Cooldown = 100;
    // infantry: 0 raider, 1 plain; buildings: 0 hq, 1 wall (1x1), 2 silo
    private const int Raider = 0, Plain = 1, Hq = 0, Wall = 1, Silo = 2;
    private const int WallX = 20;

    private static SimSetup Setup(int plunderCap = 1000, bool upgradeData = true)
    {
        var infantry = new[]
        {
            ScriptedGame.Infantry("a_raider", 20, 50) with { Raider = true },
            ScriptedGame.Infantry("b_plain", 20, 50),
        };
        var buildings = new[]
        {
            ScriptedGame.Building("a_hq", 3, 3, 500),
            ScriptedGame.Building("b_wall", 1, 1, 400) with { Wall = true },
            ScriptedGame.Building("c_silo", 2, 2, 300) with { Storage = 20000 },
        };
        var rules = ScriptedGame.RulesWith(new[] { ScriptedGame.Vehicle("a_dummy", 10, 100) }, infantry) with
        {
            Buildings = buildings,
            Economy = EconomyRules.Default with { PlunderCap = plunderCap },
            Upgrades = new[]
            {
                new UpgradeType("grapple", Faction.Authority, 0, 0, Array.Empty<string>(), "none")
                { PlunderPermille = 1500, BreachTicks = upgradeData ? Breach : 0, BreachCooldownTicks = Cooldown },
            },
        };
        return new SimSetup(1, Rts.Sim.Map.MapData.CreateFlat(64, 64), 2, rules, 0x4406UL) { AllowDebug = true };
    }

    private static Simulation NewSim(bool upgrade, int type, out EntityId unit, CellCoord at = default, int plunderCap = 1000)
    {
        var sim = new Simulation(Setup(plunderCap));
        sim.PlaceBuilding(0, Hq, new CellCoord(4, 30));
        sim.PlaceBuilding(1, Hq, new CellCoord(50, 30));
        for (int y = 0; y < 64; y++) sim.PlaceBuilding(1, Wall, new CellCoord(WallX, y));
        unit = sim.SpawnInfantry(0, type, at.X == 0 ? new CellCoord(WallX - 2, 30) : at);
        if (upgrade) sim.Players.Upgrades[0] |= 1UL;
        return sim;
    }

    private static void Run(Simulation sim, int ticks, params Command[] first)
    {
        sim.Step(first);
        for (int t = 1; t < ticks; t++) sim.Step(Array.Empty<Command>());
    }

    private static Command Move(Simulation sim, EntityId id, int x, int y) =>
        new(sim.Tick, 0, 1, CommandKind.Move, id, default, new CellCoord(x, y), (int)ActorKind.Infantry);

    [Fact]
    public void RaiderBesideAnEnemyWall_OpensIt_WithTheUpgrade()
    {
        Simulation sim = NewSim(true, Raider, out _, new CellCoord(WallX - 1, 30));
        int wall = sim.Map.BuildingAt(new CellCoord(WallX, 30)).Index;
        Run(sim, 2);
        Assert.True(sim.Effects.BuildingBreach[wall] > 0);
        Assert.Equal(0, sim.Effects.BuildingBreachOwner[wall]);
        Assert.True(sim.BreachOpenFor(wall, 0));
        Assert.False(sim.BreachOpenFor(wall, 1), "the wall's own team is not the breacher's team");
    }

    [Fact]
    public void NoUpgrade_NoBreach()
    {
        Simulation sim = NewSim(false, Raider, out _, new CellCoord(WallX - 1, 30));
        Run(sim, 5);
        for (int y = 0; y < 64; y++) Assert.Equal(0, sim.Effects.BuildingBreach[sim.Map.BuildingAt(new CellCoord(WallX, y)).Index]);
    }

    [Fact]
    public void NonRaider_NeverBreaches()
    {
        Simulation sim = NewSim(true, Plain, out _, new CellCoord(WallX - 1, 30));
        Run(sim, 5);
        Assert.False(sim.Effects.AnyTether);
    }

    [Fact]
    public void BreachCloses_ThenCoolsDown_ThenCanOpenAgain()
    {
        Simulation sim = NewSim(true, Raider, out _, new CellCoord(WallX - 1, 30));
        int wall = sim.Map.BuildingAt(new CellCoord(WallX, 30)).Index;
        Run(sim, 2);
        Assert.True(sim.Effects.BuildingBreach[wall] > 0);
        Run(sim, Breach + 2);
        Assert.True(sim.Effects.BuildingBreach[wall] <= 0, "closed");
        Assert.False(sim.BreachOpenFor(wall, 0));
        Run(sim, Cooldown / 2);
        Assert.True(sim.Effects.BuildingBreach[wall] < 0, "still cooling down while the runner stands beside it");
        Run(sim, Cooldown);
        Assert.True(sim.Effects.BuildingBreach[wall] > 0, "opened again after the cooldown");
    }

    [Fact]
    public void Breach_OpensForTheBreachersTeam_NotForTheWallsOwner()
    {
        Simulation sim = NewSim(true, Raider, out var runner, new CellCoord(WallX - 1, 30));
        EntityId plain = sim.SpawnInfantry(0, Plain, new CellCoord(WallX - 2, 30));
        EntityId enemy = sim.SpawnInfantry(1, Plain, new CellCoord(WallX + 2, 33));
        Run(sim, 2);
        Run(sim, 1, Move(sim, runner, WallX + 4, 30) with { Sequence = 2 }, Move(sim, plain, WallX + 4, 31) with { Sequence = 3 }, Move(sim, enemy, WallX - 5, 33) with { Sequence = 4, Player = 1 });
        Run(sim, 50);
        Assert.True(sim.Infantry.SlotCell[runner.Index].X > WallX, "the runner went through the gap");
        Assert.True(sim.Infantry.SlotCell[plain.Index].X > WallX, "a team-mate follows through the gap");
        Assert.True(sim.Infantry.SlotCell[enemy.Index].X > WallX, "the wall's own team is not let through");
    }

    [Fact]
    public void BreachState_IsHashedAndSurvivesSaveLoad()
    {
        Simulation sim = NewSim(true, Raider, out _, new CellCoord(WallX - 1, 30));
        Run(sim, 5);
        var ms = new MemoryStream();
        sim.Save(ms); ms.Position = 0;
        Simulation loaded = Simulation.Load(ms, sim.Setup);
        int wall = sim.Map.BuildingAt(new CellCoord(WallX, 30)).Index;
        Assert.Equal(sim.Effects.BuildingBreach[wall], loaded.Effects.BuildingBreach[wall]);
        sim.ComputeHashes(out ulong a); loaded.ComputeHashes(out ulong b);
        Assert.Equal(a, b);
        for (int t = 0; t < 200; t++) { sim.Step(Array.Empty<Command>()); loaded.Step(Array.Empty<Command>()); }
        sim.ComputeHashes(out ulong a2); loaded.ComputeHashes(out ulong b2);
        Assert.Equal(a2, b2);
        sim.Effects.BuildingBreach[wall]++;
        sim.ComputeHashes(out ulong c);
        Assert.NotEqual(a2, c);
    }

    // ---- SIMECON-09 / M-06: the x1.5 bonus comes BEFORE the cap ----------------------------------------------------

    private static int Plunder(bool upgrade, int stored, int cap)
    {
        var sim = new Simulation(Setup(cap));
        sim.PlaceBuilding(0, Hq, new CellCoord(4, 30));
        EntityId silo = sim.PlaceBuilding(1, Silo, new CellCoord(30, 10));
        EntityId unit = sim.SpawnInfantry(0, Raider, new CellCoord(24, 10));
        sim.Players.Stored[1] = stored;
        if (upgrade) sim.Players.Upgrades[0] |= 1UL;
        int cash = sim.Players.Cash[0];
        sim.Step(new[] { new Command(sim.Tick, 0, 1, CommandKind.Enter, unit, silo, default, (int)ActorKind.Infantry | ((int)ActorKind.Building << 8)) });
        for (int t = 0; t < 1500 && sim.Players.Cash[0] == cash; t++) sim.Step(Array.Empty<Command>());
        return sim.Players.Cash[0] - cash;
    }

    [Fact]
    public void Plunder_BonusIsAppliedBeforeTheCap()
    {
        // 20% of 10000 = 2000; x1.5 = 3000; the cap (1000) is the last word. Capping first would give 1000 x 1.5 = 1500.
        Assert.Equal(1000, Plunder(true, 10000, 1000));
        Assert.Equal(1000, Plunder(false, 10000, 1000));
    }

    [Fact]
    public void Plunder_BonusStillRaisesAnUncappedYield()
    {
        Assert.Equal(300, Plunder(true, 1000, 1000));   // 200 x 1.5
        Assert.Equal(200, Plunder(false, 1000, 1000));
        Assert.Equal(900, Plunder(true, 3000, 1000));   // 600 x 1.5 = 900, under the cap
    }
}
