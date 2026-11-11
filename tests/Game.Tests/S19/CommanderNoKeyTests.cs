// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Game.Screens;
using Rts.Sim.Rules;

namespace Rts.Game.Tests.S19;

/// <summary>S19 UI1 item 4 (D-205): picking an LLM commander with no key shows a notice first.</summary>
public sealed class CommanderNoKeyTests
{
    private static SkirmishSlot Ai(bool commander) => new(SlotKind.Ai, default, 2, 0, 1, commander);

    [Fact]
    public void Switching_a_commander_on_without_a_key_needs_the_notice()
    {
        SkirmishSlot off = Ai(false);
        Assert.True(SkirmishSetupScreen.NeedsNoKeyNotice(off, SkirmishSetupScreen.ToggleCommander(off), hasKey: false));
    }

    [Fact]
    public void A_key_or_switching_off_or_a_non_AI_slot_needs_no_notice()
    {
        SkirmishSlot off = Ai(false), on = Ai(true);
        Assert.False(SkirmishSetupScreen.NeedsNoKeyNotice(off, SkirmishSetupScreen.ToggleCommander(off), hasKey: true));
        Assert.False(SkirmishSetupScreen.NeedsNoKeyNotice(on, SkirmishSetupScreen.ToggleCommander(on), hasKey: false));
        var human = new SkirmishSlot(SlotKind.Human, default, 2, 0, 1);
        Assert.False(SkirmishSetupScreen.NeedsNoKeyNotice(human, SkirmishSetupScreen.ToggleCommander(human), hasKey: false));
    }

    [Fact]
    public void The_notice_text_says_what_happens_and_what_to_do()
    {
        Assert.Contains("standard AI", SetupText.NoKeyNotice);
        Assert.Contains("Settings", SetupText.NoKeyNotice);
    }
}
