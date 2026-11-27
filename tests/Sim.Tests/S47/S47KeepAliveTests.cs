// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Sim.Core;
using static Rts.Sim.Tests.Production.ProductionFixture;

namespace Rts.Sim.Tests.S47;

/// <summary>S47 C-06: the per-player KeepAlive flag holds a player up against the no-buildings defeat rule; saved and hashed.</summary>
public class S47KeepAliveTests
{
    private static Simulation TwoBases() =>
        NewSim(new[] { ("test_hq", 4, 4) }, others: new[] { ((byte)1, "test_hq", 10, 10) });

    private static void KillHq(Simulation sim, int x, int y)
    {
        sim.Buildings.Health[BuildingOf(sim, x, y).Index] = 0;
        sim.Step(None);
    }

    [Fact]
    public void KeepAlive_player_without_assets_is_not_defeated_and_the_game_goes_on()
    {
        var sim = TwoBases();
        sim.Players.KeepAlive[1] = true;
        KillHq(sim, 10, 10);
        for (int i = 0; i < 5; i++) sim.Step(None);
        Assert.False(sim.Players.Defeated[1]);
        Assert.False(sim.GameOver);
    }

    [Fact]
    public void Turning_it_off_defeats_the_player_on_the_next_check()
    {
        var sim = TwoBases();
        sim.Players.KeepAlive[1] = true;
        KillHq(sim, 10, 10);
        Assert.False(sim.Players.Defeated[1]);
        sim.Players.KeepAlive[1] = false;
        sim.Step(None);
        Assert.True(sim.Players.Defeated[1]);
        Assert.True(sim.GameOver);
    }

    [Fact]
    public void Without_the_flag_the_player_is_defeated_as_before()
    {
        var sim = TwoBases();
        KillHq(sim, 10, 10);
        Assert.True(sim.Players.Defeated[1]);
    }

    [Fact]
    public void Flag_survives_save_and_load_and_changes_the_hash()
    {
        var sim = TwoBases();
        ulong off = sim.ComputeStateHash();
        sim.Players.KeepAlive[1] = true;
        Assert.NotEqual(off, sim.ComputeStateHash());
        sim.Players.KeepAlive[1] = false;
        Assert.Equal(off, sim.ComputeStateHash());   // default false keeps the old bytes
        sim.Players.KeepAlive[1] = true;
        sim.Step(None);

        var ms = new MemoryStream();
        sim.Save(ms);
        ms.Position = 0;
        var loaded = Simulation.Load(ms, sim.Setup);
        Assert.True(loaded.Players.KeepAlive[1]);
        Assert.False(loaded.Players.KeepAlive[0]);
        Assert.Equal(sim.ComputeStateHash(), loaded.ComputeStateHash());
    }
}
