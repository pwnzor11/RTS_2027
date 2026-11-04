// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Game.Screens;
using Rts.Game.Ui.Llm;
using Rts.Game.Ui.Spectator;
using Rts.Llm;
using Rts.Sim.Rules;

namespace Rts.Game.Tests.Skirmish;

[Collection("LlmHooks")]
public sealed class SkirmishCommanderSetupTests
{
    private static readonly string[] Tiers = { "Recruit", "Regular", "Veteran", "Expert", "Elite" };

    [Fact]
    public void Toggle_cycles_on_an_AI_slot()
    {
        var ai = new SkirmishSlot(SlotKind.Ai, Faction.Authority, 3, 0, 0);
        SkirmishSlot on = SkirmishSetupScreen.ToggleCommander(ai);
        Assert.True(on.Commander);
        Assert.False(SkirmishSetupScreen.ToggleCommander(on).Commander);
    }

    [Theory]
    [InlineData(SlotKind.Human)]
    [InlineData(SlotKind.Closed)]
    public void Toggle_is_ignored_for_Human_and_Closed(SlotKind kind)
    {
        var slot = new SkirmishSlot(kind, Faction.Authority, 2, 0, 0);
        Assert.Equal(slot, SkirmishSetupScreen.ToggleCommander(slot));
    }

    [Fact]
    public void Label_shows_LLM_on_top_of_the_tier_and_only_for_commander_slots()
    {
        var plain = new SkirmishSlot(SlotKind.Ai, Faction.Authority, 3, 0, 0);
        Assert.Equal("2 AI Veteran Authority T1", SkirmishSetupScreen.SlotLabel(plain, 1, Tiers));
        string label = SkirmishSetupScreen.SlotLabel(plain with { Commander = true }, 1, Tiers);
        Assert.Contains("LLM", label);
        Assert.Contains("Veteran", label);
    }

    [Fact]
    public void Key_check_needs_a_provider_and_a_stored_key()
    {
        ISecretStore old = LlmHooks.SecretStore;
        try
        {
            var store = new MemorySecretStore();
            LlmHooks.SecretStore = store;
            var none = new Settings(LlmProvider: "none");
            var openai = new Settings(LlmProvider: "openai");
            Assert.False(SkirmishSetupScreen.HasCommanderKey(openai));   // provider but no key
            store.Save("sk-test");
            Assert.False(SkirmishSetupScreen.HasCommanderKey(none));     // key but no provider
            Assert.True(SkirmishSetupScreen.HasCommanderKey(openai));
        }
        finally { LlmHooks.SecretStore = old; }
        Assert.Contains("no key", SetupText.NoKeyHint);
    }

    [Fact]
    public void Spectator_rows_carry_the_LLM_tag_and_all_commander_games_validate()
    {
        var slots = new[]
        {
            new SkirmishSlot(SlotKind.Ai, Faction.Authority, 2, 0, 0, Commander: true),
            new SkirmishSlot(SlotKind.Ai, Faction.Directorate, 2, 1, 1, Commander: true),
        };
        var s = new SkirmishSettings(new MapChoice(null, new Rts.Data.MapGenParams(Rts.Data.Biome.RustBelt, Rts.Data.MapSize.Small, 2, 7)), slots, 7);
        Assert.Empty(s.Validate());
        Assert.True(s.IsSpectator);
        Assert.Equal(new[] { true, true }, SpectatorSetup.CommandersOf(s));

        var row = new SpectatorRow(0, 0, Faction.Authority, 1, 0, 0, 0, 0, 0, 0, 0, default, 0, false, true);
        Assert.Contains("P1 LLM", SpectatorTable.Compact(row));
        Assert.Contains("P1 LLM", SpectatorTable.Detail(row));
        Assert.DoesNotContain("LLM", SpectatorTable.Compact(row with { Commander = false }));
    }
}
