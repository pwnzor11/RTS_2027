// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Data;
using Rts.Game.Host;
using Rts.Game.Sandbox;
using Rts.Game.Screens;

namespace Rts.Game.Tests.Skirmish;

public sealed class SkirmishDefaultHashTests
{
    // Pinned from the pre-M4-11 code (SkirmishGame.Create(rules, 0, 2), 1200 ticks). If this changes, a default skirmish changed.
    private const ulong DefaultMapHash = 0x3db1bf31e751d224;
    // M4-16: re-pinned (symmetric regrowth order, oriented exits/docks and the start-bias tie-breaks change a default skirmish).
    private const ulong DefaultStateHashAt1200 = 0xba7eaf0f6e1f63e0;   // M6-02: re-pinned for the appended "rigs"/"salvage" parts and effects v2 (no behaviour change); M5-01: "directive" part

    private static string RulesDir => RulesLocator.FindRulesDir(AppContext.BaseDirectory)
        ?? throw new InvalidOperationException("data/rules not found");

    private static ulong Run(SkirmishGame g, int ticks)
    {
        var host = new SimHost(g.Sim) { Ai = g.Ai };
        host.StepTicks(ticks);
        return g.Sim.ComputeStateHash();
    }

    [Fact]
    public void Default_settings_give_the_pinned_simulation()
    {
        SkirmishGame g = SkirmishGame.Create(RulesDir, SkirmishSettings.Default);
        Assert.Equal(DefaultMapHash, g.Sim.MapHash);
        Assert.Equal(DefaultStateHashAt1200, Run(g, 1200));
    }

    [Fact]
    public void Default_settings_equal_the_old_seed_and_tier_entry_point()
    {
        SkirmishGame old = SkirmishGame.Create(RulesDir, 0, 2);
        SkirmishGame now = SkirmishGame.Create(RulesDir, SkirmishSettings.Default);
        Assert.Equal(Run(old, 600), Run(now, 600));
        Assert.Equal(old.Sim.Setup.PlayerCount, now.Sim.Setup.PlayerCount);
        Assert.Null(now.Sim.Setup.Teams);
        Assert.Null(now.Sim.Setup.Factions);
        Assert.Equal(old.Sim.Setup.Seed, now.Sim.Setup.Seed);
        Assert.Equal((byte)0, now.Local);
        Assert.True(now.Curated);
    }

    [Fact]
    public void The_save_template_of_the_default_game_is_unchanged()
    {
        SkirmishGame old = SkirmishGame.Create(RulesDir, 0, 2);
        SkirmishGame now = SkirmishGame.Create(RulesDir, SkirmishSettings.Default);
        var a = old.SaveTemplate(SkirmishSettings.Default);
        var b = now.SaveTemplate(SkirmishSettings.Default);
        Assert.Equal(a.MapId, b.MapId);
        Assert.Equal(a.LocalPlayer, b.LocalPlayer);
        Assert.Equal(a.Ai, b.Ai);
        Assert.Equal("file:" + SkirmishGame.CuratedMapFile, b.MapId);
    }
}
