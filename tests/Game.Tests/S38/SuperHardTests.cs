// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Game.Host;
using Rts.Game.Lan.Lobby;
using Rts.Game.Saves;
using Rts.Game.Screens;
using Rts.Game.Tests.Saves;
using Rts.Sim.Rules;

namespace Rts.Game.Tests.S38;

/// <summary>S38 (M5-15): the AI income edge (handicap 125/150), the "Super hard (LLM)" preset, and 150 surviving saves and the LAN lobby.</summary>
public sealed class SuperHardTests
{
    private static readonly string[] Tiers = { "Recruit", "Regular", "Veteran", "Hardened", "Elite" };

    [Fact]
    public void AiSlots_CycleThroughTheEdgeSteps_AndHumanSlotsDoNot()
    {
        Assert.Equal(new[] { 50, 75, 100 }, SlotOptions.HandicapSteps);
        Assert.Equal(new[] { 50, 75, 100, 125, 150 }, SlotOptions.AiHandicapSteps);
        Assert.Equal(75, SlotOptions.NextHandicap(50, ai: true));
        Assert.Equal(125, SlotOptions.NextHandicap(100, ai: true));
        Assert.Equal(150, SlotOptions.NextHandicap(125, ai: true));
        Assert.Equal(50, SlotOptions.NextHandicap(150, ai: true));
        Assert.Equal(50, SlotOptions.NextHandicap(100, ai: false));
        Assert.Equal(50, SlotOptions.NextHandicap(100));
        Assert.Equal(50, SlotOptions.NextHandicap(125, ai: false));   // not a human step: starts again at the first
        Assert.Equal(100, SlotOptions.ForHuman(150));
        Assert.Equal(75, SlotOptions.ForHuman(75));
    }

    [Fact]
    public void Labels_SayEdgeAboveHundred_AndKeepTheOldWordsBelow()
    {
        Assert.Equal("Edge +25%", SlotOptions.HandicapLabel(125));
        Assert.Equal("Edge +50%", SlotOptions.HandicapLabel(150));
        Assert.Equal("Handicap 75%", SlotOptions.HandicapLabel(75));
        Assert.Equal("Handicap none", SlotOptions.HandicapLabel(100));
        Assert.Equal(150, SlotOptions.SuperHardEdge);
    }

    [Fact]
    public void ThePreset_SetsTierCommanderAndEdge_OnAnAiSlotOnly()
    {
        var ai = new SkirmishSlot(SlotKind.Ai, Faction.Directorate, 2, 1, 1);
        SkirmishSlot s = SkirmishSetupScreen.ApplySuperHard(ai);
        Assert.Equal(5, s.AiTier);
        Assert.True(s.Commander);
        Assert.Equal(SlotOptions.SuperHardEdge, s.Handicap);
        Assert.Equal(Faction.Directorate, s.Faction);
        Assert.True(SkirmishSetupScreen.IsSuperHard(s));
        Assert.False(SkirmishSetupScreen.IsSuperHard(ai));

        var human = new SkirmishSlot(SlotKind.Human, Faction.Authority, 2, 0, 0);
        Assert.Equal(human, SkirmishSetupScreen.ApplySuperHard(human));
        var closed = new SkirmishSlot(SlotKind.Closed, Faction.Authority, 2, 0, 0);
        Assert.Equal(closed, SkirmishSetupScreen.ApplySuperHard(closed));
    }

    [Fact]
    public void ThePreset_StillTriggersTheNoKeyNotice_OnlyWithoutAKey()
    {
        var ai = new SkirmishSlot(SlotKind.Ai, Faction.Authority, 2, 1, 1);
        SkirmishSlot after = SkirmishSetupScreen.ApplySuperHard(ai);
        Assert.True(SkirmishSetupScreen.NeedsNoKeyNotice(ai, after, hasKey: false));
        Assert.False(SkirmishSetupScreen.NeedsNoKeyNotice(ai, after, hasKey: true));
        Assert.False(SkirmishSetupScreen.NeedsNoKeyNotice(after, SkirmishSetupScreen.ApplySuperHard(after), hasKey: false));   // already a commander slot
    }

