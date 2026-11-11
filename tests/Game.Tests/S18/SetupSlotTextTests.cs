// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Game.Screens;
using Rts.Game.Ui;
using Rts.Sim.Rules;

namespace Rts.Game.Tests.S18;

/// <summary>S18 UX-LM-03: the setup panel is sized for the widest slot label, so no label a player can build is wider than the column the
/// panel was sized for.</summary>
[Collection("UiStatics")]
public sealed class SetupSlotTextTests
{
    private static readonly string[] Tiers = { "Recruit", "Regular", "Veteran", "Expert", "Elite" };

    [Theory]
    [InlineData(TextSize.Normal)]
    [InlineData(TextSize.ExtraLarge)]
    public void Every_slot_label_is_no_wider_than_the_widest_text_the_panel_is_sized_for(TextSize size)
    {
        float before = UiScale.TextFactor;
        try
        {
            UiScale.TextFactor = UiScale.FactorOf(size);
            UiUnit unit = 2;
            IReadOnlyList<string> widest = SkirmishSetupScreen.WidestSlotTexts(Tiers);
            int limit = widest.Max(t => BitmapFont.Measure("> " + t, unit));
            foreach (SlotKind kind in Enum.GetValues<SlotKind>())
                foreach (Faction f in RuleCounts.PlayableOrder)
                    foreach (bool commander in new[] { false, true })
                        for (int tier = 1; tier <= Tiers.Length; tier++)
                            for (int team = 0; team < SkirmishSettings.MaxSlots; team++)
                                for (int index = 0; index < SkirmishSettings.MaxSlots; index++)
                                {
                                    var slot = new SkirmishSlot(kind, f, tier, 0, team, Commander: commander);
                                    string label = SkirmishSetupScreen.SlotLabel(slot, index, Tiers);
                                    Assert.True(BitmapFont.Measure("> " + label, unit) <= limit, $"'{label}' is wider than the sized-for text");
                                }
        }
        finally
        {
            UiScale.TextFactor = before;
        }
    }
}
