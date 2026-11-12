// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Sim.Core;
using Rts.Sim.Rules;
using Rts.Sim.Tests.M3.Burrow;

namespace Rts.Sim.Tests.S18;

/// <summary>S18 audit (SIM-RULES-02): the Hatch to Bore Head morph is paid from Cash only, never from Stored.</summary>
public sealed class MorphCostAuditTests
{
    /// <summary>BurrowWorld with a "house" that stores 2000 credits, a Hatch for player 0, and the given purse.</summary>
    private static (Simulation Sim, EntityId Hatch) Scene(int cash, int stored)
    {
        SimRules rules = BurrowWorld.Rules();
        var buildings = rules.Buildings.ToArray();
        buildings[BurrowWorld.House] = buildings[BurrowWorld.House] with { Storage = 2000 };
        var sim = new Simulation(BurrowWorld.Setup() with { Rules = rules with { Buildings = buildings } });
        Assert.False(sim.PlaceBuilding(0, BurrowWorld.House, new CellCoord(4, 4)).IsNone);
        EntityId hatch = sim.PlaceBuilding(0, BurrowWorld.Hatch, new CellCoord(10, 10));
        sim.Step(Array.Empty<Command>());   // economy recomputes the storage cap
        sim.Players.Cash[0] = cash; sim.Players.Stored[0] = stored;
        return (sim, hatch);
    }

    private static Command Upgrade(Simulation sim, EntityId b) => BurrowWorld.Cmd(sim, CommandKind.Upgrade, b, default, BurrowWorld.BoreHead);

    [Fact]
    public void Morph_IsAccepted_WhenTheMoneyIsInStoredAndCashIsShort()
    {
        var (sim, hatch) = Scene(cash: 299, stored: 2000);   // the morph costs 400 - 100 = 300
        sim.Step(new[] { Upgrade(sim, hatch) });
        Assert.Equal(BurrowWorld.BoreHead + 1, sim.Tunnels.BuildingMorphType[hatch.Index]);
    }

    [Fact]
    public void Morph_IsCountedAsSpent()
    {
        var (sim, hatch) = Scene(cash: 300, stored: 2000);
        int spentBefore = sim.Stats.CreditsSpent[0];
        sim.Step(new[] { Upgrade(sim, hatch) });
        Assert.Equal(spentBefore + 300, sim.Stats.CreditsSpent[0]);
    }

    [Fact]
    public void Morph_IsRefused_WhenFundsAreShort()
    {
        var (sim, hatch) = Scene(cash: 100, stored: 199);
        sim.Step(new[] { Upgrade(sim, hatch) });
        Assert.Equal(0, sim.Tunnels.BuildingMorphType[hatch.Index]);
        Assert.Equal(299, sim.Funds(0));
    }

    [Fact]
    public void Desired_Morph_IsPaidFromFunds_CashFirstThenStored()
    {
        var (sim, hatch) = Scene(cash: 100, stored: 2000);
        sim.Step(new[] { Upgrade(sim, hatch) });
        Assert.Equal(BurrowWorld.BoreHead + 1, sim.Tunnels.BuildingMorphType[hatch.Index]);
        Assert.Equal(0, sim.Players.Cash[0]);
        Assert.Equal(1800, sim.Players.Stored[0]);
    }
}