    [Fact]
    public void TheRowsShowLlmTierAndEdge_AndTheTextLivesInSetupText()
    {
        SkirmishSlot s = SkirmishSetupScreen.ApplySuperHard(new SkirmishSlot(SlotKind.Ai, Faction.Authority, 2, 1, 1));
        Assert.Equal("2 AI LLM - Elite +50% Authority T2", SkirmishSetupScreen.SlotLabel(s, 1, Tiers));
        Assert.Equal("2 AI Elite Authority T2", SkirmishSetupScreen.SlotLabel(s with { Commander = false, Handicap = 100 }, 1, Tiers));
        Assert.StartsWith("2 Edge +50%", SkirmishSetupScreen.OptionsLabel(s, 1));
        Assert.Equal("Super hard (LLM)", SetupText.SuperHardLabel);
        Assert.Equal("Elite AI led by the LLM commander, with +50% income and build speed", SetupText.SuperHardHint);
        Assert.Contains("Super", SetupText.SuperHardOptionsHint);
        // the setup panel is sized for the widest slot text, edge included
        Assert.Contains(SkirmishSetupScreen.WidestSlotTexts(Tiers), t => t.Contains(" +50% "));
    }

    [Fact]
    public void HandicapAbove100_Validates_AndOutOfRangeDoesNot()
    {
        SkirmishSettings Make(int h)
        {
            var st = new SkirmishSettings(77, 3, 2, Faction.Directorate, Faction.Authority);
            var slots = st.Slots.ToArray();
            slots[1] = slots[1] with { Handicap = h };
            return st with { Slots = slots };
        }
        Assert.Empty(Make(150).Validate());
        Assert.NotEmpty(Make(201).Validate());
        Assert.NotEmpty(Make(9).Validate());
        Assert.Equal(150, Make(150).Handicaps![1]);
    }

    [Fact]
    public void TheSaveHeader_RoundTrips150()
    {
        using var t = new SaveTestSupport();
        (SimHost host, _) = t.NewSkirmish();
        var st = new SkirmishSettings(77, 3, 2, Faction.Directorate, Faction.Authority);
        var slots = st.Slots.ToArray();
        slots[1] = SkirmishSetupScreen.ApplySuperHard(slots[1] with { Kind = SlotKind.Ai });
        slots[0] = slots[0] with { Handicap = 125 };   // the header tolerates any 10..200
        GameSaveHeader h = new()
        {
            Mode = SaveMode.Skirmish, MapId = "generated:64:77", MapName = "Gen", RulesHash = 1, Tick = 5, Build = "x",
            Skirmish = st with { Slots = slots },
        };
        var ms = new MemoryStream();
        GameSaveFile.Write(ms, h, host.Sim, Array.Empty<(string, string)>());
        GameSaveHeader back = GameSaveFile.ReadHeader(new MemoryStream(ms.ToArray()));
        Assert.Equal(h.Skirmish, back.Skirmish);
        Assert.Equal(150, back.Skirmish!.Slots[1].Handicap);
        Assert.Equal(125, back.Skirmish.Slots[0].Handicap);
    }

    [Fact]
    public void TheLanLobby_RoundTrips150_AndRefusesOutOfRange()
    {
        var d = new LobbyData("Hosty", LobbyMap.Curated,
            new[]
            {
                new LobbySlotState(new SkirmishSlot(SlotKind.Human, Faction.Authority, 2, 0, 0), 0, "Hosty", true),
                new LobbySlotState(SkirmishSetupScreen.ApplySuperHard(new SkirmishSlot(SlotKind.Ai, Faction.Directorate, 2, 1, 1))),
            },
            Array.Empty<LobbySpectator>(), 4);
        LobbyData back = LobbyCodec.Decode(LobbyCodec.Encode(d));
        Assert.Equal(d, back);
        Assert.Equal(150, back.Slots[1].Slot.Handicap);
        Assert.True(back.Slots[1].Slot.Commander);
        Assert.Equal(5, back.Slots[1].Slot.AiTier);

        LobbyData Lobby(int h) => d with { Slots = new[] { d.Slots[0], new LobbySlotState(new SkirmishSlot(SlotKind.Ai, Faction.Authority, 2, 1, 1, false, h)) } };
        Assert.Equal(200, LobbyCodec.Decode(LobbyCodec.Encode(Lobby(200))).Slots[1].Slot.Handicap);
        Assert.Throws<InvalidDataException>(() => LobbyCodec.Decode(LobbyCodec.Encode(Lobby(201))));
    }
}
