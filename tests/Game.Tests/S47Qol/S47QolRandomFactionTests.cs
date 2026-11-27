// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Game.Screens;
using Rts.Sim.Rules;

namespace Rts.Game.Tests.S47Qol;

/// <summary>S47 U-13 (cnc#49): the setup screen's random faction roll.</summary>
public sealed class S47QolRandomFactionTests
{
    private static SkirmishSlot Slot(SlotKind kind, Faction f) => new(kind, f, 1, 0, 0);

    [Fact]
    public void Every_open_slot_gets_the_faction_the_roll_picks_and_closed_slots_are_left_alone()
    {
        var slots = new[] { Slot(SlotKind.Human, Faction.Authority), Slot(SlotKind.Closed, Faction.Authority), Slot(SlotKind.Ai, Faction.Authority) };
        IReadOnlyList<SkirmishSlot> rolled = SkirmishSetupScreen.RandomFactions(slots, n => n - 1);
        Faction last = RuleCounts.PlayableOrder[^1];
        Assert.Equal(last, rolled[0].Faction);
        Assert.Equal(Faction.Authority, rolled[1].Faction);
        Assert.Equal(last, rolled[2].Faction);
        Assert.Equal(slots[0].Colour, rolled[0].Colour);
    }

    [Fact]
    public void A_wild_roll_is_clamped_to_a_playable_faction()
    {
        IReadOnlyList<SkirmishSlot> rolled = SkirmishSetupScreen.RandomFactions(new[] { Slot(SlotKind.Ai, Faction.Authority) }, _ => 999);
        Assert.Contains(rolled[0].Faction, RuleCounts.PlayableOrder);
    }
}
