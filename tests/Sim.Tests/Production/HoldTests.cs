// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Sim.Core;
using Rts.Sim.Rules;
using static Rts.Sim.Tests.Production.ProductionFixture;

namespace Rts.Sim.Tests.Production;

/// <summary>S15 (M11-10i): the production hold. A held front item makes no progress and spends nothing; resume continues; cancel refunds.</summary>
public class HoldTests
{
    private static readonly ProductionCategory Infantry = ProductionCategory.Infantry;
    private static readonly ProductionCategory Structure = ProductionCategory.Structure;

    private static ushort _seq = 5000;

    internal static Command Hold(Simulation sim, ProductionCategory cat, bool hold, byte player = 0) =>
        new(sim.Tick, player, _seq++, CommandKind.Hold, default, default, default, (int)cat, hold ? 1 : 0);

    [Fact]
    public void CommandKind_Hold_IsAppendedAt25()
    {
        Assert.Equal(25, (int)CommandKind.Hold);
        Assert.Equal(24, (int)CommandKind.Pack);
    }

    [Fact]
    public void Hold_StopsProgressAndSpending_ResumeContinues()
    {
        var sim = Base(cash: 1000);
        Step(sim, Build(sim, Infantry, Inf("test_rifleman")));
        Run(sim, 5);
        int spent = sim.Production.CreditsSpent(0, Infantry), cash = sim.Players.Cash[0];
        int progress = sim.HudFor(0).Queues.Single(q => q.Category == Infantry).Items[0].ProgressPermille;
        Assert.True(spent > 0);

        Step(sim, Hold(sim, Infantry, true));
        Assert.True(sim.Production.IsHeld(0, Infantry));
        Assert.True(sim.HudFor(0).Queues.Single(q => q.Category == Infantry).Items[0].Held);
        Run(sim, 200);   // far longer than the build time
        Assert.Equal(spent, sim.Production.CreditsSpent(0, Infantry));
        Assert.Equal(cash, sim.Players.Cash[0]);
        Assert.Equal(progress, sim.HudFor(0).Queues.Single(q => q.Category == Infantry).Items[0].ProgressPermille);
        Assert.Equal(1, sim.Production.QueueLength(0, Infantry));
        Assert.Equal(0, sim.Infantry.Slots.Count);

        Step(sim, Hold(sim, Infantry, false));
        Assert.False(sim.Production.IsHeld(0, Infantry));
        RunUntil(sim, () => sim.Infantry.Slots.Count == 1);
        Assert.Equal(900, sim.Players.Cash[0]);   // exactly the cost, once
    }

    [Fact]
    public void Cancel_OfAHeldItem_RefundsExactlyWhatWasSpent_AndClearsTheHold()
    {
        var sim = Base(cash: 1000);
        Step(sim, Build(sim, Infantry, Inf("test_rifleman")), Build(sim, Infantry, Inf("test_engineer")));
        Run(sim, 7);
        Step(sim, Hold(sim, Infantry, true));
        Assert.True(sim.Players.Cash[0] < 1000);
        Step(sim, Cancel(sim, Infantry, 0));
        // The refund is exact: everything missing from the purse is what the next item (engineer) has already paid in the cancel tick.
        Assert.Equal(1000, sim.Players.Cash[0] + sim.Production.CreditsSpent(0, Infantry));
        Assert.False(sim.Production.IsHeld(0, Infantry));   // the next item is not born held
        Assert.Equal(1, sim.Production.QueueLength(0, Infantry));
        int before = sim.Production.CreditsSpent(0, Infantry);
        Run(sim, 5);
        Assert.True(sim.Production.CreditsSpent(0, Infantry) > before);   // the engineer keeps being paid for
    }

    [Fact]
    public void Hold_OnAnEmptyLane_IsIgnored_AndHoldIsPerLane()
    {
        var sim = Base(cash: 1000);
        Step(sim, Hold(sim, Infantry, true));
        Assert.False(sim.Production.IsHeld(0, Infantry));
        Step(sim, Build(sim, Infantry, Inf("test_rifleman")), Build(sim, ProductionCategory.Vehicle, Veh("test_tank")));
        Step(sim, Hold(sim, Infantry, true));
        int infSpent = sim.Production.CreditsSpent(0, Infantry), vehSpent = sim.Production.CreditsSpent(0, ProductionCategory.Vehicle);
        Run(sim, 10);
        Assert.True(sim.Production.CreditsSpent(0, ProductionCategory.Vehicle) > vehSpent);   // the vehicle lane still runs
        Assert.Equal(infSpent, sim.Production.CreditsSpent(0, Infantry));
    }

    [Fact]
    public void Hold_WorksOnAStructureLane()
    {
        var sim = Base(cash: 1000);
        Step(sim, Build(sim, Structure, Bld("test_power")));
        Run(sim, 4);
        Step(sim, Hold(sim, Structure, true));
        int spent = sim.Production.CreditsSpent(0, Structure);
        Run(sim, 300);
        Assert.Equal(spent, sim.Production.CreditsSpent(0, Structure));
        Assert.Equal(-1, sim.Production.ReadyType(0, Structure));
        Step(sim, Hold(sim, Structure, false));
        RunUntil(sim, () => sim.Production.ReadyType(0, Structure) >= 0);
    }

    [Fact]
    public void SaveLoad_MidHold_KeepsTheHold_AndContinuesIdentically()
    {
        var sim = Base(cash: 1000);
        Step(sim, Build(sim, Infantry, Inf("test_rifleman")));
        Run(sim, 6);
        Step(sim, Hold(sim, Infantry, true));
        var ms = new MemoryStream(); sim.Save(ms); ms.Position = 0;
        var loaded = Simulation.Load(ms, sim.Setup);
        Assert.True(loaded.Production.IsHeld(0, Infantry));
        Assert.Equal(sim.ComputeStateHash(), loaded.ComputeStateHash());
        for (int t = 0; t < 120; t++)
        {
            Command[] cmds = t == 50 ? new[] { Hold(sim, Infantry, false) } : None;
            sim.Step(cmds); loaded.Step(cmds);
            Assert.Equal(sim.ComputeStateHash(), loaded.ComputeStateHash());
        }
        Assert.Equal(1, sim.Infantry.Slots.Count);
        Assert.Equal(1, loaded.Infantry.Slots.Count);
    }

    [Fact]
    public void Held_IsHashed()
    {
        Simulation Make(bool hold)
        {
            var s = Base(cash: 1000);
            Step(s, Build(s, Infantry, Inf("test_rifleman")));
            Run(s, 3);
            if (hold) Step(s, Hold(s, Infantry, true)); else Run(s, 1);
            // Same tick, same spending: only the flag differs.
            return s;
        }
        var a = Make(true); var b = Make(false);
        Assert.NotEqual(a.ComputeSubHashes().Single(h => h.Part == "production"), b.ComputeSubHashes().Single(h => h.Part == "production"));
    }

    [Fact]
    public void SameCommands_GiveTheSameHash_Twice()
    {
        ulong Play()
        {
            var s = Base(cash: 1000);
            for (int t = 0; t < 150; t++)
            {
                Command[] c = t == 0 ? new[] { Build(s, Infantry, Inf("test_rifleman")) }
                    : t == 6 ? new[] { Hold(s, Infantry, true) }
                    : t == 60 ? new[] { Hold(s, Infantry, false) }
                    : None;
                s.Step(c);
            }
            return s.ComputeStateHash();
        }
        Assert.Equal(Play(), Play());
    }
}
