// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Game.Controls;
using Rts.Game.Host;
using Rts.Game.Saves;
using Rts.Game.Screens;
using Rts.Game.Tests.Saves;
using Rts.Sim.Core;
using Rts.Sim.Rules;

namespace Rts.Game.Tests.S19;

/// <summary>S19 D-205 (M13-15): the per-slot skirmish options (handicap, starting credits, small army) and the stance hotkeys.</summary>
public sealed class SlotOptionsTests
{
    private static SkirmishSettings With(Func<SkirmishSlot, SkirmishSlot> change)
    {
        var s = new SkirmishSettings(77, 3, 2, Faction.Directorate, Faction.Authority);
        var slots = s.Slots.ToArray();
        slots[0] = change(slots[0]);
        return s with { Slots = slots };
    }

    [Fact]
    public void Defaults_ProduceNoSetupOptions_SoTheOldGameIsBuilt()
    {
        var s = new SkirmishSettings(77, 3, 2);
        Assert.Null(s.Handicaps);
        Assert.Null(s.StartingCash);
        Assert.Null(s.SmallArmies);
    }

    [Fact]
    public void ChosenOptions_FlowToTheSetupArrays()
    {
        SkirmishSettings s = With(x => x with { Handicap = 50, CreditsPreset = 2, SmallArmy = true });
        Assert.Equal(50, s.Handicaps![0]);
        Assert.Equal(100, s.Handicaps![1]);
        Assert.Equal(SlotOptions.CreditsPresets[2], s.StartingCash![0]);
        Assert.Equal(-1, s.StartingCash![1]);
        Assert.True(s.SmallArmies![0]);
        Assert.False(s.SmallArmies![1]);
    }

    [Fact]
    public void Steps_WrapRound()
    {
        Assert.Equal(75, SlotOptions.NextHandicap(50));
        Assert.Equal(100, SlotOptions.NextHandicap(75));
        Assert.Equal(50, SlotOptions.NextHandicap(100));
        Assert.Equal(0, SlotOptions.NextCredits(SlotOptions.CreditsPresets.Count - 1));
        Assert.Equal(-1, SlotOptions.CreditsFor(0));
    }

    [Fact]
    public void ASkirmishGame_StartsWithTheChosenCredits_AndTheHandicap()
    {
        SkirmishSettings s = With(x => x with { Handicap = 75, CreditsPreset = 3 });
        using var t = new SaveTestSupport();
        using Rts.Game.Sandbox.SkirmishGame game = Rts.Game.Sandbox.SkirmishGame.Create(t.RulesDir, s);
        Rts.Sim.Simulation sim = game.Sim;
        Assert.Equal(SlotOptions.CreditsPresets[3], sim.Players.Cash[0]);
        Assert.Equal(75, sim.Setup.HandicapOf(0));
    }

    [Fact]
    public void TheSaveHeader_RoundTripsTheSlotOptions()
    {
        using var t = new SaveTestSupport();
        (SimHost host, _) = t.NewSkirmish();
        GameSaveHeader h = new()
        {
            Mode = SaveMode.Skirmish, MapId = "generated:64:77", MapName = "Gen", RulesHash = 1, Tick = 5, Build = "x",
            Skirmish = With(x => x with { Handicap = 75, CreditsPreset = 1, SmallArmy = true }),
        };
        var ms = new MemoryStream();
        GameSaveFile.Write(ms, h, host.Sim, Array.Empty<(string, string)>());
        GameSaveHeader back = GameSaveFile.ReadHeader(new MemoryStream(ms.ToArray()));
        Assert.Equal(h.Skirmish, back.Skirmish);
    }

    [Fact]
    public void TheStanceHotkeys_AreDistinctAndFree()
    {
        var keys = new[] { GameAction.StanceAggressive, GameAction.StanceDefensive, GameAction.StanceHoldFire }.Select(a => KeyBindings.Default.Get(a)).ToArray();
        Assert.Equal(3, keys.Distinct().Count());
        Assert.Empty(KeyBindings.Default.Conflicts());
    }
}
