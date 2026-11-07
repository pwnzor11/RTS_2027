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
    private const ulong DefaultStateHashAt1200 = 0x65689835f4187e66;   // M4-17: re-pinned because a skirmish now has the no-rebuild defeat rule on, so the production part appends the per-player clocks (no behaviour change at 1200 ticks; was 0x12388f4120596271); earlier: M7-08: re-pinned because the rules now hold naval types, so the "naval" part writes its sea labels (only that sub-hash moved, the other 23 are unchanged; was 0xc00f745f3e2e25e1); earlier: M7-02: re-pinned for the appended "shipments"/"beachhead"/"naval" parts (no behaviour change; was 0x9c95ea3a6593965f); earlier: SUP-2/3: re-pinned for the three new building rows (type indices shift; supply on or off gives the same hash at 1200 ticks; was 0x185968091d57c20f); earlier: VET-4: re-pinned for the "veterancy" part, the shooter column and [veterancy] enabled = true in data/rules/game.toml (was 0xba7eaf0f6e1f63e0); earlier: M6-02: re-pinned for the appended "rigs"/"salvage" parts and effects v2 (no behaviour change); M5-01: "directive" part

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
