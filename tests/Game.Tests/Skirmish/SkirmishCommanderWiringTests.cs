// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Data;
using Rts.Game.Host;
using Rts.Game.Sandbox;
using Rts.Game.Screens;
using Rts.Llm;

namespace Rts.Game.Tests.Skirmish;

public sealed class SkirmishCommanderWiringTests
{
    private static string RulesDir => RulesLocator.FindRulesDir(AppContext.BaseDirectory)
        ?? throw new InvalidOperationException("data/rules not found");

    private static SkirmishSettings WithCommander()
    {
        SkirmishSettings d = SkirmishSettings.Default;
        var slots = d.Slots.Select(s => s.Kind == SlotKind.Ai ? s with { Commander = true } : s).ToList();
        return d with { Slots = slots };
    }

    [Fact]
    public void No_app_settings_means_no_commanders()
    {
        using SkirmishGame g = SkirmishGame.Create(RulesDir, WithCommander());
        Assert.Empty(g.Commanders);
    }

    [Fact]
    public void Commander_slot_gets_an_offline_commander_when_the_provider_is_none_and_the_game_still_runs()
    {
        using SkirmishGame plain = SkirmishGame.Create(RulesDir, SkirmishSettings.Default);
        using SkirmishGame g = SkirmishGame.Create(RulesDir, WithCommander(), new Settings());
        ICommanderChannel c = Assert.Single(g.Commanders);
        Assert.Equal((byte)1, c.Player);
        Assert.False(c.Online);

        new SimHost(plain.Sim) { Ai = plain.Ai }.StepTicks(100);
        new SimHost(g.Sim) { Ai = g.Ai }.StepTicks(100);
        var lines = new List<CommanderLine>();
        g.Chat.CopyTo(lines);
        Assert.Single(lines, l => l.System);
        Assert.Equal(plain.Sim.ComputeStateHash(), g.Sim.ComputeStateHash());   // offline commander = byte-identical game
    }
}
